using UnityEngine;

namespace PokaGoryu
{
    /// <summary>Короткая вспышка-спрайт: растёт и гаснет, затем исчезает сама.</summary>
    public class TimedFx : MonoBehaviour
    {
        public float Life = 0.3f;
        public float StartScale = 1f;
        public float EndScale = 2f;
        public Color Tint = Color.white;
        public Material Mat;

        float t;

        public static TimedFx Spawn(Vector3 pos, float startSize, float endSize, Color tint, float life)
        {
            Material m = Gfx.Unlit(tint, Gfx.TexHalo, true, false, 0.3f);
            GameObject go = Gfx.Sprite(null, pos, new Vector2(1f, 1f), m, false);
            go.name = "Fx";
            TimedFx fx = go.AddComponent<TimedFx>();
            fx.Life = life;
            fx.StartScale = startSize;
            fx.EndScale = endSize;
            fx.Tint = tint;
            fx.Mat = m;
            go.transform.localScale = new Vector3(startSize, startSize, 1f);
            return fx;
        }

        void OnDestroy()
        {
            if (Mat != null) Destroy(Mat);
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / Life;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            float s = Mathf.Lerp(StartScale, EndScale, k);
            transform.localScale = new Vector3(s, s, 1f);
            if (Mat != null) Mat.SetColor("_Color", new Color(Tint.r, Tint.g, Tint.b, Tint.a * (1f - k)));
        }
    }
}
