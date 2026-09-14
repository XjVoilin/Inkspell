using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Renders the actual tier sprites at card scale without tier labels or tier frames.</summary>
    public static class SpellTierPresentationPreview
    {
        private const int PreviewLayer = 31;
        private static readonly Color Ink = new(.14f, .11f, .10f);

        public static void BuildAndCapture()
        {
            SpellPresentationAuthoring.Apply();
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D capture = null;
            try
            {
                var cameraObject = new GameObject("SpellTierCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.transform.position = new Vector3(0, 0, -20);
                camera.orthographic = true;
                camera.orthographicSize = 960;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.92f, .84f, .68f);
                camera.cullingMask = 1 << PreviewLayer;
                camera.enabled = false;

                target = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;

                var canvasObject = new GameObject("SpellTierPresentation", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 1920);

                Picture("Paper", canvas.transform, LoadMain("bg_battlefieldPaper"), Vector2.zero, new Vector2(1080, 1920));
                Label(canvas.transform, "SPELL SILHOUETTE / THREE TIERS", new Vector2(0, 840), new Vector2(980, 70), 42);
                Label(canvas.transform, "CARD-SCALE READABILITY · NO TIER TEXT · NO TIER FRAME", new Vector2(0, 784), new Vector2(980, 44), 22);

                var columns = new[] {"TIER I", "TIER II", "TIER III"};
                var columnX = new[] {-280f, 0f, 280f};
                for (var i = 0; i < 3; i++)
                    Label(canvas.transform, columns[i], new Vector2(columnX[i], 706), new Vector2(230, 42), 24);

                var spells = new[]
                {
                    new SpellRow("FIREBALL", "FireballSample/icon_fireballTier"),
                    new SpellRow("CHAIN LIGHTNING", "SpellTierPresentation/icon_chainLightningTier"),
                    new SpellRow("FROST RING", "SpellTierPresentation/icon_frostRingTier"),
                    new SpellRow("RUNE SHIELD", "SpellTierPresentation/icon_runeShieldTier"),
                };
                for (var row = 0; row < spells.Length; row++)
                {
                    var y = 485 - row * 370;
                    var panel = Picture("Row", canvas.transform, null, new Vector2(0, y), new Vector2(980, 330));
                    panel.color = new Color(.20f, .12f, .08f, row % 2 == 0 ? .08f : .04f);
                    Label(panel.transform, spells[row].Name, new Vector2(-390, 124), new Vector2(280, 38), 23);
                    for (var tier = 1; tier <= 3; tier++)
                    {
                        var card = Rect($"{spells[row].Name}Tier{tier}", panel.transform, new Vector2(columnX[tier - 1], -5), new Vector2(214, 244));
                        Picture("Base", card, LoadMain("tile_spellSlotBase"), Vector2.zero, new Vector2(214, 214));
                        Picture("Icon", card, LoadTier(spells[row].Prefix, tier), new Vector2(0, 4), new Vector2(168, 168));
                        Label(card, "1", new Vector2(72, -80), new Vector2(44, 40), 25);
                    }
                }
                Label(canvas.transform, "TIER IS READ FROM THE SPELL SHAPE", new Vector2(0, -874), new Vector2(980, 45), 22);

                foreach (var child in canvasObject.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = PreviewLayer;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                var resolved = RenderTexture.GetTemporary(1080, 1920, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(target, resolved);
                var previous = RenderTexture.active;
                RenderTexture.active = resolved;
                capture = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
                capture.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(resolved);
                var bytes = capture.EncodeToPNG();
                Save(bytes, "Design/AIArt/ArtSource/SpellTierPresentation/SpellTierPresentation_preview.png");
                Save(bytes, "outputs/spell-tier-presentation/SpellTierPresentation.png");
                AssetDatabase.SaveAssets();
                Debug.Log("[SpellTierPresentation] Four-spell three-tier preview captured.");
            }
            finally
            {
                if (capture != null) Object.DestroyImmediate(capture);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private readonly struct SpellRow
        {
            internal readonly string Name;
            internal readonly string Prefix;
            internal SpellRow(string name, string prefix) { Name = name; Prefix = prefix; }
        }

        private static Sprite LoadMain(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Arts/Textures/MainWindow/" + name + ".png");

        private static Sprite LoadTier(string prefix, int tier) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Game/Arts/Textures/{prefix}{tier}.png");

        private static void Save(byte[] bytes, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var label = Rect("Label", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Arts/Fonts/LXGW WenKai SDF.asset");
            label.text = text;
            label.fontSize = fontSize;
            label.color = Ink;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
    }
}
