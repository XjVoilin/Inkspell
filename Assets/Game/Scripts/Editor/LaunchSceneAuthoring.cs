using System;
using System.IO;
using System.Linq;
using Game.Aot;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Authors a bundled Canvas, including its startup strings, directly into Launch.</summary>
    public static class LaunchSceneAuthoring
    {
        private const string ScenePath = "Assets/Game/Scenes/Launch.unity";
        private const string Art = "Assets/Game/Arts/Textures/MainWindow/";
        private const string Preview = "Design/AIArt/ArtSource/Launch/Launch_preview.png";
        [Serializable] private class Languages { public Language[] rows; }
        [Serializable] private class Language { public string Key; public string CN; }
        private static Language[] _language;
        private static TMP_FontAsset _font;
        private static readonly Color Ink = new Color(.16f, .17f, .19f);
        private static readonly Color Gold = new Color(.60f, .43f, .20f);

        [MenuItem("July/Inkspell/Rebuild Launch Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var entry = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GameEntry>()).Single();
            var old = entry.transform.Find("LaunchPresentation");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            _language = JsonUtility.FromJson<Languages>("{\"rows\":" + File.ReadAllText("Assets/Game/Res/Configs/tblanguage.json") + "}").rows;
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Arts/Fonts/LXGW WenKai SDF.asset");
            var root = Rect("LaunchPresentation", entry.transform, 0, 0, 1080, 1920);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<GraphicRaycaster>();
            var view = root.gameObject.AddComponent<LaunchPresentation>();
            var backdropCamera = new GameObject("LaunchCamera", typeof(Camera)).GetComponent<Camera>();
            backdropCamera.transform.SetParent(root, false);
            backdropCamera.clearFlags = CameraClearFlags.SolidColor;
            backdropCamera.backgroundColor = new Color(.93f, .87f, .73f);
            backdropCamera.cullingMask = 0;
            backdropCamera.depth = -100;
            backdropCamera.orthographic = true;

            var paper = Picture("Paper", root, "bg_paperWarm", 0, 0, 1080, 1920);
            paper.preserveAspect = false;
            paper.raycastTarget = true;
            paper.rectTransform.anchorMin = Vector2.zero;
            paper.rectTransform.anchorMax = Vector2.one;
            paper.rectTransform.sizeDelta = Vector2.zero;
            Label("Title", root, L("LAUNCH_TITLE"), 0, 525, 880, 135, 100, Ink);
            Label("EnglishTitle", root, "I N K S P E L L", 0, 414, 800, 65, 34, Gold);
            Rule("TitleRuleLeft", root, -174, 330, 230, 2, Gold);
            Rule("TitleRuleRight", root, 174, 330, 230, 2, Gold);
            var seal = Rule("TitleSeal", root, 0, 330, 12, 12, Gold);
            seal.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var halo = Picture("InkHalo", root, "img_spellSlotEmptyMark", 0, -5, 670, 670);
            halo.color = new Color(.72f, .51f, .19f, .20f);
            var book = Picture("LivingSpellbook", root, "img_livingSpellbook", 0, -20, 460, 535);
            var motes = new RectTransform[6];
            var positions = new[] {new Vector2(-275, 115), new Vector2(260, 210), new Vector2(-245, -165), new Vector2(270, -130), new Vector2(-110, 270), new Vector2(145, -295)};
            for (var i = 0; i < motes.Length; i++)
            {
                var size = i % 2 == 0 ? 9 : 6;
                motes[i] = Rule("InkMote" + i, root, positions[i].x, positions[i].y, size, size, i % 2 == 0 ? Gold : Ink).rectTransform;
                motes[i].localRotation = Quaternion.Euler(0, 0, 45);
            }
            var track = Picture("ProgressTrack", root, "img_progressTrack", 0, -515, 480, 14);
            track.preserveAspect = false;
            track.color = new Color(1, 1, 1, .4f);
            var progress = Picture("ProgressFill", root, "img_progressFill", 0, -515, 480, 14);
            progress.preserveAspect = false;
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillAmount = 0;
            var status = Label("Status", root, L("LAUNCH_AWAKEN"), 0, -594, 880, 100, 30, Ink);

            var bindings = new SerializedObject(view);
            bindings.FindProperty("_group").objectReferenceValue = group;
            bindings.FindProperty("_book").objectReferenceValue = book.rectTransform;
            bindings.FindProperty("_halo").objectReferenceValue = halo;
            bindings.FindProperty("_progress").objectReferenceValue = progress;
            bindings.FindProperty("_status").objectReferenceValue = status;
            var particles = bindings.FindProperty("_motes");
            particles.arraySize = motes.Length;
            for (var i = 0; i < motes.Length; i++) particles.GetArrayElementAtIndex(i).objectReferenceValue = motes[i];
            var stages = bindings.FindProperty("_stages");
            var keys = new[] {"LAUNCH_AWAKEN", "LAUNCH_RESOURCES", "LAUNCH_UPDATE", "LAUNCH_SYSTEMS", "LAUNCH_ENTER"};
            stages.arraySize = keys.Length;
            for (var i = 0; i < keys.Length; i++) stages.GetArrayElementAtIndex(i).stringValue = L(keys[i]);
            bindings.FindProperty("_failure").stringValue = L("LAUNCH_FAILURE");
            bindings.ApplyModifiedPropertiesWithoutUndo();
            var entryBindings = new SerializedObject(entry);
            entryBindings.FindProperty("_presentation").objectReferenceValue = view;
            entryBindings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RenderPreview(canvas);
            Debug.Log("[LaunchSceneAuthoring] PASS: saved bundled startup Canvas and rendered preview.");
        }

        private static string L(string key) => _language.Single(row => row.Key == key).CN;
        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }
        private static Image Rule(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        private static Image Picture(string name, Transform parent, string asset, float x, float y, float w, float h)
        {
            var image = Rule(name, parent, x, y, w, h, Color.white);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + asset + ".png");
            if (image.sprite == null) throw new InvalidOperationException("Missing launch art: " + asset);
            image.preserveAspect = true;
            return image;
        }
        private static TMP_Text Label(string name, Transform parent, string value, float x, float y, float w, float h, int size, Color color)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.enableWordWrapping = false;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
        private static void RenderPreview(Canvas canvas)
        {
            var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.93f, .87f, .73f);
            var target = new RenderTexture(1080, 1920, 24);
            var previous = RenderTexture.active;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            canvas.scaleFactor = 1;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1080, 1920, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Preview));
            File.WriteAllBytes(Preview, image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            // Reload the saved overlay scene so preview-only settings never leak into editing.
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
