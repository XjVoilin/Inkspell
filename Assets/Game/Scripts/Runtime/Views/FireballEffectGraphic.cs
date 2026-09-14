using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>Deterministic ink-edged ribbons and local impact strokes. Never advances combat.</summary>
    public sealed class FireballEffectGraphic : MaskableGraphic
    {
        private Vector2 _from, _to;
        private int _tier = 1;
        private bool _impact;
        private float _age, _duration = 1;
        private static readonly Color Ink = new(.07f, .055f, .11f);
        private static readonly Color Ember = new(.94f, .18f, .025f);
        private static readonly Color Flame = new(1f, .48f, .035f);
        private static readonly Color Hot = new(1f, .94f, .59f);

        public void SetFrame(Vector2 from, Vector2 to, int tier, bool impact, float age, float duration)
        {
            _from = from; _to = to; _tier = Mathf.Clamp(tier, 1, 3); _impact = impact;
            _age = age; _duration = Mathf.Max(.001f, duration);
            SetVerticesDirty();
        }

        public static float FlightProgress(float time)
        {
            var t = Mathf.Clamp01((time - .13f) / .87f);
            return t * t * .45f + t * .55f;
        }

        public static Vector2 FlightPosition(Vector2 from, Vector2 to, float time)
        {
            var t = FlightProgress(time);
            return Vector2.Lerp(from, to, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 58);
        }

        // Shared by the actual battlefield and the editor comparison, including charge and facing.
        public static void RenderCore(Image core, Vector2 from, Vector2 to, int tier, bool impact, float time)
        {
            tier = Mathf.Clamp(tier, 1, 3);
            var rect = core.rectTransform;
            if (impact)
            {
                var expansion = 1 - Mathf.Pow(1 - Mathf.Clamp01(time / .2f), 3);
                rect.anchoredPosition = to;
                rect.localRotation = Quaternion.Euler(0, 0, tier * 19 + 18 * time);
                rect.sizeDelta = Vector2.one * (85 + 27 * tier) * Mathf.Lerp(.25f, 1.15f, expansion);
                core.color = new Color(1, 1, 1, 1 - Mathf.Clamp01((time - .10f) / .45f));
                return;
            }
            rect.anchoredPosition = FlightPosition(from, to, time);
            var direction = FlightPosition(from, to, Mathf.Min(1, time + .015f)) - FlightPosition(from, to, Mathf.Max(.13f, time - .015f));
            if (direction.sqrMagnitude < .001f) direction = to - from;
            // Flame tips trail behind the hot core instead of spinning the whole icon.
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90);
            var charge = Mathf.SmoothStep(.28f, 1f, Mathf.Clamp01(time / .13f));
            rect.sizeDelta = Vector2.one * (56 + 22 * tier) * charge * (1 + .035f * Mathf.Sin(time * 50));
            core.color = Color.white;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var t = Mathf.Clamp01(_age / _duration);
            if (_impact) Impact(vh, t);
            else Flight(vh, t);
        }

        private void Flight(VertexHelper vh, float t)
        {
            var p = FlightPosition(_from, _to, t);
            var chargeFade = 1 - Mathf.Clamp01(t / .26f);
            if (chargeFade > 0)
            {
                Arc(vh, _from, 22 + 35 * t / .26f, 4, 0, Mathf.PI * 2, WithAlpha(Flame, chargeFade));
                for (var i = 0; i < 3 + _tier * 2; i++)
                {
                    var a = i * 2.399f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Stroke(vh, _from + dir * (48 * chargeFade), -dir * 9, 2, WithAlpha(Hot, chargeFade));
                }
            }
            if (t <= .13f) return;
            Ribbon(vh, t, 0, 13 + 4 * _tier, Ink);
            Ribbon(vh, t, 0, 10 + 4 * _tier, Ember);
            Ribbon(vh, t, 0, 5 + 2 * _tier, Flame);
            Ribbon(vh, t, 0, 2 + _tier, Hot);
            if (_tier == 3)
            {
                Ribbon(vh, t, 16, 5, Ink);
                Ribbon(vh, t, 16, 3, Flame);
                Ribbon(vh, t, -16, 5, Ink);
                Ribbon(vh, t, -16, 3, Flame);
            }
            if (_tier >= 2)
            {
                var angle = _age * 12;
                Arc(vh, p, 26 + 6 * _tier, 4, angle, angle + 3.9f, Ember);
                Arc(vh, p, 26 + 6 * _tier, 1.8f, angle, angle + 3.7f, Hot);
                if (_tier == 3) Arc(vh, p, 48, 2, -angle, -angle + 3.8f, Flame);
            }
            for (var i = 0; i < 3 + 3 * _tier; i++)
            {
                var lag = .02f + i * .018f;
                if (t - lag < .13f) continue;
                var point = FlightPosition(_from, _to, t - lag);
                point += new Vector2(Mathf.Sin(i * 17f) * 8, Mathf.Sin(i * 9f + _age * 8) * (9 + i * 2));
                Stroke(vh, point, new Vector2(-7 - i * .7f, 3), 2.6f * (1 - i / 15f), WithAlpha(Flame, .85f - i * .045f));
            }
        }

        private void Ribbon(VertexHelper vh, float t, float side, float width, Color tint)
        {
            const int segments = 18;
            var length = .12f + .045f * _tier;
            for (var i = 0; i < segments; i++)
            {
                var u = i / (float)segments;
                var v = (i + 1f) / segments;
                var ta = Mathf.Max(.13f, t - length * u);
                var tb = Mathf.Max(.13f, t - length * v);
                if (Mathf.Abs(ta - tb) < .0001f) continue;
                var a = FlightPosition(_from, _to, ta);
                var b = FlightPosition(_from, _to, tb);
                var tangent = (a - b).normalized;
                var normal = new Vector2(-tangent.y, tangent.x);
                a += normal * (side * Mathf.Sin(u * Mathf.PI) + Mathf.Sin(u * 16 - _age * 27) * u * 5);
                b += normal * (side * Mathf.Sin(v * Mathf.PI) + Mathf.Sin(v * 16 - _age * 27) * v * 5);
                var wa = width * (1 - u) * (1 - u);
                var wb = width * (1 - v) * (1 - v);
                Quad(vh, a + normal * wa, b + normal * wb, b - normal * wb, a - normal * wa, tint);
            }
        }

        private void Impact(VertexHelper vh, float t)
        {
            var fade = Mathf.Pow(1 - t, 1.5f);
            var burst = 1 - Mathf.Pow(1 - Mathf.Clamp01(t / .28f), 3);
            // The generated flame bloom is the main impact silhouette. Rings and embers stay local.
            if (_tier >= 2)
            {
                Arc(vh, _to, 12 + (48 + 7 * _tier) * burst, 4 * fade, .12f, 5.8f, WithAlpha(Ember, fade));
                if (_tier == 3)
                {
                    var delayed = Mathf.Clamp01((t - .08f) / .55f);
                    Arc(vh, _to, 20 + 61 * delayed, 2.5f * (1 - delayed), -.4f, 5.3f, WithAlpha(Flame, (1 - delayed) * .8f));
                }
            }
            for (var i = 0; i < 5 + 5 * _tier; i++)
            {
                var angle = i * 2.399f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var distance = (30 + _tier * 17 + i % 4 * 7) * Mathf.Sqrt(t);
                var p = _to + dir * distance + Vector2.down * (25 * t * t);
                Stroke(vh, p, dir * (7 + _tier * 3) * fade, 2.8f * fade, WithAlpha(Ink, fade));
                Stroke(vh, p, dir * (6 + _tier * 3) * fade, 1.6f * fade, WithAlpha(i % 2 == 0 ? Hot : Flame, fade));
            }
            var flash = 1 - Mathf.Clamp01(t / .24f);
            Disc(vh, _to, (12 + 5 * _tier) * (.7f + burst) * flash, WithAlpha(Hot, flash));
        }

        private static Color WithAlpha(Color c, float alpha) { c.a = alpha; return c; }
        private static void Stroke(VertexHelper vh, Vector2 start, Vector2 delta, float width, Color tint)
        {
            if (width <= .01f) return;
            var normal = new Vector2(-delta.y, delta.x).normalized * width;
            Quad(vh, start + normal, start + delta, start - normal, start - delta * .12f, tint);
        }
        private static void Arc(VertexHelper vh, Vector2 center, float radius, float width, float start, float end, Color tint)
        {
            if (width <= .01f || radius <= .01f) return;
            const int count = 40;
            for (var i = 0; i < count; i++)
            {
                var a = Mathf.Lerp(start, end, i / (float)count);
                var b = Mathf.Lerp(start, end, (i + 1f) / count);
                var da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                var w = width * (.7f + .3f * Mathf.Sin(i * .6f));
                Quad(vh, center + da * (radius + w), center + db * (radius + w), center + db * (radius - w), center + da * (radius - w), tint);
            }
        }
        private static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            for (var i = 0; i < 20; i++)
            {
                var a = i * Mathf.PI * .1f;
                var b = (i + 1) * Mathf.PI * .1f;
                Quad(vh, center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, center, tint);
            }
        }
        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            var n = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
