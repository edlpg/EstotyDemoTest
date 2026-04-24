using System.Collections;
using MiniGolf.Audio;
using MiniGolf.Ball;
using MiniGolf.Config;
using MiniGolf.Hole;
using MiniGolf.Services;
using MiniGolf.UI;
using UnityEngine;

namespace MiniGolf.Core
{
    /// <summary>
    /// Central coordinator for the entire game session.
    /// <para>
    /// <b>Responsibilities (Single Responsibility):</b> GameManager owns the state
    /// machine and wires all subsystems together via events. It contains no game logic
    /// of its own — it delegates to specialised classes:
    /// <list type="bullet">
    ///   <item><see cref="TimerService"/>  — countdown logic</item>
    ///   <item><see cref="BallController"/> — input and physics</item>
    ///   <item><see cref="HoleManager"/>   — hole spawning and repositioning</item>
    ///   <item><see cref="TimerDisplay"/>  — UI feedback</item>
    ///   <item><see cref="GameOverPanel"/> — end-game UI</item>
    ///   <item><see cref="AudioManager"/>  — sound effects</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Open/Closed:</b> New game phases (e.g. a pause state) can be added to
    /// <see cref="GameState"/> and handled here without modifying any other class.
    /// </para>
    /// <para>
    /// <b>Dependency Inversion:</b> The timer is referenced as <see cref="ITimerService"/>
    /// so the concrete implementation can be replaced (e.g. for tests) without
    /// touching this class. <c>TimerDisplay.Bind</c> also receives the interface.
    /// </para>
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private GameConfig _config;

        [Header("Services")]
        [SerializeField] private TimerService _timerService;

        [Header("Gameplay")]
        [SerializeField] private BallController _ballController;
        [SerializeField] private HoleManager    _holeManager;

        [Header("UI")]
        [SerializeField] private TimerDisplay  _timerDisplay;
        [SerializeField] private GameOverPanel _gameOverPanel;
        [SerializeField] private TMPro.TMP_Text _scoreText;

        public GameState CurrentState { get; private set; } = GameState.Idle;

        private int _score;
        private Coroutine _resolveShotCoroutine;

        private static readonly WaitForSeconds WaitSpawnAnimation = new(0.35f);
        private WaitForSeconds _waitBallReset;
        private WaitForSeconds _waitHoleReposition;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            Application.targetFrameRate = 60;
            EnsureEventSystem();
            BindEvents();
            StartGame();
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // ── Initialisation ─────────────────────────────────────────────────────

        /// <summary>
        /// Subscribes all cross-system event handlers in one place so the wiring
        /// is easy to audit at a glance without digging through individual classes.
        /// </summary>
        private void BindEvents()
        {
            // Timer → UI and game-over detection.
            _timerService.OnTimeExpired      += HandleTimeExpired;
            _timerDisplay.Bind(_timerService); // Passes ITimerService — DIP in practice.

            // Ball lifecycle → state machine transitions.
            _ballController.OnAimStarted += HandleAimStarted;
            _ballController.OnShotFired  += HandleShotFired;
            _ballController.OnBallStopped += HandleBallStopped;

            // Hole entry → timer effect.
            _holeManager.OnBallEnteredHole += HandleBallEnteredHole;
        }

        /// <summary>
        /// Resets everything to its initial state and starts the countdown.
        /// Also called indirectly via scene reload (Restart button).
        /// </summary>
        private void StartGame()
        {
            _waitBallReset      = new WaitForSeconds(_config.ballResetDelay);
            _waitHoleReposition = new WaitForSeconds(_config.holeRepositionDelay);
            CurrentState = GameState.Idle;
            _score = 0;
            UpdateScoreText();
            _gameOverPanel.Hide();
            _holeManager.Initialize(_config);
            _ballController.Initialize(_config);
            _timerService.StartTimer(_config.initialTime);
            _ballController.EnableInput();
        }


        // ── State Machine Handlers ─────────────────────────────────────────────

        /// <summary>
        /// Fires when the player presses down.
        /// Locks hole positions so they cannot jump mid-aim (spec requirement).
        /// Guard: only valid from <see cref="GameState.Idle"/>.
        /// </summary>
        private void HandleAimStarted()
        {
            if (CurrentState != GameState.Idle) return;

            CurrentState = GameState.Aiming;
            _holeManager.LockPositions();
        }

        /// <summary>
        /// Fires when the player releases and the ball is launched.
        /// Unlocks holes (they can now reposition after the shot resolves)
        /// and disables input until the ball has stopped.
        /// </summary>
        private void HandleShotFired()
        {
            CurrentState = GameState.InFlight;
            _holeManager.UnlockPositions();

            // Prevent a second shot while the ball is in the air.
            _ballController.DisableInput();
        }

        /// <summary>
        /// Fires when the ball's speed drops below the stop threshold without
        /// entering a hole — a missed shot.
        /// Guard: only valid from <see cref="GameState.InFlight"/> (prevents
        /// the coroutine from firing twice if FreezeBall was already called).
        /// </summary>
        private void HandleBallStopped()
        {
            if (CurrentState != GameState.InFlight) return;

            CurrentState = GameState.Resolving;
            AudioManager.Instance?.PlayMiss();
            _resolveShotCoroutine = StartCoroutine(ResolveShot());
        }

        /// <summary>
        /// Fires when the ball enters any hole.
        /// Freezes the ball at the hole, applies the timer effect, then resolves.
        /// Primary guard: <see cref="GameState.InFlight"/>.
        /// Fallback guard: <see cref="GameState.Resolving"/> — catches the race where
        /// MonitorBallStop fires just before OnTriggerStay2D on a slow-rolling ball.
        /// In that case the running miss-resolution is cancelled and replaced by the
        /// correct hole-entry resolution.
        /// </summary>
        private void HandleBallEnteredHole(HoleType holeType)
        {
            if (CurrentState != GameState.InFlight && CurrentState != GameState.Resolving) return;

            // Cancel a miss-resolution that may have started a split-second earlier.
            if (_resolveShotCoroutine != null)
            {
                StopCoroutine(_resolveShotCoroutine);
                _resolveShotCoroutine = null;
            }

            CurrentState = GameState.Resolving;
            _ballController.FreezeBall(); // Stop the ball at the hole position.

            ApplyHoleEffect(holeType);
            _resolveShotCoroutine = StartCoroutine(ResolveShot());
        }

        /// <summary>
        /// Fires when the countdown timer reaches zero.
        /// Freezes all movement, locks holes, and shows the game-over panel.
        /// </summary>
        private void HandleTimeExpired()
        {
            CurrentState = GameState.GameOver;
            _ballController.DisableInput();
            _ballController.FreezeBall();
            _holeManager.LockPositions();
            _holeManager.StopAllTimers();
            AudioManager.Instance?.PlayGameOver();
            _gameOverPanel.Show(_score);
        }

        // ── Private Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Applies the timer delta for a hole entry and triggers the UI popup and sound.
        /// Isolated into its own method (Single Responsibility) so <see cref="HandleBallEnteredHole"/>
        /// stays focused on state transitions rather than effect logic.
        /// </summary>
        private void ApplyHoleEffect(HoleType holeType)
        {
            if (holeType == HoleType.Good)
            {
                _score++;
                UpdateScoreText();
                _timerService.AddTime(_config.goodHoleTimeBonus);
                _timerDisplay.ShowBonus(_config.goodHoleTimeBonus);
                AudioManager.Instance?.PlayGoodHole();
            }
            else
            {
                _timerService.SubtractTime(_config.badHoleTimePenalty);
                _timerDisplay.ShowBonus(-_config.badHoleTimePenalty);
                AudioManager.Instance?.PlayBadHole();
            }
        }

        /// <summary>
        /// Coroutine that runs after every shot outcome (hole or miss):
        /// <list type="number">
        ///   <item>Wait <see cref="GameConfig.ballResetDelay"/> — gives the player a moment to see the outcome.</item>
        ///   <item>Reposition holes to new random locations.</item>
        ///   <item>Wait <see cref="GameConfig.holeRepositionDelay"/> — brief pause so repositioning is visible.</item>
        ///   <item>Reset the ball to the start position with a spawn animation.</item>
        ///   <item>Wait 0.35 s — let the animation finish before accepting new input.</item>
        ///   <item>Return to <see cref="GameState.Idle"/> and re-enable input.</item>
        /// </list>
        /// Exits early if the timer expired during the wait (GameOver takes priority).
        /// </summary>
        private void UpdateScoreText()
        {
            if (_scoreText != null)
                _scoreText.text = _score.ToString();
        }

        private IEnumerator ResolveShot()
        {
            yield return _waitBallReset;

            // Timer may have expired while we were waiting — don't override GameOver.
            if (CurrentState == GameState.GameOver) yield break;

            _holeManager.RepositionHoles();
            yield return _waitHoleReposition;

            _ballController.ResetToStartPosition();
            yield return WaitSpawnAnimation;

            // Re-arm holes only after the ball has finished spawning at its start
            // position — safe radius guarantees no hole is sitting on the ball here.
            _holeManager.ActivateHoles();
            CurrentState = GameState.Idle;
            _ballController.EnableInput();
        }
    }
}
