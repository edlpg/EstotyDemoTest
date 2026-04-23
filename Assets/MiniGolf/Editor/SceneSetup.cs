using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MiniGolf.Editor
{
    /// <summary>
    /// One-click scene builder for the MiniGolf game.
    /// Targets a portrait 9:16 layout (camera ortho size 5 → visible world ±2.8 x, ±5 y).
    /// </summary>
    public static class SceneSetup
    {
        // ── Portrait playfield dimensions ──────────────────────────────────────
        // Camera ortho size = 5 → height = 10 units, width ≈ 5.625 units (9:16).
        private const float HalfW = 2.81f;   // half visible width
        private const float HalfH = 5.00f;   // half visible height
        private const float WallT = 0.5f;    // wall thickness

        // ── Menu item ─────────────────────────────────────────────────────────

        [MenuItem("MiniGolf/Setup Scene")]
        public static void SetupScene()
        {
            // Destroy all [MiniGolf] objects except AudioManager (edited manually).
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name.StartsWith("[MiniGolf]") && go.name != "[MiniGolf] AudioManager")
                    Object.DestroyImmediate(go);
            }

            CreateCamera();
            CreateBackground();
            CreateWalls();
            CreateBall();
            CreateHoles();
            CreateTrajectory();
            CreateGameManager();
            CreateUI();

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());

            Debug.Log("[MiniGolf] Scene setup complete — portrait layout (9:16).");
        }

        // ── Camera ────────────────────────────────────────────────────────────

        private static void CreateCamera()
        {
            var go  = new GameObject("[MiniGolf] Main Camera");
            go.tag  = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 5f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.13f, 0.13f, 0.15f);
            go.transform.position = new Vector3(0f, 0f, -10f);

            // URP camera data (added automatically by URP package).
            go.AddComponent<UniversalAdditionalCameraData>();
        }

        // ── Background ────────────────────────────────────────────────────────

        private static void CreateBackground()
        {
            var root = new GameObject("[MiniGolf] Background");

            // Dark outer frame (fills the full visible area).
            CreateQuad(root, "Outer", new Color(0.10f, 0.12f, 0.10f),
                       Vector3.zero, new Vector2(HalfW * 2f, HalfH * 2f), -2);

            // Green playfield — inset by wall thickness.
            float pw = (HalfW - WallT) * 2f;
            float ph = (HalfH - WallT) * 2f;
            CreateQuad(root, "Playfield", new Color(0.15f, 0.55f, 0.20f),
                       Vector3.zero, new Vector2(pw, ph), -1);
        }

        private static void CreateQuad(
            GameObject parent, string name, Color colour,
            Vector3 localPos, Vector2 size, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = localPos;
            go.transform.localScale    = new Vector3(size.x, size.y, 1f);

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite       = GetWhiteSquareSprite();
            sr.color        = colour;
            sr.sortingOrder = sortOrder;
        }

        private static Sprite _whiteSquare;

        private static Sprite GetWhiteSquareSprite()
        {
            if (_whiteSquare != null) return _whiteSquare;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _whiteSquare = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _whiteSquare;
        }

        // ── Walls ─────────────────────────────────────────────────────────────

        private static void CreateWalls()
        {
            var root = new GameObject("[MiniGolf] Walls");

            var mat = new PhysicsMaterial2D("WallMat") { bounciness = 0.3f, friction = 0f };

            // Left / Right
            AddWall(root, "Left",   new Vector2(-HalfW + WallT * 0.5f, 0f),
                    new Vector2(WallT, HalfH * 2f), mat);
            AddWall(root, "Right",  new Vector2( HalfW - WallT * 0.5f, 0f),
                    new Vector2(WallT, HalfH * 2f), mat);
            // Top / Bottom
            AddWall(root, "Top",    new Vector2(0f,  HalfH - WallT * 0.5f),
                    new Vector2(HalfW * 2f, WallT), mat);
            AddWall(root, "Bottom", new Vector2(0f, -HalfH + WallT * 0.5f),
                    new Vector2(HalfW * 2f, WallT), mat);
        }

        private static void AddWall(
            GameObject parent, string name, Vector2 pos, Vector2 size, PhysicsMaterial2D mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = pos;

            var col          = go.AddComponent<BoxCollider2D>();
            col.size         = size;
            col.sharedMaterial = mat;
        }

        // ── Ball ──────────────────────────────────────────────────────────────

        private static void CreateBall()
        {
            var go  = new GameObject("[MiniGolf] Ball");
            go.tag  = "Ball";
            go.transform.position = new Vector3(0f, -3.8f, 0f);

            // Sprite
            var sr     = go.AddComponent<SpriteRenderer>();
            sr.sprite  = LoadOrGenerateCircleSprite();
            sr.color   = new Color(0.2f, 0.85f, 0.35f);
            sr.sortingOrder = 2;
            go.transform.localScale = Vector3.one * 0.42f;

            // Physics
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale           = 0f;
            rb.linearDamping          = 2.0f;
            rb.angularDamping         = 1.0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints            = RigidbodyConstraints2D.FreezeRotation;

            var mat        = new PhysicsMaterial2D("BallMat") { bounciness = 0.3f, friction = 0.1f };
            var col        = go.AddComponent<CircleCollider2D>();
            col.radius     = 0.5f;
            col.sharedMaterial = mat;
        }

        // ── Holes ─────────────────────────────────────────────────────────────

        private static void CreateHoles()
        {
            var root = new GameObject("[MiniGolf] Holes");

            // HoleManager will be wired manually or via GameManager.
            root.AddComponent<MiniGolf.Hole.HoleManager>();
        }

        // ── Trajectory ────────────────────────────────────────────────────────

        private static void CreateTrajectory()
        {
            var go = new GameObject("[MiniGolf] Trajectory");
            go.AddComponent<MiniGolf.Ball.TrajectoryRenderer>();
        }


        // ── Game Manager ──────────────────────────────────────────────────────

        private static void CreateGameManager()
        {
            var go = new GameObject("[MiniGolf] GameManager");
            go.AddComponent<MiniGolf.Core.GameManager>();
            go.AddComponent<MiniGolf.Services.TimerService>();
        }

        // ── UI ────────────────────────────────────────────────────────────────

        private static void CreateUI()
        {
            // Canvas
            var canvasGo = new GameObject("[MiniGolf] UI");
            var canvas   = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var cs = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            cs.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1080, 1920); // Portrait 9:16
            cs.screenMatchMode     = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight  = 1f; // Match height for portrait

            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Timer Panel (top of screen)
            var timerPanel = new GameObject("TimerPanel");
            timerPanel.transform.SetParent(canvasGo.transform, false);
            var timerRect        = timerPanel.AddComponent<RectTransform>();
            timerRect.anchorMin  = new Vector2(0, 0.80f);
            timerRect.anchorMax  = new Vector2(1, 0.92f);
            timerRect.offsetMin  = Vector2.zero;
            timerRect.offsetMax  = Vector2.zero;

            timerPanel.AddComponent<MiniGolf.UI.TimerDisplay>();

            // Game Over Panel (full screen, starts hidden)
            var gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(canvasGo.transform, false);
            var goRect       = gameOverPanel.AddComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.offsetMin = Vector2.zero;
            goRect.offsetMax = Vector2.zero;

            gameOverPanel.AddComponent<MiniGolf.UI.GameOverPanel>();
            gameOverPanel.SetActive(false);
        }

        // ── Sprite Helpers ────────────────────────────────────────────────────

        private static Sprite LoadOrGenerateCircleSprite()
        {
            const string path = "Assets/MiniGolf/Sprites/Circle.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            // Generate a 128×128 white circle texture and save as PNG.
            const int size = 128;
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - half, dy = y - half;
                tex.SetPixel(x, y,
                    dx * dx + dy * dy <= half * half
                        ? Color.white
                        : Color.clear);
            }
            tex.Apply();

            System.IO.Directory.CreateDirectory("Assets/MiniGolf/Sprites");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType   = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
