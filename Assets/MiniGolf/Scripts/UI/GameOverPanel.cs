using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiniGolf.UI
{
    /// <summary>
    /// Manages the game-over overlay shown when the countdown reaches zero.
    /// <para>
    /// <b>Lifecycle note:</b> The root GameObject (<see cref="_panelRoot"/>) starts
    /// <em>disabled</em> in the scene. Unity therefore defers <c>Awake</c> until the
    /// first time <see cref="Show"/> calls <c>SetActive(true)</c>. This means the
    /// button listener is registered lazily — only once, exactly when needed —
    /// without requiring any special initialisation order in <see cref="Core.GameManager"/>.
    /// </para>
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        /// <summary>
        /// Root GameObject toggled by <see cref="Show"/> and <see cref="Hide"/>.
        /// Self-referential (the component sits on this same object) but semantically
        /// clear — it makes the intent of every call obvious.
        /// </summary>
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TMP_Text   _gameOverText;
        [SerializeField] private Button     _restartButton;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        /// <summary>
        /// Called the first time this GameObject becomes active (i.e. on the first
        /// <see cref="Show"/> call). Registers the restart button listener once.
        /// </summary>
        private void Awake()
        {
            _restartButton?.onClick.AddListener(OnRestartClicked);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Activates the panel and sets the headline text.
        /// Triggers <c>Awake</c> on first call (see class summary).
        /// </summary>
        public void Show(int finalScore)
        {
            _panelRoot.SetActive(true);

            if (_gameOverText != null)
                _gameOverText.text = $"TIME'S UP!\n<size=55%><color=#FFD700>Gold collected: {finalScore}</color></size>";
        }

        /// <summary>Deactivates the panel. Safe to call even when already hidden.</summary>
        public void Hide() => _panelRoot.SetActive(false);

        // ── Private: Button Handler ────────────────────────────────────────────

        /// <summary>
        /// Reloads the active scene by name, effectively restarting the game.
        /// </summary>
        private void OnRestartClicked() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
