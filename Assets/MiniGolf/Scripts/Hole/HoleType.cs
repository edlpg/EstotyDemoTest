namespace MiniGolf.Hole
{
    /// <summary>
    /// Defines the current behaviour of a hole on the playfield.
    /// <para>
    /// Holes switch between these two types at random intervals.
    /// A flash warning is shown for a configurable duration before each transition
    /// (see <see cref="HoleController.FlashWarningRoutine"/>).
    /// </para>
    /// </summary>
    public enum HoleType
    {
        /// <summary>
        /// Rendered blue. When the ball enters, time is added to the countdown.
        /// Bonus amount is defined by <see cref="Config.GameConfig.goodHoleTimeBonus"/>.
        /// </summary>
        Good,

        /// <summary>
        /// Rendered red. When the ball enters, time is subtracted from the countdown.
        /// Penalty amount is defined by <see cref="Config.GameConfig.badHoleTimePenalty"/>.
        /// </summary>
        Bad
    }
}
