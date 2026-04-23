using System.Collections.Generic;
using MiniGolf.Config;
using UnityEngine;

namespace MiniGolf.Ball
{
    /// <summary>
    /// Renders the aim trajectory as a series of pooled dot sprites.
    /// <para>
    /// Single Responsibility: this class only draws the dots — it knows nothing
    /// about input, physics, or game state. <see cref="BallController"/> calls
    /// <see cref="UpdateTrajectory"/> or <see cref="Hide"/> each frame.
    /// </para>
    /// <para>
    /// Object pooling is used (dots are created once in <see cref="Initialize"/>
    /// and reused) to avoid per-frame allocations during aiming.
    /// </para>
    /// </summary>
    public class TrajectoryRenderer : MonoBehaviour
    {
        [SerializeField] private GameObject _dotPrefab;

        private GameConfig _config;

        /// <summary>Pool of pre-instantiated dot sprite renderers.</summary>
        private readonly List<SpriteRenderer> _dots = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the dot pool from the config and hides all dots.
        /// Must be called before <see cref="UpdateTrajectory"/> or <see cref="Hide"/>.
        /// </summary>
        /// <param name="config">Provides dot count, spacing and scale settings.</param>
        public void Initialize(GameConfig config)
        {
            _config = config;
            CreateDotPool();
            SetVisible(false);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Positions and reveals all dots along a straight line from
        /// <paramref name="ballPosition"/> in <paramref name="shootDirection"/>.
        /// <para>
        /// Dot properties that vary with index:
        /// <list type="bullet">
        ///   <item><b>Position</b> — linear spacing scaled by <paramref name="normalizedForce"/>
        ///         so the trajectory shortens at low power.</item>
        ///   <item><b>Scale</b> — decreases towards the end for a natural tapering look.</item>
        ///   <item><b>Alpha</b> — fades from 0.85 at the start to 0.10 at the end.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="ballPosition">World-space origin of the trajectory line (ball centre).</param>
        /// <param name="shootDirection">Normalised shoot direction vector.</param>
        /// <param name="normalizedForce">0–1 representing how much power the player has charged.</param>
        public void UpdateTrajectory(Vector2 ballPosition, Vector2 shootDirection, float normalizedForce)
        {
            SetVisible(true);

            for (int i = 0; i < _dots.Count; i++)
            {
                if (_dots[i] == null) continue;

                // t goes from 0 (first dot, near ball) to 1 (last dot, far end).
                float t        = (float)i / (_dots.Count - 1);
                float distance = (i + 1) * _config.trajectoryDotSpacing * normalizedForce;
                Vector2 point  = ballPosition + shootDirection * distance;

                // Place each dot slightly in front of the scene (z = -0.1) so it
                // renders above the background but below the ball (z = 0, sortOrder = 4).
                _dots[i].transform.position = new Vector3(point.x, point.y, -0.1f);

                // Taper the dot size from full to half as t approaches 1.
                float scale = _config.trajectoryDotScale * (1f - t * 0.5f);
                _dots[i].transform.localScale = Vector3.one * scale;

                // Fade alpha so distant dots are more transparent, giving a depth cue.
                float alpha = Mathf.Lerp(0.85f, 0.1f, t);
                _dots[i].color = new Color(1f, 1f, 1f, alpha);
                _dots[i].gameObject.SetActive(true);
            }
        }

        /// <summary>Hides all trajectory dots. Called on shot release or input cancel.</summary>
        public void Hide() => SetVisible(false);

        // ── Private Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Destroys any existing dots and creates a fresh pool of
        /// <see cref="GameConfig.trajectoryDotCount"/> instances as children of this transform.
        /// </summary>
        private void CreateDotPool()
        {
            foreach (var dot in _dots)
            {
                if (dot != null) Destroy(dot.gameObject);
            }
            _dots.Clear();

            for (int i = 0; i < _config.trajectoryDotCount; i++)
            {
                var go = Instantiate(_dotPrefab, transform);
                go.name = $"TrajectoryDot_{i}";
                var sr = go.GetComponent<SpriteRenderer>();
                _dots.Add(sr);
                go.SetActive(false); // Start hidden; shown only when aiming.
            }
        }

        /// <summary>Activates or deactivates every dot in the pool.</summary>
        private void SetVisible(bool visible)
        {
            foreach (var dot in _dots)
            {
                if (dot != null)
                    dot.gameObject.SetActive(visible);
            }
        }
    }
}
