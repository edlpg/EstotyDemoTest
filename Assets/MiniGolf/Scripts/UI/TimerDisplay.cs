using System.Collections;
using MiniGolf.Audio;
using MiniGolf.Services;
using TMPro;
using UnityEngine;

namespace MiniGolf.UI
{
    /// <summary>
    /// Displays the countdown timer and animates a bonus/penalty popup
    /// whenever time is added or removed.
    /// <para>
    /// Binds to <see cref="ITimerService"/> via <see cref="Bind"/> rather than
    /// holding a concrete <see cref="TimerService"/> reference (Dependency Inversion).
    /// This means the display can be unit-tested with a mock timer.
    /// </para>
    /// </summary>
    public class TimerDisplay : MonoBehaviour
    {
        /// <summary>Main countdown label (e.g. "30", "07").</summary>
        [SerializeField] private TMP_Text _timerText;

        /// <summary>
        /// Short-lived popup showing "+5s" or "-10s" after a hole event.
        /// Starts disabled and is shown/hidden by <see cref="AnimateBonus"/>.
        /// </summary>
        [SerializeField] private TMP_Text _bonusText;

        // Color thresholds — pre-defined as statics to avoid per-frame allocations.
        private static readonly Color NormalColor        = Color.white;
        private static readonly Color WarningColor       = new(1f, 0.7f, 0.1f); // Orange — ≤ 10s
        private static readonly Color CriticalColor      = new(1f, 0.2f, 0.2f); // Red    — ≤ 5s
        private static readonly Color BonusPositiveColor = new(0.2f, 1f, 0.4f);
        private static readonly Color BonusNegativeColor = new(1f, 0.3f, 0.3f);

        // Pre-built string table "00"–"99" — avoids per-frame ToString allocations.
        private static readonly string[] TimerStrings = new string[100];
        static TimerDisplay()
        {
            for (int i = 0; i < 100; i++) TimerStrings[i] = i.ToString("D2");
        }

        private int _lastDisplayedSeconds = -1;

        /// <summary>Reference to the running bonus animation so a new one can cancel the previous.</summary>
        private Coroutine    _bonusCoroutine;
        private RectTransform _bonusRect;

        /// <summary>
        /// Cached anchored position of the bonus text, used as the animation start point.
        /// Stored in <c>Awake</c> so the animation always resets to the same origin.
        /// </summary>
        private Vector2 _bonusOriginPos;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_bonusText != null)
            {
                _bonusRect      = _bonusText.GetComponent<RectTransform>();
                _bonusOriginPos = _bonusRect.anchoredPosition;
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Subscribes to <see cref="ITimerService.OnTimeChanged"/> so the display
        /// updates automatically every frame. Call once after the timer is initialised.
        /// </summary>
        /// <param name="timerService">The timer this display should reflect.</param>
        public void Bind(ITimerService timerService)
        {
            timerService.OnTimeChanged += OnTimeChanged;
        }

        /// <summary>
        /// Triggers the animated bonus/penalty popup.
        /// If a previous animation is still running, it is cancelled and restarted.
        /// </summary>
        /// <param name="amount">
        /// Positive value for a time bonus (green "+5s"), negative for a penalty (red "-10s").
        /// </param>
        public void ShowBonus(float amount)
        {
            if (_bonusCoroutine != null)
                StopCoroutine(_bonusCoroutine);

            _bonusCoroutine = StartCoroutine(AnimateBonus(amount));
        }

        // ── Private: Timer Callback ────────────────────────────────────────────

        /// <summary>
        /// Invoked every frame by the timer service. Updates the text label and
        /// applies a colour-coded urgency indicator:
        /// <list type="bullet">
        ///   <item>White  — more than 10 s remaining (normal)</item>
        ///   <item>Orange — 6–10 s remaining (warning)</item>
        ///   <item>Red    — 0–5 s remaining (critical)</item>
        /// </list>
        /// </summary>
        private void OnTimeChanged(float remainingTime)
        {
            if (_timerText == null) return;

            _timerText.color = remainingTime switch
            {
                <= 5f  => CriticalColor,
                <= 10f => WarningColor,
                _      => NormalColor
            };

            // Only rebuild the text string when the displayed integer second changes.
            int seconds = Mathf.CeilToInt(remainingTime);
            if (seconds == _lastDisplayedSeconds) return;
            _lastDisplayedSeconds = seconds;
            _timerText.text = TimerStrings[Mathf.Clamp(seconds, 0, 99)];

            // Tick sound on every whole second in the critical zone.
            if (seconds <= 5 && seconds > 0)
                AudioManager.Instance?.PlayCountdown();
        }

        // ── Private: Bonus Animation ───────────────────────────────────────────

        /// <summary>
        /// Animates the bonus text: it appears at its origin, rises 55 px over 1.3 s,
        /// and fades out during the second half of the animation.
        /// Uses <c>RectTransform.anchoredPosition</c> rather than
        /// <c>transform.localPosition</c> for reliable Canvas-space movement.
        /// </summary>
        private IEnumerator AnimateBonus(float amount)
        {
            if (_bonusText == null || _bonusRect == null) yield break;

            bool isPositive = amount > 0;

            // Format: "+5s" for bonuses, "-10s" for penalties.
            _bonusText.text  = isPositive ? $"+{amount:F0}s" : $"{amount:F0}s";
            _bonusText.color = isPositive ? BonusPositiveColor : BonusNegativeColor;

            // Reset to origin before starting the animation.
            _bonusRect.anchoredPosition = _bonusOriginPos;
            _bonusText.alpha            = 1f;
            _bonusText.gameObject.SetActive(true);

            float elapsed       = 0f;
            const float duration = 1.3f;
            const float rise     = 55f; // Canvas pixels to rise (at 1920×1080 reference resolution).

            while (elapsed < duration)
            {
                float t = elapsed / duration;

                // Linear upward movement.
                _bonusRect.anchoredPosition = _bonusOriginPos + Vector2.up * (rise * t);

                // Solid for the first 55% of the duration, then linear fade out.
                _bonusText.alpha = t < 0.55f
                    ? 1f
                    : Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Snap back to origin and hide ready for the next call.
            _bonusRect.anchoredPosition = _bonusOriginPos;
            _bonusText.gameObject.SetActive(false);
        }
    }
}
