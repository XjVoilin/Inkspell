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
            internal FireballEffectGraphic Fire;
            internal int Tier;
            internal Vector2 From, To;
            internal SpellType Type;
            internal bool Impact;
            internal float Age, Duration;
        }
        private readonly RectTransform _root;
        private readonly List<Effect> _pool = new();
        private int _recycle;

        internal BattlePresentationEffects(RectTransform root) { _root = root; }

        internal void Play(SpellType type, Sprite sprite, Vector2 from, Vector2 to, bool impact, float travel, int tier = 1)
        {
            var effect = _pool.Find(e => !e.Image.gameObject.activeSelf);
            if (effect == null && _pool.Count < 24)
            {
                var image = CreateImage("SpellMotion", _root);
                effect = new Effect {Image = image, Lightning = new Image[15]};
                for (var i = 0; i < effect.Lightning.Length; i++)
                    effect.Lightning[i] = CreateImage("LightningSegment", image.rectTransform);
                _pool.Add(effect);
            }
            effect ??= _pool[_recycle++ % _pool.Count];
            effect.Type = type;
            effect.Tier = Mathf.Clamp(tier, 1, 3);
            effect.From = from;
            effect.To = type == SpellType.Shield ? from : to;
            effect.Impact = impact;
            effect.Age = 0;
            effect.Duration = impact ? (type == SpellType.Fireball ? .48f + .10f * effect.Tier : .38f) : Mathf.Max(.08f, travel);
            effect.Image.sprite = sprite;
            effect.Image.gameObject.SetActive(true);
            if (type == SpellType.Fireball && effect.Fire == null)
            {
                var go = new GameObject("FireballBrushwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(FireballEffectGraphic));
                var rect = (RectTransform)go.transform;
                rect.SetParent(_root, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                effect.Fire = go.GetComponent<FireballEffectGraphic>();
                effect.Fire.raycastTarget = false;
            }
            if (effect.Fire != null)
            {
                effect.Fire.gameObject.SetActive(type == SpellType.Fireball);
                effect.Fire.transform.SetAsLastSibling();
            }
            effect.Image.transform.SetAsLastSibling();
            Render(effect);
        }

        internal void Tick(float deltaTime)
        {
            foreach (var e in _pool)
            {
                if (!e.Image.gameObject.activeSelf) continue;
                e.Age += deltaTime;
                if (e.Age >= e.Duration)
                {
                    e.Image.gameObject.SetActive(false);
                    if (e.Fire != null) e.Fire.gameObject.SetActive(false);
                }
                else Render(e);
            }
        }

        internal void Clear()
        {
            foreach (var e in _pool)
            {
                e.Image.gameObject.SetActive(false);
                if (e.Fire != null) e.Fire.gameObject.SetActive(false);
            }
        }

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
            foreach (var line in e.Lightning) line.gameObject.SetActive(false);
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
                var mainSegments = 5 + e.Tier * 2;
                for (var i = 0; i < mainSegments; i++)
                {
                    var next = delta * ((i + 1f) / mainSegments);
                    if (i < mainSegments - 1) next += normal * Mathf.Sin(i * 2.4f + e.Age * 55) * (14 + 7 * e.Tier);
                    var segment = next - previous;
                    var line = e.Lightning[i];
                    line.gameObject.SetActive(true);
                    line.rectTransform.anchoredPosition = (previous + next) * .5f;
                    line.rectTransform.sizeDelta = new Vector2(segment.magnitude, 3 + e.Tier * 2 + 2 * Mathf.Sin(t * 25));
                    line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
                    line.color = Color.Lerp(new Color(.35f, .55f, 1, .88f), Color.white, .2f * e.Tier);
                    previous = next;
                }
                // High tiers show harmless side forks; actual chain targets still come from combat facts.
                for (var branch = 0; branch < e.Tier - 1; branch++)
                {
                    var line = e.Lightning[mainSegments + branch];
                    line.gameObject.SetActive(true);
                    var root = delta * (.45f + branch * .22f);
                    var direction = (delta.normalized + normal * (branch == 0 ? .55f : -.55f)).normalized;
                    var segment = direction * (42 + 18 * e.Tier);
                    line.rectTransform.anchoredPosition = root + segment * .5f;
                    line.rectTransform.sizeDelta = new Vector2(segment.magnitude, 2 + e.Tier);
                    line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
                    line.color = new Color(.55f, .72f, 1, .75f);
                }
            }
            else if (e.Type == SpellType.Fireball)
            {
                e.Fire.SetFrame(e.From, e.To, e.Tier, e.Impact, e.Age, e.Duration);
                FireballEffectGraphic.RenderCore(e.Image, e.From, e.To, e.Tier, e.Impact, t);
            }
            else
            {
                rt.anchoredPosition = e.To;
                var size = e.Type == SpellType.Shield
                    ? 150 + e.Tier * 42
                    : e.Type == SpellType.FrostRing
                        ? 150 + e.Tier * 55
                        : 95 + e.Tier * 24;
                var pulse = e.Type == SpellType.Shield ? 1 + .035f * e.Tier * Mathf.Sin(t * Mathf.PI * 3) : 1;
                rt.sizeDelta = Vector2.one * size * pulse * Mathf.Lerp(e.Impact ? .65f : .25f, 1.25f, t);
                if (e.Type == SpellType.FrostRing)
                    rt.localRotation = Quaternion.Euler(0, 0, t * (55 + 35 * e.Tier));
                if (!e.Impact) e.Image.color = new Color(1, 1, 1, .45f + .4f * t);
            }
        }
    }
}
