using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Столб света над автовокзалом: издали яркий (виден над крышами из любого района), а когда герой подошёл ближе
    /// 20-60 м, плавно тает, чтобы не слепить и не закрывать сам автобус.
    /// </summary>
    public class BeaconFade : MonoBehaviour
    {
        public float NearDist = 20f;     // ближе этого столб почти невидим
        public float FarDist = 65f;      // дальше этого столб виден целиком
        public float MinFactor = 0.07f;

        Renderer rend;
        Material mat;
        Color baseColor;

        void Start()
        {
            rend = GetComponent<Renderer>();
            if (rend == null) return;
            mat = rend.sharedMaterial;
            if (mat != null) baseColor = mat.GetColor("_Color");
        }

        void LateUpdate()
        {
            if (mat == null || Gfx.CamTransform == null) return;
            Vector3 d = Gfx.CamTransform.position - transform.position;
            d.y = 0f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(NearDist, FarDist, d.magnitude));
            Color c = baseColor;
            c.a *= Mathf.Lerp(MinFactor, 1f, k);
            mat.SetColor("_Color", c);
        }
    }
}
