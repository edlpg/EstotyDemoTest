using UnityEngine;

namespace MiniGolf.Audio
{
    /// <summary>
    /// Singleton responsible for all audio playback in the game.
    /// <para>
    /// Exposes one <c>PlayXxx()</c> method per sound event. Every method is
    /// null-safe — if a clip has not been assigned in the Inspector, the call
    /// is silently ignored, so the game never throws on missing audio.
    /// </para>
    /// <para>
    /// Audio clips are intentionally left unassigned so free Asset Store sounds
    /// can be dropped in without touching code.
    /// </para>
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Sources")]

        /// <summary>Source used for all one-shot sound effects.</summary>
        [SerializeField] private AudioSource _sfxSource;

        /// <summary>Source used for background music (looping).</summary>
        [SerializeField] private AudioSource _bgmSource;

        [Header("Sound Effects")]

        /// <summary>Played when the ball is launched.</summary>
        public AudioClip shootClip;

        /// <summary>Played when the ball enters a Good (blue) hole.</summary>
        public AudioClip goodHoleClip;

        /// <summary>Played when the ball enters a Bad (red) hole.</summary>
        public AudioClip badHoleClip;

        /// <summary>Played when the ball stops without entering any hole.</summary>
        public AudioClip missClip;

        /// <summary>Played when a hole begins its pre-change flash warning.</summary>
        public AudioClip warningClip;

        /// <summary>Played once when the countdown timer reaches zero.</summary>
        public AudioClip gameOverClip;

        /// <summary>Reserved for a low-time countdown tick (not yet triggered by code).</summary>
        public AudioClip countdownClip;

        /// <summary>Global singleton reference. Set in <c>Awake</c>; destroyed on duplicates.</summary>
        public static AudioManager Instance { get; private set; }

        private void Awake()
        {
            // Enforce a single instance. Any duplicate (e.g. from scene reload) is destroyed.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Plays the shoot sound effect.</summary>
        public void PlayShoot() => TryPlay(shootClip);

        /// <summary>Plays the good-hole reward sound.</summary>
        public void PlayGoodHole() => TryPlay(goodHoleClip);

        /// <summary>Plays the bad-hole penalty sound.</summary>
        public void PlayBadHole() => TryPlay(badHoleClip);

        /// <summary>Plays the missed-shot sound.</summary>
        public void PlayMiss() => TryPlay(missClip);

        /// <summary>Plays the hole-about-to-change warning sound.</summary>
        public void PlayWarning() => TryPlay(warningClip);

        /// <summary>Plays the game-over sound.</summary>
        public void PlayGameOver() => TryPlay(gameOverClip);

        /// <summary>Plays the low-time countdown tick.</summary>
        public void PlayCountdown() => TryPlay(countdownClip);

        // ── Private Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Plays <paramref name="clip"/> as a one-shot if both the clip and
        /// the sfx source are assigned. Prevents null-reference exceptions when
        /// audio assets have not yet been imported.
        /// </summary>
        private void TryPlay(AudioClip clip)
        {
            if (clip != null && _sfxSource != null)
                _sfxSource.PlayOneShot(clip);
        }
    }
}
