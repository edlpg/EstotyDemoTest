using System;

namespace MiniGolf.Services
{
    /// <summary>
    /// Contract for the game's countdown timer.
    /// <para>
    /// Abstracting the timer behind an interface (Dependency Inversion Principle)
    /// means any consumer — <c>GameManager</c>, <c>TimerDisplay</c>, tests — can
    /// depend on this contract rather than the concrete <see cref="TimerService"/>
    /// MonoBehaviour. Swapping implementations (e.g. a networked or cheat timer)
    /// requires no changes to consumers.
    /// </para>
    /// </summary>
    public interface ITimerService
    {
        /// <summary>Seconds remaining on the countdown. Never goes below zero.</summary>
        float RemainingTime { get; }

        /// <summary>
        /// <c>true</c> while the timer is actively counting down.
        /// <c>false</c> when stopped, paused, or after expiry.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Fired every frame while the timer is running, and once immediately
        /// when time is added or subtracted.
        /// <para>Parameter: current <see cref="RemainingTime"/>.</para>
        /// </summary>
        event Action<float> OnTimeChanged;

        /// <summary>
        /// Fired once when <see cref="RemainingTime"/> reaches zero.
        /// Consumers should treat this as the game-over signal.
        /// </summary>
        event Action OnTimeExpired;

        /// <summary>
        /// Initialises and starts the countdown from <paramref name="duration"/> seconds.
        /// Fires <see cref="OnTimeChanged"/> immediately with the starting value.
        /// </summary>
        /// <param name="duration">Starting time in seconds.</param>
        void StartTimer(float duration);

        /// <summary>Pauses the countdown without resetting it.</summary>
        void StopTimer();

        /// <summary>
        /// Adds <paramref name="seconds"/> to the remaining time (Good hole reward).
        /// Fires <see cref="OnTimeChanged"/> immediately.
        /// </summary>
        /// <param name="seconds">Positive value to add.</param>
        void AddTime(float seconds);

        /// <summary>
        /// Subtracts <paramref name="seconds"/> from the remaining time (Bad hole penalty).
        /// Clamps to zero and fires <see cref="OnTimeExpired"/> if the result reaches zero.
        /// Does nothing when the timer is already stopped.
        /// </summary>
        /// <param name="seconds">Positive value to subtract.</param>
        void SubtractTime(float seconds);
    }
}
