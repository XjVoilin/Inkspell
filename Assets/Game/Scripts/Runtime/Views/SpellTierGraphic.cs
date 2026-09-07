using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // Bevelled ink frame follows the existing parchment tile, with no new textures/materials.
    public sealed class SpellTierGraphic : MaskableGraphic
    {
        private int _tier;
        private static readonly Vector2[] Corners =
        {
            new(.12f,.055f), new(.88f,.055f), new(.945f,.12f), new(.945f,.88f),
            new(.88f,.945f), new(.12f,.945f), new(.055f,.88f), new(.055f,.12f)
        };
        public void SetTier(int tier)
        {
            if (_tier == tier) return;
            _tier = tier;
            SetVerticesDirty();
        }
        public static Color TierColor(int tier) => tier switch
        {
            2 => new Color(.12f,.43f,.43f), // oxidised teal
            3 => new Color(.45f,.20f,.38f), // muted plum ink
            _ => new Color(.49f,.34f,.18f), // aged bronze
        };
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_tier <= 0) return;
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var width = _tier == 1 ? 2.5f : 4.5f;
            var shrink = 1 - width / (Mathf.Min(rect.width,rect.height) * .5f);
            for (var i = 0; i < Corners.Length; i++)
            {
                var a = rect.min + Vector2.Scale(Corners[i],rect.size);
                var b = rect.min + Vector2.Scale(Corners[(i+1)%Corners.Length],rect.size);
                Quad(vh,a,b,center+(b-center)*shrink,center+(a-center)*shrink,TierColor(_tier));
                if (_tier == 3)
                    Quad(vh,center+(a-center)*.90f,center+(b-center)*.90f,
                        center+(b-center)*.885f,center+(a-center)*.885f,new Color(.76f,.57f,.29f));
            }
        }
        private static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
        {
            var n=vh.currentVertCount;
            vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);
            vh.AddVert(c,tint,Vector2.zero);vh.AddVert(d,tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
