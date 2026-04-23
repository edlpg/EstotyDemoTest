using System;
using UnityEngine;

namespace MiniGolf.Services
{
    /// <summary>
    /// MonoBehaviour implementation of <see cref="ITimerService"/>.
    /// <para>
    /// Counts down in <c>Update</c> using <c>Time.deltaTime</c> and broadcasts
    /// every change via <see cref="OnTimeChanged"/>. Time can be added or removed
    /// mid-game (Good/Bad hole effects) with immediate event notification.
    /// </para>
    /// </summary>
    public class TimerService : MonoBehaviour, ITimerService
    {
        /// <inheritdoc/>
        public float RemainingTime { get; private set; }

        /// <inheritdoc/>
        public bool IsRunning { get; private set; }

        /// <inheritdoc/>
        public event Action<float> OnTimeChanged;

        /// <inheritdoc/>
        public event Action OnTimeExpired;

        /// <inheritdoc/>
        public void StartTimer(float duration)
        {
            RemainingTime = duration;
            IsRunning = true;

            // Notify immediately so the UI shows the correct value on the first frame.
            OnTimeChanged?.Invoke(RemainingTime);
        }

        /// <inheritdoc/>
        public void StopTimer() => IsRunning = false;

        /// <inheritdoc/>
        public void AddTime(float seconds)
        {
            RemainingTime += seconds;
            OnTimeChanged?.Invoke(RemainingTime);
        }

        /// <inheritdoc/>
        public void SubtractTime(float seconds)
        {
            // Ignore penalties after the timer has already stopped (e.g. game over).
            if (!IsRunning) return;

            RemainingTime = Mathf.Max(0f, RemainingTime - seconds);
            OnTimeChanged?.Invoke(RemainingTime);

            if (RemainingTime <= 0f)
                ExpireTimer();
        }

        private void Update()
        {
            if (!IsRunning) return;

            RemainingTime -= Time.deltaTime;
            OnTimeChanged?.Invoke(RemainingTime);

            if (RemainingTime <= 0f)
            {
                RemainingTime = 0f; // Clamp so UI never shows a negative value.
                ExpireTimer();
            }
        }

        /// <summary>
        /// Stops the timer and fires the expiry event.
        /// Centralised so both <c>Update</c> and <c>SubtractTime</c> behave identically.
        /// </summary>
        private void ExpireTimer()
        {
            IsRunning = false;
            OnTimeExpired?.Invoke();
        }
    }
}
