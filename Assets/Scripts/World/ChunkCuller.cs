using System.Collections.Generic;
using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Дальность прорисовки города: куски карты (чанки) дальше CullDistance выключаются. За такой дистанцией город
    /// всё равно тонет в тумане, а рисовать его — тратить draw call'ы и вершины зря. Проверка идёт 5 раз в секунду.
    /// </summary>
    public class ChunkCuller : MonoBehaviour
    {
        public float CullDistance = 175f;

        struct Entry
        {
            public Renderer R;
            public Vector3 C;
            public float Rad;
            public bool On;
        }

        readonly List<Entry> items = new List<Entry>();
        float timer;

        public int Count { get { return items.Count; } }

        public int VisibleCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].On) n++;
                }
                return n;
            }
        }

        public void Add(Renderer r)
        {
            if (r == null) return;
            Bounds b = r.bounds;
            Entry e = new Entry();
            e.R = r;
            e.C = b.center;
            e.Rad = b.extents.magnitude;
            e.On = true;
            items.Add(e);
        }

        /// <summary>Пересчитать видимость сразу (после телепорта камеры: без этого пять кадров был бы виден пустой мир).</summary>
        public void Refresh()
        {
            timer = 0f;
            Update();
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.2f;
            Transform cam = Gfx.CamTransform;
            if (cam == null) return;
            Vector3 p = cam.position;
            for (int i = 0; i < items.Count; i++)
            {
                Entry e = items[i];
                if (e.R == null) continue;
                float d = Vector3.Distance(p, e.C) - e.Rad;
                bool on = d < CullDistance;
                if (on != e.On)
                {
                    e.On = on;
                    e.R.enabled = on;
                    items[i] = e;
                }
            }
        }
    }
}
