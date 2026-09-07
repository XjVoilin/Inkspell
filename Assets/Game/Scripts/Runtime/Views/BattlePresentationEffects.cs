using System.Collections.Generic;
using cfg;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>Bounded UI effect pool. Presentation never advances or re-rolls battle simulation.</summary>
    internal sealed class BattlePresentationEffects
    {
        private sealed class Effect
        {
            internal Image Image;
            internal Image[] Lightning;
            internal Vector2 From, To;
            internal SpellType Type;
            internal bool Impact;
            internal float Age, Duration;
        }
        private readonly RectTransform _root;
        private readonly List<Effect> _pool = new();
        private int _recycle;

        internal BattlePresentationEffects(RectTransform root) { _root = root; }

        internal void Play(SpellType type, Sprite sprite, Vector2 from, Vector2 to, bool impact, float travel)
        {
            var effect = _pool.Find(e => !e.Image.gameObject.activeSelf);
            if (effect == null && _pool.Count < 24)
            {
                var image = CreateImage("SpellMotion", _root);
                effect = new Effect {Image = image, Lightning = new Image[7]};
                for (var i = 0; i < effect.Lightning.Length; i++)
                    effect.Lightning[i] = CreateImage("LightningSegment", image.rectTransform);
                _pool.Add(effect);
            }
            effect ??= _pool[_recycle++ % _pool.Count];
            effect.Type = type;
            effect.From = from;
            effect.To = type == SpellType.Shield ? from : to;
            effect.Impact = impact;
            effect.Age = 0;
            effect.Duration = impact ? .38f : Mathf.Max(.08f, travel);
            effect.Image.sprite = sprite;
            effect.Image.gameObject.SetActive(true);
            effect.Image.transform.SetAsLastSibling();
            Render(effect);
        }

        internal void Tick(float deltaTime)
        {
            foreach (var e in _pool)
            {
                if (!e.Image.gameObject.activeSelf) continue;
                e.Age += deltaTime;
                if (e.Age >= e.Duration) e.Image.gameObject.SetActive(false);
                else Render(e);
            }
        }

        internal void Clear()
        { foreach (var e in _pool) e.Image.gameObject.SetActive(false); }

        private static Image CreateImage(string name, RectTransform parent)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static void Render(Effect e)
        {
            var t = Mathf.Clamp01(e.Age / e.Duration);
            var rt = e.Image.rectTransform;
            var lightning = e.Type == SpellType.ChainLightning && !e.Impact;
            foreach (var line in e.Lightning) line.gameObject.SetActive(lightning);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            e.Image.color = new Color(1, 1, 1, e.Impact ? 1 - t : 1);
            if (lightning)
            {
                rt.anchoredPosition = e.From;
                e.Image.color = Color.clear;
                var delta = e.To - e.From;
                var normal = new Vector2(-delta.y, delta.x).normalized;
                var previous = Vector2.zero;
                for (var i = 0; i < e.Lightning.Length; i++)
                {
                    var next = delta * ((i + 1f) / e.Lightning.Length);
                    if (i < e.Lightning.Length - 1) next += normal * Mathf.Sin(i * 2.4f + e.Age * 55) * 22;
                    var segment = next - previous;
                    var line = e.Lightning[i];
                    line.rectTransform.anchoredPosition = (previous + next) * .5f;
                    line.rectTransform.sizeDelta = new Vector2(segment.magnitude, 5 + 2 * Mathf.Sin(t * 25));
                    line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
                    line.color = new Color(.65f, .84f, 1, .85f);
                    previous = next;
                }
            }
            else if (e.Type == SpellType.Fireball && !e.Impact)
            {
                rt.anchoredPosition = Vector2.Lerp(e.From, e.To, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 65);
                rt.sizeDelta = new Vector2(72, 72);
                rt.localRotation = Quaternion.Euler(0, 0, -t * 180);
            }
            else
            {
                rt.anchoredPosition = e.To;
                var size = e.Type == SpellType.Shield ? 200 : e.Type == SpellType.FrostRing ? 230 : 120;
                rt.sizeDelta = Vector2.one * size * Mathf.Lerp(e.Impact ? .65f : .25f, 1.25f, t);
                if (e.Type == SpellType.FrostRing) rt.localRotation = Quaternion.Euler(0, 0, t * 90);
                if (!e.Impact) e.Image.color = new Color(1, 1, 1, .45f + .4f * t);
            }
        }
    }
}
