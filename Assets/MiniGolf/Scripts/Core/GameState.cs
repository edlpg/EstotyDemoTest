namespace MiniGolf.Core
{
    /// <summary>
    /// Represents every distinct phase the game can be in.
    /// <para>
    /// The state machine lives in <see cref="GameManager"/> and transitions
    /// are driven by events from <see cref="Ball.BallController"/> and
    /// <see cref="Services.TimerService"/>.
    /// </para>
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// Ball is at the start position, waiting for the player to press.
        /// Input is enabled; holes are unlocked and may reposition.
        /// </summary>
        Idle,

        /// <summary>
        /// Player is holding down and the trajectory line is visible.
        /// Hole positions are locked — they must not move while the player aims.
        /// </summary>
        Aiming,

        /// <summary>
        /// Ball has been released and is travelling across the playfield.
        /// Input is disabled. The ball-stop monitor coroutine is running.
        /// </summary>
        InFlight,

        /// <summary>
        /// A shot outcome was detected (hole entry or ball stopped without scoring).
        /// The game waits briefly, repositions holes, then resets the ball.
        /// Input remains disabled throughout this phase.
        /// </summary>
        Resolving,

        /// <summary>
        /// The countdown timer reached zero.
        /// All input and movement are frozen; the game-over panel is visible.
        /// </summary>
        GameOver
    }
}
