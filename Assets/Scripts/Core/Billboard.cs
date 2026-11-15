using UnityEngine;

namespace PokaGoryu
{
    /// <summary>Поворачивает плоский спрайт лицом к камере — так в 3D-мир попадают 2D-элементы.</summary>
    public class Billboard : MonoBehaviour
    {
        public bool YawOnly = true;

        void LateUpdate()
        {
            Transform c = Gfx.CamTransform;
            if (c == null) return;
            Vector3 d = transform.position - c.position;
            if (YawOnly) d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
    }
}
