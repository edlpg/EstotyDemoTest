using System;
using System.Collections;
using MiniGolf.Audio;
using MiniGolf.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniGolf.Ball
{
    /// <summary>
    /// Controls the ball: handles player input, drives the Rigidbody2D,
    /// manages the trajectory preview, and broadcasts shot lifecycle events.
    /// <para>
    /// Input is read from <c>UnityEngine.InputSystem.Pointer.current</c>, which
    /// unifies mouse (editor) and touch (mobile) into a single code path.
    /// This project uses the <b>New Input System only</b> — <c>UnityEngine.Input</c>
    /// is not available.
    /// </para>
    /// <para>
    /// <b>Drag mechanic:</b> The player presses anywhere on screen to record an
    /// origin point, then drags to set direction and power. The trajectory line
    /// is drawn from the ball outward in the drag direction. On release, an impulse
    /// is applied proportional to drag distance (clamped to <see cref="GameConfig.maxDragDistance"/>).
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class BallController : MonoBehaviour
    {
        [SerializeField] private TrajectoryRenderer _trajectoryRenderer;
        [SerializeField] private SpriteRenderer     _spriteRenderer;

        private GameConfig  _config;
        private Rigidbody2D _rb;
        private Camera      _mainCamera;

        /// <summary>Reference kept so the coroutine can be cancelled if needed (e.g. hole entry).</summary>
        private Coroutine _monitorCoroutine;

        /// <summary>Reference kept so an in-progress spawn animation can be interrupted on quick resets.</summary>
        private Coroutine _spawnCoroutine;

        /// <summary>World-space position where the player first pressed down this frame.</summary>
        private Vector2 _pressWorldPos;

        /// <summary>Ball world position at the moment aiming started. Used to restore after pullback.</summary>
        private Vector2 _aimOrigin;

        /// <summary>How far the ball can visually pull back at max drag (world units).</summary>
        private const float MaxPullback = 0.3f;

        /// <summary><c>true</c> while the player is holding and the trajectory is visible.</summary>
        private bool _isAiming;

        /// <summary>
        /// When <c>false</c>, all input is ignored.
        /// Toggled by <see cref="Core.GameManager"/> to enforce the game state machine.
        /// </summary>
        private bool _inputEnabled;

        // Pre-defined colors to avoid allocating Color structs on every color change.
        private static readonly Color BallIdleColor   = new(0.2f, 0.85f, 0.35f);
        private static readonly Color BallAimingColor = new(0.45f, 1f,   0.55f);

        // Cached WaitForSeconds — reusing instances avoids per-shot heap allocations.
        private static readonly WaitForSeconds WaitGracePeriod = new(0.4f);
        private static readonly WaitForSeconds WaitPollInterval = new(0.06f);

        private float _cameraDepth;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fired when the player presses down and begins aiming.</summary>
        public event Action OnAimStarted;

        /// <summary>Fired when the player releases and the ball is launched.</summary>
        public event Action OnShotFired;

        /// <summary>
        /// Fired when the ball's speed drops below <see cref="GameConfig.ballStopThreshold"/>
        /// without having entered a hole — i.e. a missed shot.
        /// </summary>
        public event Action OnBallStopped;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _rb           = GetComponent<Rigidbody2D>();
            _mainCamera   = Camera.main;
            _cameraDepth  = -_mainCamera.transform.position.z;
        }

        /// <summary>
        /// Passes config to the trajectory renderer and places the ball at its start position.
        /// Called by <see cref="Core.GameManager.StartGame"/> before input is enabled.
        /// </summary>
        public void Initialize(GameConfig config)
        {
            _config = config;
            _trajectoryRenderer.Initialize(config);
            ResetToStartPosition();
        }

        // ── Input Toggle ───────────────────────────────────────────────────────

        /// <summary>Allows <c>Update</c> to process pointer input.</summary>
        public void EnableInput() => _inputEnabled = true;

        /// <summary>
        /// Blocks pointer input and cancels any in-progress aim (hides trajectory,
        /// restores idle ball colour) so the UI is never left in an inconsistent state.
        /// </summary>
        public void DisableInput()
        {
            _inputEnabled = false;
            if (_isAiming)
                CancelAim();
        }

        // ── Ball Control ───────────────────────────────────────────────────────

        /// <summary>
        /// Immediately halts the ball and switches the Rigidbody2D to kinematic
        /// so physics cannot move it further. Also cancels the stop-monitor coroutine
        /// to prevent a stale <see cref="OnBallStopped"/> from firing.
        /// </summary>
        public void FreezeBall()
        {
            StopMonitoring();
            _rb.linearVelocity  = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.isKinematic     = true;
        }

        /// <summary>
        /// Freezes physics, teleports the ball to <see cref="GameConfig.ballStartPosition"/>,
        /// re-enables dynamic physics, and plays the elastic spawn animation.
        /// </summary>
        public void ResetToStartPosition()
        {
            StopMonitoring();
            _rb.linearVelocity  = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.isKinematic     = false;

            transform.position = new Vector3(
                _config.ballStartPosition.x,
                _config.ballStartPosition.y,
                0f
            );

            // Cancel any previous spawn animation that may still be running.
            if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = StartCoroutine(SpawnAnimation());
        }

        // ── Input Handling ─────────────────────────────────────────────────────

        private void Update()
        {
            if (!_inputEnabled) return;

            // Pointer.current unifies Mouse and Touchscreen input.
            // Returns null if no device is connected.
            var pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
                HandlePressDown(pointer.position.ReadValue());
            else if (pointer.press.isPressed && _isAiming)
                HandlePressDrag(pointer.position.ReadValue());
            else if (pointer.press.wasReleasedThisFrame && _isAiming)
                HandlePressUp(pointer.position.ReadValue());
        }

        /// <summary>
        /// Records the press origin in world space and notifies <see cref="Core.GameManager"/>
        /// so it can lock hole positions before any drag occurs.
        /// </summary>
        private void HandlePressDown(Vector2 screenPos)
        {
            _pressWorldPos        = ScreenToWorld(screenPos);
            _aimOrigin            = transform.position;
            _isAiming             = true;
            _rb.isKinematic       = true; // Prevent physics fighting manual pullback position.
            _spriteRenderer.color = BallAimingColor;
            OnAimStarted?.Invoke();
        }

        /// <summary>
        /// Recomputes the drag delta each frame and updates the trajectory visualisation.
        /// The dead zone (0.01 world units) prevents jitter when the finger is stationary.
        /// </summary>
        private void HandlePressDrag(Vector2 screenPos)
        {
            Vector2 currentWorldPos = ScreenToWorld(screenPos);
            Vector2 delta           = currentWorldPos - _pressWorldPos;

            if (delta.magnitude < 0.01f) return; // Dead zone — ignore micro-movements.

            float distance        = Mathf.Min(delta.magnitude, _config.maxDragDistance);
            float normalizedForce = distance / _config.maxDragDistance;

            // Pull the ball back in the drag direction, dampened (not 1:1 with finger).
            Vector2 pullback = delta.normalized * (normalizedForce * MaxPullback);
            transform.position = new Vector3(_aimOrigin.x + pullback.x, _aimOrigin.y + pullback.y, 0f);

            // Trajectory starts from the pulled-back ball position.
            _trajectoryRenderer.UpdateTrajectory(
                transform.position,
                -delta.normalized,
                normalizedForce
            );
        }

        /// <summary>
        /// Hides the trajectory and fires the ball if the drag exceeds a minimum
        /// threshold (0.05 world units). Drags shorter than this are treated as taps
        /// and do not fire — preventing accidental zero-power shots.
        /// </summary>
        private void HandlePressUp(Vector2 screenPos)
        {
            _trajectoryRenderer.Hide();
            _isAiming             = false;
            _spriteRenderer.color = BallIdleColor;

            // Snap ball back to its origin before launching.
            transform.position = new Vector3(_aimOrigin.x, _aimOrigin.y, 0f);

            Vector2 currentWorldPos = ScreenToWorld(screenPos);
            Vector2 delta           = currentWorldPos - _pressWorldPos;

            // Ignore accidental taps or near-zero drags.
            if (delta.magnitude < 0.05f)
            {
                _rb.isKinematic = false;
                return;
            }

            float distance = Mathf.Min(delta.magnitude, _config.maxDragDistance);
            float force    = (distance / _config.maxDragDistance) * _config.maxShootForce;

            // Negate: drag down → shoot up (slingshot mechanic).
            Shoot(-delta.normalized, force);
        }

        // ── Physics ────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies a linear velocity impulse to the Rigidbody2D and starts monitoring
        /// for when the ball comes to a stop.
        /// </summary>
        private void Shoot(Vector2 direction, float force)
        {
            _rb.isKinematic    = false;
            _rb.linearVelocity = direction * force;
            AudioManager.Instance?.PlayShoot();
            OnShotFired?.Invoke();
            _monitorCoroutine = StartCoroutine(MonitorBallStop());
        }

        /// <summary>
        /// Polls the ball's speed every 0.06 s (after an initial 0.4 s grace period
        /// to let the ball build velocity). Once speed falls below
        /// <see cref="GameConfig.ballStopThreshold"/>, fires <see cref="OnBallStopped"/>.
        /// <para>
        /// The grace period prevents a false "stopped" reading in the first frames
        /// after the impulse is applied, when the Rigidbody2D hasn't fully resolved yet.
        /// </para>
        /// </summary>
        private IEnumerator MonitorBallStop()
        {
            yield return WaitGracePeriod;

            while (true)
            {
                if (_rb.linearVelocity.magnitude < _config.ballStopThreshold)
                {
                    StopMonitoring();
                    OnBallStopped?.Invoke();
                    yield break;
                }
                yield return WaitPollInterval;
            }
        }

        /// <summary>Cancels the monitor coroutine and clears the reference.</summary>
        private void StopMonitoring()
        {
            if (_monitorCoroutine == null) return;
            StopCoroutine(_monitorCoroutine);
            _monitorCoroutine = null;
        }

        /// <summary>
        /// Restores the idle state without firing a shot.
        /// Called by <see cref="DisableInput"/> when input is cut mid-aim.
        /// </summary>
        private void CancelAim()
        {
            _isAiming             = false;
            _rb.isKinematic       = false;
            transform.position    = new Vector3(_aimOrigin.x, _aimOrigin.y, 0f);
            _trajectoryRenderer.Hide();
            _spriteRenderer.color = BallIdleColor;
        }

        // ── Animations ─────────────────────────────────────────────────────────

        /// <summary>
        /// Scales the ball from zero to its full size with a brief overshoot
        /// (elastic feel) using a sine-based curve. Runs every time the ball resets.
        /// </summary>
        private IEnumerator SpawnAnimation()
        {
            transform.localScale  = Vector3.zero;
            _spriteRenderer.color = BallIdleColor;

            float elapsed       = 0f;
            const float duration = 0.28f;

            while (elapsed < duration)
            {
                float t         = elapsed / duration;
                // Overshoot peaks at t=0.5 (sin(π*0.5) = 1) then settles back.
                float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                transform.localScale = Vector3.one * (t * overshoot);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = Vector3.one; // Snap to exact final scale.
        }

        // ── Utilities ──────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a screen-space position (pixels) to world-space (units).
        /// Works correctly for an orthographic camera at any position on the Z axis.
        /// </summary>
        private Vector2 ScreenToWorld(Vector2 screenPos)
        {
            // The third component of ScreenToWorldPoint is the distance from the camera.
            // For an orthographic camera at z = -10, objects sit at z = 0,
            // so the distance is -cameraZ = 10.
            Vector3 world = _mainCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, _cameraDepth)
            );
            return new Vector2(world.x, world.y);
        }
    }
}
