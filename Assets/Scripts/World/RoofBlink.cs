using UnityEngine;

namespace PokaGoryu
{
    /// <summary>Красные огни на крышах: мигают все вместе (один общий материал). Видны издалека — ориентиры в темноте.</summary>
    public class RoofBlink : MonoBehaviour
    {
        public Material Mat;

        void Update()
        {
            if (Mat == null) return;
            float p = Mathf.Clamp01(0.5f + 0.9f * Mathf.Sin(Time.time * 3.4f));
            Mat.SetColor("_Color", new Color(1f * p, 0.12f * p, 0.08f * p, 1f));
        }
    }
}
