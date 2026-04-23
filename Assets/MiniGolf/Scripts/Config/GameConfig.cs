using UnityEngine;

namespace MiniGolf.Config
{
    /// <summary>
    /// Central ScriptableObject that holds every tunable value in the game.
    /// All fields are editable in the Unity Inspector at runtime, making it
    /// easy to balance the game without recompiling.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "MiniGolf/Game Config")]
    public class GameConfig : ScriptableObject
    {
        // ─────────────────────────── Timer ───────────────────────────

        [Header("Timer")]

        /// <summary>Countdown duration when a new game starts (seconds).</summary>
        public float initialTime = 30f;

        /// <summary>Seconds added to the timer when the ball enters a Good (blue) hole.</summary>
        public float goodHoleTimeBonus = 5f;

        /// <summary>Seconds removed from the timer when the ball enters a Bad (red) hole.</summary>
        public float badHoleTimePenalty = 10f;

        // ─────────────────────────── Ball ───────────────────────────

        [Header("Ball")]

        /// <summary>
        /// Multiplier applied on top of drag distance to determine shoot impulse.
        /// Kept for future use; primary force scaling is via <see cref="maxShootForce"/>.
        /// </summary>
        public float shootForceMultiplier = 6f;

        /// <summary>Maximum impulse (units/s) applied to the ball on release.</summary>
        public float maxShootForce = 18f;

        /// <summary>
        /// Maximum drag distance in world units that counts for force calculation.
        /// Dragging further than this still fires at full power.
        /// </summary>
        public float maxDragDistance = 2.5f;

        /// <summary>
        /// Speed threshold (units/s) below which the ball is considered stopped.
        /// Triggers the "miss" resolution path.
        /// </summary>
        public float ballStopThreshold = 0.05f;

        /// <summary>Seconds to wait after a shot outcome before repositioning holes.</summary>
        public float ballResetDelay = 0.6f;

        /// <summary>Seconds to wait after repositioning holes before resetting the ball.</summary>
        public float holeRepositionDelay = 0.3f;

        // ─────────────────────────── Holes ───────────────────────────

        [Header("Holes")]

        /// <summary>Number of holes present on the playfield simultaneously.</summary>
        public int holeCount = 4;

        /// <summary>Minimum seconds before a hole changes type.</summary>
        public float holeChangeMinInterval = 4f;

        /// <summary>Maximum seconds before a hole changes type.</summary>
        public float holeChangeMaxInterval = 9f;

        /// <summary>
        /// How many seconds before a type change the hole starts flashing.
        /// This warning duration is deducted from the random interval,
        /// so the total cycle length is always <c>randomInterval</c> seconds.
        /// </summary>
        public float holeFlashWarningDuration = 2f;

        // ─────────────────────────── Trajectory ───────────────────────────

        [Header("Trajectory")]

        /// <summary>Number of dot sprites in the trajectory pool.</summary>
        public int trajectoryDotCount = 12;

        /// <summary>World-space distance between consecutive trajectory dots.</summary>
        public float trajectoryDotSpacing = 0.45f;

        /// <summary>Base local scale of each trajectory dot sprite.</summary>
        public float trajectoryDotScale = 0.18f;

        // ─────────────────────────── Playfield ───────────────────────────

        [Header("Playfield")]

        /// <summary>Bottom-left corner of the valid spawn area (world space).</summary>
        public Vector2 playfieldMin = new(-2.3f, -4.5f);

        /// <summary>Top-right corner of the valid spawn area (world space).</summary>
        public Vector2 playfieldMax = new(2.3f, 4.5f);

        /// <summary>World position the ball is reset to after every shot.</summary>
        public Vector2 ballStartPosition = new(0f, -3.8f);

        /// <summary>
        /// Holes will not spawn within this radius of <see cref="ballStartPosition"/>.
        /// Prevents a hole from appearing directly on top of the ball.
        /// </summary>
        public float holeSafeRadiusFromBall = 1.5f;

        /// <summary>
        /// Logical radius of a hole used for minimum spacing checks between holes.
        /// Actual collider radius is set on the prefab independently.
        /// </summary>
        public float holeRadius = 0.42f;
    }
}
