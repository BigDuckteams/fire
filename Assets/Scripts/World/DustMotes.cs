using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Пылинки в воздухе вокруг камеры. Видны только там, где есть свет (альфа считается через LightField),
    /// поэтому в конусах ламп и в ауре героя воздух «живёт», а в темноте их нет.
    /// </summary>
    public class DustMotes : MonoBehaviour
    {
        const int Count = 56;
        const float Radius = 9f;

        Transform[] t;
        Renderer[] r;
        Vector3[] vel;
        float[] phase;
        MaterialPropertyBlock mpb;

        public void Init()
        {
            t = new Transform[Count];
            r = new Renderer[Count];
            vel = new Vector3[Count];
            phase = new float[Count];
            mpb = new MaterialPropertyBlock();
            Material m = OwnedResources.Keep(gameObject, Gfx.Unlit(new Color(1f, 0.95f, 0.8f, 0.5f), Gfx.TexHalo, true, false, 0.2f));
            for (int i = 0; i < Count; i++)
            {
                float s = Random.Range(0.035f, 0.08f);
                GameObject g = Gfx.Sprite(transform, Vector3.zero, new Vector2(s, s), m, false);
                g.name = "Mote";
                t[i] = g.transform;
                r[i] = g.GetComponent<Renderer>();
                vel[i] = new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(-0.04f, 0.06f), Random.Range(-0.05f, 0.05f));
                phase[i] = Random.value * 6.28f;
                t[i].position = RandomAround(Vector3.zero);
            }
        }

        static Vector3 RandomAround(Vector3 c)
        {
            return c + Random.insideUnitSphere * Radius;
        }

        void LateUpdate()
        {
            Transform cam = Gfx.CamTransform;
            if (cam == null || t == null) return;
            GameManager gm = GameManager.Instance;
            float dt = (gm != null && gm.Paused) ? 0f : Time.deltaTime;
            Vector3 cp = cam.position;
            for (int i = 0; i < Count; i++)
            {
                Vector3 p = t[i].position;
                p += vel[i] * dt;
                p.x += Mathf.Sin(Time.time * 0.6f + phase[i]) * 0.02f * dt * 10f;
                if ((p - cp).sqrMagnitude > Radius * Radius) p = RandomAround(cp);
                t[i].position = p;
                float light = LightField.Sample(p);
                float a = Mathf.Clamp01(light * 1.1f) * 0.55f;
                mpb.SetColor("_Color", new Color(1f, 0.95f, 0.8f, a));
                r[i].SetPropertyBlock(mpb);
            }
        }
    }
}
