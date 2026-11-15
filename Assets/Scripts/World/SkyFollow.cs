using UnityEngine;

namespace PokaGoryu
{
    /// <summary>Небесный купол всегда центрирован на камере (бесконечно далёкое небо).</summary>
    public class SkyFollow : MonoBehaviour
    {
        void LateUpdate()
        {
            Transform c = Gfx.CamTransform;
            if (c != null) transform.position = c.position;
        }
    }
}
