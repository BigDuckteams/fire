using System.Collections.Generic;
using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Генератор в нише у тоннеля. Герой отдаёт ему свет (выдох, F) — и все погасшие лампы вокруг загораются.
    /// Это вклад в город: потратил свет один раз — получил освещённый тоннель для всех, кто пойдёт следом.
    /// </summary>
    public class Generator : MonoBehaviour, ILightSource
    {
        public static readonly List<Generator> All = new List<Generator>();

        public const float Need = 28f;
        public const float Reach = 50f;       // лампы в таком радиусе загораются

        public float Charge;
        public bool Running;
        public float Progress;

        public Vector3 FocusPoint { get { return pos + Vector3.up * 1.0f; } }
        public Vector3 BasePos { get { return pos; } }

        Vector3 pos;
        Material lampMat;
        Material haloMat;
        AudioSource hum;

        public static Generator Create(Transform parent, Vector3 pos)
        {
            GameObject go = new GameObject("Generator");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            Generator g = go.AddComponent<Generator>();
            g.Setup(pos);
            return g;
        }

        void Setup(Vector3 p)
        {
            pos = p;
            Progress = CityMap.Progress(p.x, p.z);
            Transform t = transform;
            // Корпус целиком — один меш (рама, двигатель с рёбрами, бак, выхлоп, щиток, провода)
            MeshBatch gb = new MeshBatch();
            Models.GeneratorBody(gb);
            Mesh gm = gb.Build("PG_Generator");
            LevelBuilder.TrackMesh(gm);
            GameObject gbody = Gfx.MeshObject(t, gm, Gfx.Lit(Color.white), "GeneratorBody");
            gbody.transform.localPosition = Vector3.zero;
            GameObject gcol = Gfx.StaticBox(t, p + new Vector3(0f, 0.7f, 0f), new Vector3(1.9f, 1.1f, 1.2f), "GeneratorCollider");
            gcol.transform.SetParent(t, true);
            lampMat = OwnedResources.Keep(gameObject, Gfx.Unlit(new Color(0.25f, 0.06f, 0.04f, 1f), null, false, false, 0.2f));
            Gfx.Prim(PrimitiveType.Sphere, t, new Vector3(-0.5f, 1.2f, -0.52f), new Vector3(0.22f, 0.22f, 0.22f), lampMat, false);
            haloMat = OwnedResources.Keep(gameObject, Gfx.Unlit(new Color(1f, 0.6f, 0.3f, 0f), Gfx.TexHalo, true, false, 0.12f));
            Gfx.Sprite(t, new Vector3(0f, 1.5f, 0f), new Vector2(3.4f, 3.4f), haloMat, false);
            hum = ProcAudio.Source(gameObject, "engine", true, 0f, true);
            All.Add(this);
            LightField.Register(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
            LightField.Unregister(this);
        }

        /// <summary>Герой отдаёт свет (единицы).</summary>
        public void Feed(float amount)
        {
            if (Running) return;
            Charge += amount;
            if (Charge >= Need) StartGenerator();
        }

        void StartGenerator()
        {
            Running = true;
            int lit = 0;
            for (int i = 0; i < Lamp.All.Count; i++)
            {
                Lamp l = Lamp.All[i];
                if (l.IsHearth || l.IsDecoy) continue;
                float dx = l.BasePos.x - pos.x;
                float dz = l.BasePos.z - pos.z;
                if (dx * dx + dz * dz > Reach * Reach) continue;
                if (l.Dead)
                {
                    l.Feed(1.0f);
                    lit++;
                }
            }
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.Notify(lit > 0 ? "Генератор запущен: вокруг зажглись огни (" + lit + ")" : "Генератор запущен");
            TimedFx.Spawn(pos + Vector3.up * 1.4f, 1f, 8f, new Color(1f, 0.7f, 0.4f, 0.8f), 0.6f);
        }

        public void ResetToInitial()
        {
            Charge = 0f;
            Running = false;
        }

        void Update()
        {
            float ratio = Running ? 1f : Mathf.Clamp01(Charge / Need);
            float pulse = Running ? 0.8f + 0.2f * Mathf.Sin(Time.time * 9f) : 0.4f + 0.6f * ratio;
            lampMat.SetColor("_Color", Running ? new Color(0.4f * pulse + 0.3f, 1f * pulse, 0.4f * pulse, 1f) : new Color(0.25f + 0.7f * ratio, 0.06f + 0.4f * ratio, 0.04f, 1f));
            haloMat.SetColor("_Color", new Color(1f, 0.65f, 0.3f, (Running ? 0.5f : 0.35f * ratio) * pulse));
            AudioMix.SetVolume(hum, Running ? 0.35f : 0.12f * ratio);

            // подпись — только когда смотришь на генератор с расстояния взаимодействия (3,2 м)
            if (!Running)
            {
                string txt = ratio > 0.01f ? "ГЕНЕРАТОР " + Mathf.RoundToInt(ratio * 100f) + "%" : "ГЕНЕРАТОР";
                GameUI.Label(pos + Vector3.up * 2.2f, txt, new Color(1f, 0.75f, 0.4f, 1f), 4.2f, pos + Vector3.up * 0.9f, 0.92f);
            }
        }

        // ------------------------------------------------------------- ILightSource

        public Vector3 LightPosition { get { return pos + Vector3.up * 1.4f; } }
        public float LightRadius { get { return Running ? 6f : 2f; } }
        public float LightIntensity { get { return Running ? 0.5f : 0.15f; } }
        public Color LightColor { get { return new Color(1f, 0.7f, 0.4f, 1f); } }
    }
}
