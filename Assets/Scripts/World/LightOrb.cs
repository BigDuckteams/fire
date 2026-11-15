using System.Collections.Generic;
using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Осколок освобождённого света: выпадает, когда Тень убита точным лучом (или погас человек).
    /// Герой вдыхает его на E. Грубая вспышка рассеивает свет впустую — осколков не остаётся.
    /// </summary>
    public class LightOrb : MonoBehaviour, ILightSource
    {
        public static readonly List<LightOrb> All = new List<LightOrb>();

        public float Value = 6f;
        public bool Collected { get { return collecting; } }

        bool collecting;
        float age;
        float collectSpeed = 4f;
        Vector3 basePos;
        Material mat;
        Material haloMat;
        float size;

        public static LightOrb Spawn(Vector3 pos, float value)
        {
            GameObject go = new GameObject("LightOrb");
            go.transform.position = pos + Vector3.up * 0.6f;
            LightOrb o = go.AddComponent<LightOrb>();
            o.Setup(value);
            return o;
        }

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i] != null) Destroy(All[i].gameObject);
            }
            All.Clear();
        }

        void Setup(float v)
        {
            Value = v;
            basePos = transform.position;
            size = Mathf.Lerp(0.18f, 0.42f, Mathf.Clamp01(v / 25f));
            mat = OwnedResources.Keep(gameObject, Gfx.Unlit(new Color(1f, 0.9f, 0.55f, 1f), null, false, false, 0.3f));
            Gfx.Prim(PrimitiveType.Sphere, transform, Vector3.zero, new Vector3(size, size, size), mat, false);
            haloMat = OwnedResources.Keep(gameObject, Gfx.Unlit(new Color(1f, 0.85f, 0.5f, 0.6f), Gfx.TexHalo, true, false, 0.2f));
            Gfx.Sprite(transform, Vector3.zero, new Vector2(size * 7f, size * 7f), haloMat, false);
            All.Add(this);
            LightField.Register(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
            LightField.Unregister(this);
        }

        /// <summary>Герой вдохнул осколок: он летит к груди.</summary>
        public void Collect()
        {
            collecting = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            GameManager gm = GameManager.Instance;
            if (gm != null && (gm.State != GameState.Playing || gm.Paused)) return;
            age += dt;

            if (!collecting)
            {
                transform.position = basePos + new Vector3(0f, Mathf.Sin(age * 2.2f) * 0.12f, 0f);
                // осколок рядом с героем сам летит к нему: убил — получил свет (на E его можно взять и издалека, до OrbReach).
                // Если света и так полно или герой на последнем вздохе, осколок ждёт.
                PlayerController near = PlayerController.Instance;
                if (near != null && age > 0.4f && !near.Hero.InLastBreath && near.Hero.Life < GameConfig.MaxLight - 0.5f)
                {
                    Vector3 chest = near.transform.position + Vector3.up * 1.3f;
                    if ((transform.position - chest).magnitude < GameConfig.OrbMagnetRadius) collecting = true;
                }
                if (!collecting)
                {
                    if (age > 40f)
                    {
                        Destroy(gameObject);
                        All.Remove(this);
                        LightField.Unregister(this);
                    }
                    return;
                }
            }

            PlayerController pl = PlayerController.Instance;
            if (pl == null) return;
            Vector3 target = pl.transform.position + Vector3.up * 1.3f;
            collectSpeed += dt * 14f;
            transform.position = Vector3.MoveTowards(transform.position, target, collectSpeed * dt);
            if ((transform.position - target).magnitude < 0.5f)
            {
                pl.Hero.Gain(Value * GameDifficulty.OrbScale, LightReason.Orb);
                ProcAudio.OneShot(pl.Sfx, "orb", 0.8f, Random.Range(0.95f, 1.1f));
                All.Remove(this);
                LightField.Unregister(this);
                Destroy(gameObject);
            }
        }

        public Vector3 LightPosition { get { return transform.position; } }
        public float LightRadius { get { return collecting ? 0f : 3.5f; } }
        public float LightIntensity { get { return 0.5f; } }
        public Color LightColor { get { return new Color(1f, 0.85f, 0.5f, 1f); } }
    }
}
