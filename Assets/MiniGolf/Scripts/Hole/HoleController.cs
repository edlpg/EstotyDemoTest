using System;
using System.Collections;
using MiniGolf.Audio;
using MiniGolf.Config;
using UnityEngine;

namespace MiniGolf.Hole
{
    /// <summary>
    /// Controls a single hole on the playfield.
    /// <para>
    /// Responsibilities (Single Responsibility Principle — one per class):
    /// <list type="bullet">
    ///   <item>Maintaining and displaying the hole's current <see cref="HoleType"/>.</item>
    ///   <item>Running the autonomous type-change timer with a pre-change flash warning.</item>
    ///   <item>Detecting ball entry via its trigger collider and broadcasting the event.</item>
    ///   <item>Playing the entry particle effect.</item>
    /// </list>
    /// Repositioning logic lives in <see cref="HoleManager"/> to keep this class focused.
    /// </para>
    /// </summary>
    public class HoleController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private CircleCollider2D _collider;
        [SerializeField] private ParticleSystem _entryParticles;

        private GameConfig _config;

        /// <summary>The inner cavity child SpriteRenderer, repurposed as the warning ring by scaling it out.</summary>
        private SpriteRenderer _outerRing;

        /// <summary>Original state of the cavity — restored after each warning flash.</summary>
        private Vector3 _ringOriginalScale;
        private Color   _ringOriginalColor;
        private int     _ringOriginalSortingOrder;

        /// <summary>Reference to the running type-change coroutine so it can be cancelled cleanly.</summary>
        private Coroutine _changeCoroutine;

        // Pre-defined colors stored as statics to avoid allocating Color structs every frame.
        private static readonly Color GoodColor     = new(0.2f, 0.55f, 1f);
        private static readonly Color BadColor      = new(1f,  0.25f, 0.25f);
        private static readonly Color GoodColorDark = new(0.1f, 0.35f, 0.7f);
        private static readonly Color BadColorDark  = new(0.7f, 0.1f,  0.1f);

        /// <summary>Current type of this hole. Changes at runtime via <see cref="TypeChangeRoutine"/>.</summary>
        public HoleType HoleType { get; private set; }

        /// <summary>
        /// <c>true</c> when the collider is active and the hole can accept ball entry.
        /// Set to <c>false</c> immediately on entry to prevent double-triggering.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Fired when a ball (tagged "Ball") enters the trigger.
        /// Passes <c>this</c> so <see cref="HoleManager"/> can read <see cref="HoleType"/>
        /// without needing a direct reference back to this controller.
        /// </summary>
        public event Action<HoleController> OnBallEntered;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        /// <summary>
        /// Sets up this hole with the given config and initial type, then starts the
        /// autonomous type-change cycle. Called by <see cref="HoleManager"/> after spawning.
        /// </summary>
        /// <param name="config">Shared game configuration (intervals, durations, etc.).</param>
        /// <param name="initialType">The type this hole starts with.</param>
        public void Initialize(GameConfig config, HoleType initialType)
        {
            _config = config;
            FindOuterRing();
            SetType(initialType);
            SetActive(true); // Explicitly enables the collider, not just the flag.
            // Make trigger radius large enough to detect the ball across the whole visual hole.
            _collider.radius = 0.65f;
            StartTypeChangeTimer();
        }

        /// <summary>
        /// Finds the existing inner cavity child SpriteRenderer and saves its state so it
        /// can be temporarily repurposed as the warning ring during flashing.
        /// </summary>
        private void FindOuterRing()
        {
            if (_outerRing != null) return;

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == _spriteRenderer) continue;
                if (sr.gameObject == gameObject) continue; // Never pick root's own SR.

                _outerRing                = sr;
                _ringOriginalScale        = sr.transform.localScale;
                _ringOriginalColor        = sr.color;
                _ringOriginalSortingOrder = sr.sortingOrder;

                // Ensure cavity renders in FRONT of the main circle at rest (hole depth look).
                _outerRing.sortingOrder = _spriteRenderer.sortingOrder + 1;
                break;
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Enables or disables the trigger collider and the <see cref="IsActive"/> flag.
        /// Disabling prevents the hole from accepting ball entries (e.g. immediately after
        /// a ball has entered, or when the game is over).
        /// </summary>
        public void SetActive(bool active)
        {
            IsActive = active;
            // Collider stays enabled at all times; IsActive alone guards OnTriggerEnter2D.
            // Disabling the collider caused permanent misses when spurious overlaps fired
            // during ball resets or spawning.
        }

        /// <summary>
        /// Starts (or restarts) the autonomous type-change coroutine.
        /// Stopping first ensures there is never more than one coroutine running at a time.
        /// </summary>
        public void StartTypeChangeTimer()
        {
            StopTypeChangeTimer();
            _changeCoroutine = StartCoroutine(TypeChangeRoutine());
        }

        /// <summary>Cancels the type-change coroutine if it is currently running.</summary>
        public void StopTypeChangeTimer()
        {
            if (_changeCoroutine != null)
            {
                StopCoroutine(_changeCoroutine);
                _changeCoroutine = null;
            }
            RestoreOuterRing();
        }

        private void RestoreOuterRing()
        {
            if (_outerRing == null) return;
            _outerRing.sortingOrder         = _spriteRenderer.sortingOrder + 1; // Back in front.
            _outerRing.transform.localScale = _ringOriginalScale;
            _outerRing.color                = _ringOriginalColor;
        }

        /// <summary>Plays the one-shot particle burst at this hole's world position.</summary>
        public void PlayEntryEffect()
        {
            _entryParticles?.Play();
        }

        /// <summary>Teleports the hole to <paramref name="position"/> in world space.</summary>
        /// <param name="position">Target XY world position. Z is always kept at 0.</param>
        public void MoveTo(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, 0f);
        }

        // ── Private: Type Management ───────────────────────────────────────────

        /// <summary>
        /// Sets <see cref="HoleType"/> and immediately updates all visuals to match.
        /// </summary>
        private void SetType(HoleType type)
        {
            HoleType = type;
            ApplyColor(HoleType == HoleType.Good ? GoodColor : BadColor);
        }

        /// <summary>
        /// Pushes <paramref name="color"/> to the sprite and to the particle system's
        /// start color so the burst matches the hole's current type.
        /// </summary>
        private void ApplyColor(Color color)
        {
            _spriteRenderer.color = color;

            if (_entryParticles != null)
            {
                // ParticleSystem.main is a struct copy — must be assigned back.
                var main = _entryParticles.main;
                main.startColor = color;
            }
        }

        // ── Private: Coroutines ────────────────────────────────────────────────

        /// <summary>
        /// The core autonomous loop:
        /// <list type="number">
        ///   <item>Pick a random interval within [min, max].</item>
        ///   <item>Wait for (interval − flashDuration) seconds.</item>
        ///   <item>Flash between current and target color for <c>flashDuration</c> seconds.</item>
        ///   <item>Flip the type, update visuals, restart the cycle.</item>
        /// </list>
        /// </summary>
        private IEnumerator TypeChangeRoutine()
        {
            float totalWait      = UnityEngine.Random.Range(_config.holeChangeMinInterval, _config.holeChangeMaxInterval);
            float waitBeforeFlash = totalWait - _config.holeFlashWarningDuration;

            // Only wait if there is time left before the flash should begin.
            if (waitBeforeFlash > 0f)
                yield return new WaitForSeconds(waitBeforeFlash);

            // Alert the player that a change is imminent.
            AudioManager.Instance?.PlayWarning();
            yield return StartCoroutine(FlashWarningRoutine(_config.holeFlashWarningDuration));

            // Flip type and update visuals to reflect the new state.
            HoleType = HoleType == HoleType.Good ? HoleType.Bad : HoleType.Good;
            ApplyColor(HoleType == HoleType.Good ? GoodColor : BadColor);

            // Restart the cycle so the hole keeps changing indefinitely.
            StartTypeChangeTimer();
        }

        /// <summary>
        /// Oscillates the sprite color between the current type's color and the
        /// upcoming type's color for <paramref name="duration"/> seconds.
        /// Uses a sine wave for a smooth, eye-catching pulse rather than a hard blink.
        /// </summary>
        /// <param name="duration">Total flash duration in seconds.</param>
        private IEnumerator FlashWarningRoutine(float duration)
        {
            if (_outerRing == null) yield break;

            // Move cavity BEHIND the main fill so only the scaled-out edge is visible as a ring.
            _outerRing.sortingOrder         = _spriteRenderer.sortingOrder - 1;
            _outerRing.transform.localScale = Vector3.one * 1.15f;

            Color ringColor        = HoleType == HoleType.Good ? BadColor : GoodColor;
            const float flashSpeed = 7f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float alpha      = (Mathf.Sin(elapsed * flashSpeed) + 1f) * 0.5f;
                _outerRing.color = new Color(ringColor.r, ringColor.g, ringColor.b, alpha);
                elapsed         += Time.deltaTime;
                yield return null;
            }

            RestoreOuterRing();
        }

        // ── Physics Callback ───────────────────────────────────────────────────

        /// <summary>
        /// Unity physics callback. Fires when a 2D collider enters this trigger.
        /// Guards against:
        /// <list type="bullet">
        ///   <item>Inactive holes (already consumed by a ball this shot).</item>
        ///   <item>Non-ball objects (walls, other holes) via the "Ball" tag check.</item>
        /// </list>
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other) => HandleBallContact(other);

        /// <summary>
        /// Fallback for cases where Enter fires while the collider is briefly
        /// mid-animation and the engine drops the event. Stay catches those on the
        /// next physics tick without double-triggering (IsActive guard handles that).
        /// </summary>
        private void OnTriggerStay2D(Collider2D other) => HandleBallContact(other);

        private void HandleBallContact(Collider2D other)
        {
            if (!IsActive) return;
            if (!other.CompareTag("Ball")) return;

            // Deactivate immediately to prevent the same ball triggering twice.
            SetActive(false);
            PlayEntryEffect();
            OnBallEntered?.Invoke(this);
        }
    }
}
