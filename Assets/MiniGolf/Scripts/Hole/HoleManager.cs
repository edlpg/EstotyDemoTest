using System;
using System.Collections.Generic;
using MiniGolf.Config;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MiniGolf.Hole
{
    /// <summary>
    /// Manages the collection of holes on the playfield.
    /// <para>
    /// Responsibilities (Single Responsibility Principle):
    /// <list type="bullet">
    ///   <item>Spawning and destroying <see cref="HoleController"/> instances.</item>
    ///   <item>Repositioning all holes to new random positions after each shot.</item>
    ///   <item>Locking/unlocking position changes based on game state.</item>
    ///   <item>Aggregating per-hole <c>OnBallEntered</c> events into a single
    ///         <see cref="OnBallEnteredHole"/> event for <see cref="Core.GameManager"/>.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class HoleManager : MonoBehaviour
    {
        [SerializeField] private GameObject _holePrefab;

        private GameConfig _config;

        /// <summary>All active hole controllers. Populated in <see cref="SpawnHoles"/>.</summary>
        private readonly List<HoleController> _holes = new();

        /// <summary>
        /// When <c>true</c>, <see cref="RepositionHoles"/> is a no-op.
        /// Set during aiming and in-flight phases to honour the spec requirement:
        /// "Holes can change positions… but not when the player started holding down the finger."
        /// </summary>
        private bool _positionsLocked;

        /// <summary>
        /// Fired when any hole detects a ball entry.
        /// <para>Parameter: the <see cref="HoleType"/> of the entered hole.</para>
        /// <see cref="Core.GameManager"/> subscribes to this to apply timer effects.
        /// </summary>
        public event Action<HoleType> OnBallEnteredHole;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Creates all holes at random valid positions and starts their type-change timers.
        /// Safe to call multiple times — existing holes are destroyed before respawning.
        /// </summary>
        /// <param name="config">Shared game configuration.</param>
        public void Initialize(GameConfig config)
        {
            _config = config;
            SpawnHoles();
        }

        /// <summary>
        /// Prevents holes from being repositioned. Called when the player starts aiming
        /// so holes don't jump around mid-drag.
        /// </summary>
        public void LockPositions() => _positionsLocked = true;

        /// <summary>Allows holes to be repositioned again. Called when the ball is launched.</summary>
        public void UnlockPositions() => _positionsLocked = false;

        /// <summary>
        /// Stops all hole type-change timers. Called on game over so warning sounds
        /// and flash animations do not continue playing on the game-over screen.
        /// </summary>
        public void StopAllTimers()
        {
            foreach (var hole in _holes)
                hole.StopTypeChangeTimer();
        }

        /// <summary>
        /// Moves all holes to new random positions and resets their type-change timers.
        /// Does nothing if positions are locked (see <see cref="LockPositions"/>).
        /// </summary>
        public void RepositionHoles()
        {
            if (_positionsLocked) return;

            foreach (var hole in _holes)
            {
                hole.SetActive(true);                    // Re-enable holes that were deactivated by a ball entry.
                hole.MoveTo(GetRandomPosition());
                hole.StartTypeChangeTimer();             // Restart the random type-change cycle.
            }
        }

        // ── Private: Spawning ──────────────────────────────────────────────────

        private void SpawnHoles()
        {
            // Destroy any previously spawned holes to allow safe re-initialisation.
            foreach (var hole in _holes)
            {
                if (hole != null) Destroy(hole.gameObject);
            }
            _holes.Clear();

            for (int i = 0; i < _config.holeCount; i++)
            {
                var go             = Instantiate(_holePrefab, transform);
                var holeController = go.GetComponent<HoleController>();

                // Alternate starting types so the playfield has a mix of Good and Bad holes.
                var type = i % 2 == 0 ? HoleType.Good : HoleType.Bad;
                holeController.Initialize(_config, type);

                // Subscribe before MoveTo in case a position triggers immediate entry (edge case).
                holeController.OnBallEntered += HandleBallEntered;
                holeController.MoveTo(GetRandomPosition());

                // Add to the list AFTER MoveTo so that IsTooCloseToExistingHoles correctly
                // separates each newly placed hole from previously placed ones.
                _holes.Add(holeController);
            }
        }

        // ── Private: Position Logic ────────────────────────────────────────────

        /// <summary>
        /// Returns a random world-space position within the playfield boundaries that:
        /// <list type="bullet">
        ///   <item>Is not within <see cref="GameConfig.holeSafeRadiusFromBall"/> of the ball start.</item>
        ///   <item>Is not too close to any already-placed hole.</item>
        /// </list>
        /// Falls back to a safe upper-area position if no valid spot is found within
        /// <c>maxAttempts</c> iterations (prevents infinite loops on very crowded fields).
        /// </summary>
        private Vector2 GetRandomPosition()
        {
            const int maxAttempts = 30;
            var   min        = _config.playfieldMin;
            var   max        = _config.playfieldMax;
            float safeRadius = _config.holeSafeRadiusFromBall;
            var   ballStart  = _config.ballStartPosition;

            float safeRadiusSq = safeRadius * safeRadius;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Keep 1 unit of margin from each wall edge.
                var candidate = new Vector2(
                    Random.Range(min.x + 1f, max.x - 1f),
                    Random.Range(min.y + 1f, max.y - 1f)
                );

                if ((candidate - ballStart).sqrMagnitude < safeRadiusSq) continue;
                if (IsTooCloseToExistingHoles(candidate)) continue;

                return candidate;
            }

            // Fallback: use the upper half of the field to avoid the ball start area.
            return new Vector2(
                Random.Range(min.x + 1f, max.x - 1f),
                Random.Range(min.y + 2f, max.y - 1f)
            );
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="candidate"/> is closer than
        /// three hole-radii to any hole already in <see cref="_holes"/>.
        /// This spacing prevents holes from overlapping visually.
        /// </summary>
        private bool IsTooCloseToExistingHoles(Vector2 candidate)
        {
            float minDist   = _config.holeRadius * 3f;
            float minDistSq = minDist * minDist;

            foreach (var hole in _holes)
            {
                if (hole == null) continue;
                if (((Vector2)hole.transform.position - candidate).sqrMagnitude < minDistSq)
                    return true;
            }
            return false;
        }

        // ── Event Relay ────────────────────────────────────────────────────────

        /// <summary>
        /// Receives the per-hole event and re-fires it as a manager-level event,
        /// exposing only the <see cref="HoleType"/> that consumers need.
        /// This keeps <see cref="Core.GameManager"/> decoupled from individual
        /// <see cref="HoleController"/> instances.
        /// </summary>
        private void HandleBallEntered(HoleController hole)
        {
            OnBallEnteredHole?.Invoke(hole.HoleType);
        }
    }
}
