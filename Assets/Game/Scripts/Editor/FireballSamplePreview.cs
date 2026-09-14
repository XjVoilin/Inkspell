using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Isolated, replayable comparison using the actual fireball presentation primitives.</summary>
    public sealed class FireballSamplePreview : EditorWindow
    {
        private Scene _scene;
        private Camera _camera;
        private Canvas _canvas;
        private RenderTexture _texture;
        private readonly Image[] _cores = new Image[3];
        private readonly Sprite[] _tierSprites = new Sprite[3];
        private Sprite _impactSprite;
        private readonly FireballEffectGraphic[] _effects = new FireballEffectGraphic[3];
        private readonly Image[] _targets = new Image[3];
        private readonly TMP_Text[] _phaseLabels = new TMP_Text[3];
        private double _start;
        private bool _playing = true;
        private float _time;
        private const float Travel = .2f;
        private static readonly Vector2 From = new(-180, 0);
        private static readonly Vector2 To = new(285, 0);
        private static readonly Color Dark = new(.16f, .12f, .11f);

        [MenuItem("July/Inkspell/Fireball Three Tier Preview")]
        public static void Open() => GetWindow<FireballSamplePreview>("Fireball I / II / III").Show();

        // CI entry point: persists bindings and renders still + runtime animation frames.
        public static void BuildAndCapture()
        {
            SpellPresentationAuthoring.Apply();
            var window = CreateInstance<FireballSamplePreview>();
            try
            {
                window.EnsureScene();
                var directory = "outputs/fireball-sample";
                Directory.CreateDirectory(directory);
                Directory.CreateDirectory(directory + "/frames");
                for (var i = 0; i < 72; i++)
                {
                    window.RenderAt(i / 30f);
                    window.Save(directory + "/frames/" + i.ToString("D3") + ".png", 540, 960);
                }
                window.RenderAt(.24f);
                window.Save(directory + "/FireballThreeTiers.png", 1080, 1920);
                Directory.CreateDirectory("Design/AIArt/ArtSource/FireballSample");
                window.Save("Design/AIArt/ArtSource/FireballSample/FireballSample_preview.png", 1080, 1920);
                window.RenderAt(.58f);
                window.Save(directory + "/FireballImpact.png", 1080, 1920);
                AssetDatabase.SaveAssets();
                Debug.Log("[FireballSample] Prefab bindings and three-tier comparison captured.");
            }
            finally { DestroyImmediate(window); }
        }

        private void OnEnable() { _start = EditorApplication.timeSinceStartup; EditorApplication.update += Animate; }
        private void OnDisable()
        {
            EditorApplication.update -= Animate;
            if (_texture != null) { _texture.Release(); DestroyImmediate(_texture); }
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _camera = null; _canvas = null;
        }
        private void Animate()
        {
            if (_playing) { _time = (float)(EditorApplication.timeSinceStartup - _start) % 2.4f; Repaint(); }
        }
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _playing = GUILayout.Toggle(_playing, "Play", EditorStyles.toolbarButton, GUILayout.Width(60));
                if (GUILayout.Button("Replay", EditorStyles.toolbarButton, GUILayout.Width(65))) { _time = 0; _start = EditorApplication.timeSinceStartup; }
                var time = GUILayout.HorizontalSlider(_time, 0, 2.4f);
                if (Mathf.Abs(time - _time) > .01f) { _time = time; _playing = false; }
            }
            EnsureScene(); RenderAt(_time);
            var area = new Rect(0, 24, position.width, position.height - 24);
            GUI.DrawTexture(area, _texture, ScaleMode.ScaleToFit, false);
        }

        private void EnsureScene()
        {
            if (_camera != null) return;
            _scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("FireballComparisonCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, _scene);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.scene = _scene;
            _camera.transform.position = new Vector3(0, 0, -20);
            _camera.orthographic = true; _camera.orthographicSize = 960;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(.93f, .86f, .70f);
            _camera.cullingMask = 1 << 31;
            _camera.enabled = false;
            _texture = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            _texture.Create(); _camera.targetTexture = _texture;
            var canvasObject = new GameObject("FireballComparison", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, _scene);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _camera;
            _impactSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Arts/Textures/FireballSample/img_fireballImpact.png");
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1080, 1920);
            Picture("Paper", _canvas.transform, "bg_battlefieldPaper", Vector2.zero, new Vector2(1080, 1920), false);
            Label(_canvas.transform, "FIREBALL / TIER STUDY", new Vector2(0, 825), new Vector2(960, 80), 46);
            Label(_canvas.transform, "SAME TIMING  /  0.5x SLOW MOTION", new Vector2(0, 758), new Vector2(960, 50), 25);
            var names = new[] { "I / EMBER", "II / ORBIT", "III / CROWN" };
            var details = new[] { "SINGLE FLAME  /  SHORT TRAIL", "FLAME ORBIT  /  IMPACT RING", "FIRE CROWN  /  TWIN TRAILS" };
            for (var i = 0; i < 3; i++)
            {
                var y = 500 - i * 470;
                var row = Rect("Tier" + (i + 1), _canvas.transform, new Vector2(0, y), new Vector2(960, 440));
                Label(row, names[i], new Vector2(-215, 165), new Vector2(490, 55), 36);
                _phaseLabels[i] = Label(row, "CAST", new Vector2(300, 165), new Vector2(200, 50), 24);
                Picture("Tile", row, "tile_spellSlotBase", new Vector2(-365, 8), new Vector2(174, 174));
                var icon = Picture("Icon", row, "", new Vector2(-365, 8), new Vector2(140, 140));
                icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Game/Arts/Textures/FireballSample/icon_fireballTier{i + 1}.png");
                _tierSprites[i] = icon.sprite;
                _targets[i] = Picture("Target", row, "img_enemyInkling", To, new Vector2(124, 140));
                var effectsRect = Rect("Brushwork", row, Vector2.zero, new Vector2(960, 440));
                effectsRect.gameObject.AddComponent<CanvasRenderer>();
                _effects[i] = effectsRect.gameObject.AddComponent<FireballEffectGraphic>();
                _effects[i].raycastTarget = false;
                _cores[i] = Picture("Core", row, "", From, Vector2.one * 80);
                _cores[i].sprite = icon.sprite;
                Label(row, details[i], new Vector2(0, -142), new Vector2(940, 55), 23);
                var line = Picture("Divider", row, "", new Vector2(0, -213), new Vector2(920, 2));
                line.color = new Color(.38f, .26f, .12f, .3f);
            }
            Label(_canvas.transform, "RUNTIME EFFECTS  /  SINGLE TARGET  /  3 TIERS", new Vector2(0, -867), new Vector2(990, 55), 24);
            foreach (var child in canvasObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        }

        private void RenderAt(float seconds)
        {
            EnsureScene();
            seconds *= .5f;
            for (var i = 0; i < 3; i++)
            {
                var impact = seconds >= Travel;
                var age = impact ? seconds - Travel : seconds;
                var duration = impact ? .48f + .1f * (i + 1) : Travel;
                var active = age < duration;
                _effects[i].gameObject.SetActive(active);
                _cores[i].gameObject.SetActive(active);
                _cores[i].sprite = impact ? _impactSprite : _tierSprites[i];
                _effects[i].SetFrame(From, To, i + 1, impact, age, duration);
                FireballEffectGraphic.RenderCore(_cores[i], From, To, i + 1, impact, age / duration);
                var hit = impact ? Mathf.Clamp01(1 - age / .2f) : 0;
                _targets[i].rectTransform.localScale = new Vector3(1 + .1f * hit, 1 - .07f * hit, 1);
                _targets[i].color = Color.Lerp(Color.white, new Color(1, .5f, .2f), hit);
                _phaseLabels[i].text = !active ? "READY" : impact ? "IMPACT" : seconds < Travel * .13f ? "CAST" : "FLIGHT";
            }
            Canvas.ForceUpdateCanvases();
            _camera.Render();
        }

        private void Save(string path, int width, int height)
        {
            var previous = RenderTexture.active;
            var scaled = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(_texture, scaled);
            RenderTexture.active = scaled;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            DestroyImmediate(image); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(scaled);
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private static Image Picture(string name, Transform parent, string asset, Vector2 position, Vector2 size, bool preserve = true)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.sprite = string.IsNullOrEmpty(asset) ? null : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Arts/Textures/MainWindow/" + asset + ".png");
            image.preserveAspect = preserve; image.raycastTarget = false;
            return image;
        }
        private static TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var label = Rect("Label", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Arts/Fonts/LXGW WenKai SDF.asset");
            label.text = text; label.fontSize = fontSize; label.color = Dark;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            return label;
        }
    }
}
