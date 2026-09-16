using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>추가 비트맵 의존성 없는 단색 UI 아이콘. 크기에 맞춰 메시를 한 번 갱신한다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerUiIcon : MaskableGraphic
    {
        public enum Symbol { Coin, Shield, Bow, Tower, Spark, Plus, Gate, Sword, Close }
        public Symbol Kind { get => _symbol; set { if (_symbol != value) { _symbol = value; SetVerticesDirty(); } } }

        [SerializeField] private Symbol _symbol;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (_symbol)
            {
                case Symbol.Close:
                    Line(mesh, .25f, .25f, .75f, .75f); Line(mesh, .25f, .75f, .75f, .25f); break;
                case Symbol.Coin:
                    Ring(mesh, .5f, .5f, .38f, .06f);
                    Line(mesh, .43f, .27f, .43f, .73f);
                    Line(mesh, .58f, .30f, .58f, .70f);
                    break;
                case Symbol.Shield:
                    Line(mesh, .2f, .8f, .8f, .8f); Line(mesh, .2f, .8f, .25f, .4f);
                    Line(mesh, .25f, .4f, .5f, .16f); Line(mesh, .5f, .16f, .75f, .4f);
                    Line(mesh, .75f, .4f, .8f, .8f); Line(mesh, .5f, .26f, .5f, .7f);
                    break;
                case Symbol.Bow:
                    Line(mesh, .3f, .15f, .7f, .5f); Line(mesh, .7f, .5f, .3f, .85f);
                    Line(mesh, .3f, .15f, .3f, .85f); Line(mesh, .15f, .5f, .88f, .5f);
                    Line(mesh, .88f, .5f, .74f, .64f); break;
                case Symbol.Tower:
                case Symbol.Gate:
                    Line(mesh, .2f, .18f, .2f, .8f); Line(mesh, .8f, .18f, .8f, .8f);
                    Line(mesh, .2f, .8f, .8f, .8f); Line(mesh, .15f, .18f, .85f, .18f);
                    Line(mesh, .38f, .18f, .38f, .48f); Line(mesh, .62f, .18f, .62f, .48f);
                    Line(mesh, .38f, .48f, .62f, .48f);
                    Line(mesh, .2f, .8f, .2f, .93f); Line(mesh, .5f, .8f, .5f, .93f); Line(mesh, .8f, .8f, .8f, .93f);
                    break;
                case Symbol.Plus:
                    Line(mesh, .2f, .5f, .8f, .5f); Line(mesh, .5f, .2f, .5f, .8f); break;
                case Symbol.Sword:
                    Line(mesh, .25f, .2f, .8f, .8f); Line(mesh, .8f, .8f, .66f, .78f);
                    Line(mesh, .8f, .8f, .78f, .65f); Line(mesh, .2f, .48f, .5f, .2f); break;
                default:
                    Line(mesh, .5f, .12f, .5f, .88f); Line(mesh, .12f, .5f, .88f, .5f);
                    Line(mesh, .27f, .27f, .73f, .73f); Line(mesh, .27f, .73f, .73f, .27f); break;
            }
        }

        private void Ring(VertexHelper mesh, float x, float y, float radius, float thickness)
        {
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 2 * Mathf.PI / segments;
                float b = (i + 1) * 2 * Mathf.PI / segments;
                Line(mesh, x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius,
                    x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius, thickness);
            }
        }

        private void Line(VertexHelper mesh, float ax, float ay, float bx, float by, float width = .06f)
        {
            var rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            var center = rect.center;
            var a = center + new Vector2(ax - .5f, ay - .5f) * size;
            var b = center + new Vector2(bx - .5f, by - .5f) * size;
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * size * width * .5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(a - n, color, Vector2.zero); mesh.AddVert(a + n, color, Vector2.zero);
            mesh.AddVert(b + n, color, Vector2.zero); mesh.AddVert(b - n, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
