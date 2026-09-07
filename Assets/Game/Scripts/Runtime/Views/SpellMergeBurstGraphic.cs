using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class SpellMergeBurstGraphic : MaskableGraphic
    {
        private float _progress;
        private bool _success;
        public void Render(float progress, bool success)
        {
            _progress = Mathf.Clamp01(progress); _success = success; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var tint = _success ? new Color(1, .72f, .2f, 1 - _progress) : new Color(.14f, .10f, .22f, 1 - _progress);
            var radius = Mathf.Lerp(18, 105, _progress);
            var center = rectTransform.rect.center;
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI / 6;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var side = new Vector2(-dir.y, dir.x);
                var p = center + dir * radius;
                var size = (1 - _progress) * (_success ? 10 : 7);
                var n = vh.currentVertCount;
                vh.AddVert(p + dir * size, tint, Vector2.zero);
                vh.AddVert(p + side * size * .4f, tint, Vector2.zero);
                vh.AddVert(p - dir * size, tint, Vector2.zero);
                vh.AddVert(p - side * size * .4f, tint, Vector2.zero);
                vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
