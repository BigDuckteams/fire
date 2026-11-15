using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Модели «живого» города из коробок, призм и шаров-«кляксов» (MeshBatch): листва и деревья, кусты и клумбы, фонтан,
    /// беседка, детская площадка, витрины с товарами, вывески, столики кафе, почтовые ящики, телефонные будки, светофоры,
    /// провода и бельё. Цвет — в вершинах, свет считает шейдер мира. Локальные оси: +Z — вперёд (к улице), +X — вправо.
    /// </summary>
    public static class Decor
    {
        const int NB = MeshBatch.NoBottom;
        const int ALL = MeshBatch.AllFaces;

        static Color32 Col(int r, int g, int b) { return Models.Col(r, g, b); }
        static Color32 Dim(Color32 c, float k) { return Models.Dim(c, k); }
        static float Rnd(System.Random rng) { return (float)rng.NextDouble(); }
        static float Rng(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        static Color32 Jit(Color32 c, System.Random rng, float amt)
        {
            float k = 1f + (Rnd(rng) * 2f - 1f) * amt;
            return Dim(c, k);
        }

        static void LB(MeshBatch b, Vector3 o, float yaw, Vector3 local, Vector3 size, Color32 col)
        {
            b.BoxYaw(o + Models.RotY(local, yaw), size, yaw, col, ALL);
        }

        static void LP(MeshBatch b, Vector3 o, float yaw, Vector3 local, Vector3 axis, float r, float len, int sides, Color32 col, bool caps)
        {
            b.Prism(o + Models.RotY(local, yaw), Models.RotY(axis, yaw), r, len, sides, col, caps);
        }

        static void LRod(MeshBatch b, Vector3 o, float yaw, Vector3 a, Vector3 c, float r, Color32 col)
        {
            b.Rod(o + Models.RotY(a, yaw), o + Models.RotY(c, yaw), r, 5, col);
        }

        /// <summary>Четырёхугольник, видимый с обеих сторон.</summary>
        static void Quad2(MeshBatch b, Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Color32 col)
        {
            b.Quad(a, bb, c, d, col);
            b.Quad(d, c, bb, a, col);
        }

        // ------------------------------------------------------------------ листва и деревья

        /// <summary>Лохматый шар-«клякса»: эллипсоид с неровностями, каждая грань чуть своего оттенка (низкополигональная листва).</summary>
        public static void Blob(MeshBatch b, Vector3 c, float rx, float ry, float rz, Color32 col, System.Random rng, int seg, int rings, float jitter)
        {
            Vector3[,] p = new Vector3[rings + 1, seg];
            for (int k = 0; k <= rings; k++)
            {
                float a = Mathf.PI * k / rings;
                float y = Mathf.Cos(a);
                float rr = Mathf.Sin(a);
                bool pole = k == 0 || k == rings;
                for (int i = 0; i < seg; i++)
                {
                    float t = 6.2831853f * i / seg;
                    float j = pole ? 1f : 1f + jitter * (Rnd(rng) * 2f - 1f);
                    float jy = pole ? 1f : 1f + jitter * 0.5f * (Rnd(rng) * 2f - 1f);
                    p[k, i] = c + new Vector3(Mathf.Cos(t) * rr * rx * j, y * ry * jy, Mathf.Sin(t) * rr * rz * j);
                }
            }
            for (int k = 0; k < rings; k++)
            {
                for (int i = 0; i < seg; i++)
                {
                    int i2 = (i + 1) % seg;
                    Color32 fc = Jit(col, rng, 0.10f);
                    if (k == 0) b.Tri(p[0, i], p[1, i2], p[1, i], fc);
                    else if (k == rings - 1) b.Tri(p[rings, i], p[rings - 1, i], p[rings - 1, i2], fc);
                    else b.Quad(p[k, i], p[k, i2], p[k + 1, i2], p[k + 1, i], fc);
                }
            }
        }

        /// <summary>Дерево с прямым стволом и обычной кроной (парки).</summary>
        public static void Tree(MeshBatch b, Vector3 p, System.Random rng, int kind, Color32 leaf, float scale)
        {
            Tree(b, p, rng, kind, leaf, scale, Vector3.zero, 1f);
        }

        /// <summary>
        /// Дерево. kind: 0 — лиственное с круглой кроной, 1 — ель, 2 — тополь (тонкое и высокое, светлый ствол).
        /// leaf — цвет листвы; scale — размер (1 — около 6 м). Ствол чистый до 2.4 м: под кроной можно ходить.
        /// lean — горизонтальный наклон: крона уезжает на lean от ствола (у стены дерево клонится на улицу и не лезет в фасад);
        /// spread — ширина кроны (0.8 — компактная, для узких тротуаров).
        /// </summary>
        public static void Tree(MeshBatch b, Vector3 p, System.Random rng, int kind, Color32 leaf, float scale, Vector3 lean, float spread)
        {
            Vector3 up = Vector3.up;
            float s = scale;
            lean.y = 0f;
            bool leaning = lean.sqrMagnitude > 0.01f;
            if (kind == 1)
            {
                Color32 bark = Col(46, 36, 30);
                b.Frustum(p + new Vector3(0f, 0.9f * s, 0f), up, 0.16f * s, 0.09f * s, 1.8f * s, 6, bark, false, true);
                for (int t = 0; t < 5; t++)
                {
                    float yc = (1.5f + t * 0.95f) * s;
                    float r0 = (1.8f - t * 0.33f) * s;
                    b.Frustum(p + new Vector3(0f, yc, 0f), up, r0, 0.1f * s, 1.45f * s, 8, Jit(leaf, rng, 0.12f), true, false);
                }
                return;
            }
            if (kind == 2)
            {
                Color32 bark = Col(150, 146, 134);
                b.Frustum(p + new Vector3(0f, 2.3f * s, 0f), up, 0.13f * s, 0.07f * s, 4.6f * s, 6, bark, false, true);
                for (int k = 0; k < 6; k++)
                    b.Prism(p + new Vector3(0f, (0.5f + k * 0.7f) * s, 0f), up, 0.135f * s, 0.07f * s, 6, Col(40, 36, 32), false);
                for (int k = 0; k < 4; k++)
                {
                    float yy = (3.9f + k * 1.05f) * s;
                    float rr = (0.95f - k * 0.12f) * s * spread;
                    // тополь тянется вверх и чуть в сторону от стены: чем выше, тем сильнее наклон
                    Vector3 sh = lean * (0.25f + 0.25f * k);
                    Blob(b, p + sh + new Vector3(Rng(rng, -0.12f, 0.12f) * s, yy, Rng(rng, -0.12f, 0.12f) * s), rr, 1.25f * s, rr, Jit(leaf, rng, 0.14f), rng, 7, 4, 0.14f);
                }
                return;
            }

            Color32 bk = Col(54, 42, 34);
            float cy = (3.9f + Rnd(rng) * 0.4f) * s;
            if (leaning)
            {
                // ствол прямой до 1.5 м, дальше клонится от стены к центру кроны
                b.Frustum(p + new Vector3(0f, 0.75f * s, 0f), up, 0.22f * s, 0.16f * s, 1.5f * s, 7, bk, false, false);
                b.Rod(p + new Vector3(0f, 1.45f * s, 0f), p + lean * 0.7f + new Vector3(0f, 3.0f * s, 0f), 0.15f * s, 7, bk);
            }
            else
            {
                b.Frustum(p + new Vector3(0f, 1.3f * s, 0f), up, 0.22f * s, 0.13f * s, 2.6f * s, 7, bk, false, true);
            }
            b.Frustum(p + new Vector3(0f, 0.18f * s, 0f), up, 0.36f * s, 0.24f * s, 0.36f * s, 7, Dim(bk, 0.9f), false, false);
            Vector3 trunkTop = p + (leaning ? lean * 0.7f : Vector3.zero) + new Vector3(0f, (leaning ? 3.0f : 2.5f) * s, 0f);
            int n = 4 + rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                float a = i * 6.2831853f / n + Rnd(rng) * 0.8f;
                float rr = (0.8f + Rnd(rng) * 0.5f) * s * spread;
                Vector3 off = new Vector3(Mathf.Cos(a) * rr, (Rnd(rng) - 0.35f) * 0.8f * s, Mathf.Sin(a) * rr);
                float sz = (1.25f + Rnd(rng) * 0.45f) * s * (0.5f + 0.5f * spread);
                Vector3 center = p + lean + new Vector3(0f, cy, 0f) + off;
                Blob(b, center, sz, sz * 0.85f, sz, Jit(leaf, rng, 0.16f), rng, 8, 4, 0.14f);
                b.Rod(trunkTop, center - (center - trunkTop).normalized * sz * 0.4f, 0.06f * s, 5, bk);
            }
            Blob(b, p + lean + new Vector3(0f, cy + 0.9f * s, 0f), 1.35f * s * (0.5f + 0.5f * spread), 1.1f * s, 1.35f * s * (0.5f + 0.5f * spread), Jit(leaf, rng, 0.12f), rng, 8, 4, 0.14f);
        }

        /// <summary>Куст: несколько шаров у земли.</summary>
        public static void Shrub(MeshBatch b, Vector3 p, System.Random rng, float r, Color32 col)
        {
            int n = 2 + rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                Vector3 off = new Vector3(Rng(rng, -0.6f, 0.6f) * r, 0f, Rng(rng, -0.6f, 0.6f) * r);
                float s = r * Rng(rng, 0.7f, 1f);
                Blob(b, p + off + new Vector3(0f, s * 0.55f, 0f), s, s * 0.72f, s, Jit(col, rng, 0.14f), rng, 7, 3, 0.16f);
            }
        }

        /// <summary>Живая изгородь: длинная приземистая коробка из листвы вдоль оси x (w — длина).</summary>
        public static void Hedge(MeshBatch b, Vector3 p, float yaw, float w, float h, System.Random rng, Color32 col)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(w / 0.9f));
            for (int i = 0; i < n; i++)
            {
                float x = -w * 0.5f + (i + 0.5f) * w / n;
                Blob(b, p + Models.RotY(new Vector3(x, h * 0.5f, 0f), yaw), w / n * 0.62f, h * 0.55f, 0.5f, Jit(col, rng, 0.12f), rng, 6, 3, 0.1f);
            }
        }

        /// <summary>Клумба с каменным бордюром и цветами.</summary>
        public static void FlowerBed(MeshBatch b, Vector3 p, float yaw, float w, float d, System.Random rng)
        {
            Color32 stone = Col(88, 84, 78);
            LB(b, p, yaw, new Vector3(0f, 0.09f, d * 0.5f), new Vector3(w, 0.18f, 0.12f), stone);
            LB(b, p, yaw, new Vector3(0f, 0.09f, -d * 0.5f), new Vector3(w, 0.18f, 0.12f), stone);
            LB(b, p, yaw, new Vector3(w * 0.5f, 0.09f, 0f), new Vector3(0.12f, 0.18f, d), stone);
            LB(b, p, yaw, new Vector3(-w * 0.5f, 0.09f, 0f), new Vector3(0.12f, 0.18f, d), stone);
            LB(b, p, yaw, new Vector3(0f, 0.1f, 0f), new Vector3(w - 0.1f, 0.15f, d - 0.1f), Col(38, 50, 32));
            Color32[] fl = { Col(176, 52, 48), Col(190, 158, 44), Col(128, 70, 148), Col(206, 200, 186), Col(196, 104, 132), Col(214, 120, 40) };
            int n = Mathf.Clamp((int)(w * d * 5f), 5, 18);
            for (int i = 0; i < n; i++)
            {
                Vector3 l = new Vector3(Rng(rng, -w * 0.5f + 0.14f, w * 0.5f - 0.14f), 0.24f, Rng(rng, -d * 0.5f + 0.14f, d * 0.5f - 0.14f));
                float r = Rng(rng, 0.09f, 0.14f);
                Color32 c = (i % 4 == 0) ? Col(44, 78, 38) : fl[rng.Next(fl.Length)];
                Blob(b, p + Models.RotY(l, yaw), r, r * 0.9f, r, c, rng, 5, 3, 0.1f);
            }
        }

        /// <summary>Фонтан: каменная чаша с водой, колонна и верхняя чаша. Диаметр около 5.4 м.</summary>
        public static void Fountain(MeshBatch b, Vector3 p)
        {
            Color32 stone = Col(96, 92, 86);
            Color32 water = Col(26, 44, 52);
            b.Prism(p + new Vector3(0f, 0.28f, 0f), Vector3.up, 2.7f, 0.56f, 18, stone, true);
            b.Prism(p + new Vector3(0f, 0.60f, 0f), Vector3.up, 2.78f, 0.1f, 18, Dim(stone, 1.12f), true);
            b.Prism(p + new Vector3(0f, 0.55f, 0f), Vector3.up, 2.42f, 0.04f, 18, water, true);
            b.Frustum(p + new Vector3(0f, 1.0f, 0f), Vector3.up, 0.42f, 0.24f, 1.0f, 10, Dim(stone, 1.05f), false, true);
            b.Frustum(p + new Vector3(0f, 1.75f, 0f), Vector3.up, 0.24f, 0.2f, 0.5f, 10, stone, false, false);
            b.Frustum(p + new Vector3(0f, 2.08f, 0f), Vector3.up, 0.22f, 1.05f, 0.34f, 14, Dim(stone, 1.1f), true, true);
            b.Prism(p + new Vector3(0f, 2.23f, 0f), Vector3.up, 0.9f, 0.03f, 14, water, true);
            b.Rod(p + new Vector3(0f, 2.2f, 0f), p + new Vector3(0f, 2.9f, 0f), 0.05f, 6, Dim(stone, 0.8f));
            // чугунные столбики по краю чаши
            for (int i = 0; i < 8; i++)
            {
                float a = i * 0.7853982f;
                Vector3 q = p + new Vector3(Mathf.Cos(a) * 2.78f, 0f, Mathf.Sin(a) * 2.78f);
                b.Prism(q + new Vector3(0f, 0.9f, 0f), Vector3.up, 0.05f, 0.5f, 6, Col(34, 34, 38), true);
            }
        }

        /// <summary>Беседка: деревянный пол, восемь колонн, конусная крыша с фонариком. Вход — со стороны +Z.</summary>
        public static void Gazebo(MeshBatch b, Vector3 p)
        {
            Color32 wood = Col(96, 74, 52);
            Color32 roof = Col(84, 50, 40);
            b.Prism(p + new Vector3(0f, 0.12f, 0f), Vector3.up, 2.5f, 0.24f, 8, Dim(wood, 0.8f), true);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 0.7853982f + 0.3927f;
                Vector3 q = p + new Vector3(Mathf.Cos(a) * 2.15f, 0f, Mathf.Sin(a) * 2.15f);
                b.Prism(q + new Vector3(0f, 1.45f, 0f), Vector3.up, 0.09f, 2.4f, 6, wood, true);
                Vector3 q2 = p + new Vector3(Mathf.Cos(a + 0.7853982f) * 2.15f, 0f, Mathf.Sin(a + 0.7853982f) * 2.15f);
                if (i != 2 && i != 3)   // перила, кроме входа
                    b.Rod(q + new Vector3(0f, 0.95f, 0f), q2 + new Vector3(0f, 0.95f, 0f), 0.035f, 5, wood);
                b.Rod(q + new Vector3(0f, 2.55f, 0f), q2 + new Vector3(0f, 2.55f, 0f), 0.05f, 5, Dim(wood, 0.85f));
            }
            b.Frustum(p + new Vector3(0f, 3.2f, 0f), Vector3.up, 2.9f, 0.12f, 1.25f, 8, roof, true, true);
            b.Prism(p + new Vector3(0f, 4.0f, 0f), Vector3.up, 0.16f, 0.3f, 6, Dim(roof, 0.8f), true);
            b.Rod(p + new Vector3(0f, 4.1f, 0f), p + new Vector3(0f, 4.6f, 0f), 0.025f, 4, Col(40, 40, 44));
        }

        // ------------------------------------------------------------------ детская площадка

        /// <summary>Качели: две А-образные опоры, перекладина, два сиденья на цепях. Ширина 3.6 м, перекладина вдоль оси x.</summary>
        public static void Swing(MeshBatch b, Vector3 p, float yaw)
        {
            Color32 pole = Col(150, 54, 40);
            Color32 chain = Col(70, 70, 76);
            for (int e = -1; e <= 1; e += 2)
            {
                LRod(b, p, yaw, new Vector3(e * 1.8f, 0f, -0.8f), new Vector3(e * 1.8f, 2.4f, 0f), 0.06f, pole);
                LRod(b, p, yaw, new Vector3(e * 1.8f, 0f, 0.8f), new Vector3(e * 1.8f, 2.4f, 0f), 0.06f, pole);
            }
            LRod(b, p, yaw, new Vector3(-1.85f, 2.4f, 0f), new Vector3(1.85f, 2.4f, 0f), 0.07f, Dim(pole, 0.9f));
            for (int s = 0; s < 2; s++)
            {
                float x = -0.7f + s * 1.4f;
                LRod(b, p, yaw, new Vector3(x - 0.2f, 2.4f, 0f), new Vector3(x - 0.2f, 0.55f, 0.15f), 0.012f, chain);
                LRod(b, p, yaw, new Vector3(x + 0.2f, 2.4f, 0f), new Vector3(x + 0.2f, 0.55f, 0.15f), 0.012f, chain);
                LB(b, p, yaw, new Vector3(x, 0.52f, 0.15f), new Vector3(0.46f, 0.05f, 0.2f), Col(34, 70, 110));
            }
        }

        /// <summary>Горка: башенка с площадкой, лесенка и спуск (вдоль +Z).</summary>
        public static void Slide(MeshBatch b, Vector3 p, float yaw)
        {
            Color32 pole = Col(40, 80, 130);
            Color32 slide = Col(180, 150, 40);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                    LRod(b, p, yaw, new Vector3(sx * 0.5f, 0f, -1.0f + sz * 0.5f), new Vector3(sx * 0.5f, 1.5f, -1.0f + sz * 0.5f), 0.05f, pole);
            }
            LB(b, p, yaw, new Vector3(0f, 1.45f, -1.0f), new Vector3(1.1f, 0.08f, 1.1f), Col(120, 90, 50));
            for (int k = 0; k < 5; k++)
                LRod(b, p, yaw, new Vector3(-0.3f, 0.2f + k * 0.28f, -1.65f - k * 0.04f), new Vector3(0.3f, 0.2f + k * 0.28f, -1.65f - k * 0.04f), 0.025f, pole);
            // спуск
            Vector3 a0 = p + Models.RotY(new Vector3(-0.28f, 1.5f, -0.45f), yaw);
            Vector3 a1 = p + Models.RotY(new Vector3(0.28f, 1.5f, -0.45f), yaw);
            Vector3 a2 = p + Models.RotY(new Vector3(0.28f, 0.22f, 1.7f), yaw);
            Vector3 a3 = p + Models.RotY(new Vector3(-0.28f, 0.22f, 1.7f), yaw);
            Quad2(b, a0, a1, a2, a3, slide);
            LRod(b, p, yaw, new Vector3(-0.3f, 1.6f, -0.45f), new Vector3(-0.3f, 0.32f, 1.7f), 0.03f, pole);
            LRod(b, p, yaw, new Vector3(0.3f, 1.6f, -0.45f), new Vector3(0.3f, 0.32f, 1.7f), 0.03f, pole);
            LRod(b, p, yaw, new Vector3(-0.5f, 1.5f, -0.5f), new Vector3(-0.5f, 2.2f, -0.5f), 0.03f, pole);
            LRod(b, p, yaw, new Vector3(0.5f, 1.5f, -0.5f), new Vector3(0.5f, 2.2f, -0.5f), 0.03f, pole);
            LB(b, p, yaw, new Vector3(0f, 2.22f, -0.5f), new Vector3(1.2f, 0.05f, 0.06f), pole);
        }

        /// <summary>Песочница: деревянный борт и песок, ведёрко.</summary>
        public static void Sandbox(MeshBatch b, Vector3 p, float yaw)
        {
            Color32 wood = Col(110, 80, 50);
            LB(b, p, yaw, new Vector3(0f, 0.15f, 1.2f), new Vector3(2.6f, 0.3f, 0.12f), wood);
            LB(b, p, yaw, new Vector3(0f, 0.15f, -1.2f), new Vector3(2.6f, 0.3f, 0.12f), wood);
            LB(b, p, yaw, new Vector3(1.25f, 0.15f, 0f), new Vector3(0.12f, 0.3f, 2.4f), wood);
            LB(b, p, yaw, new Vector3(-1.25f, 0.15f, 0f), new Vector3(0.12f, 0.3f, 2.4f), wood);
            LB(b, p, yaw, new Vector3(0f, 0.12f, 0f), new Vector3(2.4f, 0.2f, 2.3f), Col(146, 126, 88));
            LP(b, p, yaw, new Vector3(0.4f, 0.3f, 0.3f), Vector3.up, 0.1f, 0.18f, 8, Col(190, 60, 50), true);
            LB(b, p, yaw, new Vector3(-0.5f, 0.24f, -0.4f), new Vector3(0.5f, 0.2f, 0.4f), Col(150, 130, 92));
        }

        /// <summary>Карусель: диск на оси с поручнями.</summary>
        public static void MerryGoRound(MeshBatch b, Vector3 p)
        {
            b.Prism(p + new Vector3(0f, 0.28f, 0f), Vector3.up, 1.0f, 0.1f, 12, Col(60, 110, 150), true);
            b.Prism(p + new Vector3(0f, 0.12f, 0f), Vector3.up, 0.12f, 0.24f, 6, Col(50, 50, 56), false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.5708f;
                Vector3 q = p + new Vector3(Mathf.Cos(a) * 0.55f, 0f, Mathf.Sin(a) * 0.55f);
                b.Rod(q + new Vector3(0f, 0.3f, 0f), q + new Vector3(0f, 0.85f, 0f), 0.02f, 4, Col(180, 60, 50));
            }
            b.Rod(p + new Vector3(0.55f, 0.85f, 0f), p + new Vector3(0f, 0.85f, 0.55f), 0.02f, 4, Col(180, 60, 50));
            b.Rod(p + new Vector3(0f, 0.85f, 0.55f), p + new Vector3(-0.55f, 0.85f, 0f), 0.02f, 4, Col(180, 60, 50));
            b.Rod(p + new Vector3(-0.55f, 0.85f, 0f), p + new Vector3(0f, 0.85f, -0.55f), 0.02f, 4, Col(180, 60, 50));
            b.Rod(p + new Vector3(0f, 0.85f, -0.55f), p + new Vector3(0.55f, 0.85f, 0f), 0.02f, 4, Col(180, 60, 50));
        }

        // ------------------------------------------------------------------ уличная мебель

        public static void CafeSet(MeshBatch b, Vector3 p, float yaw, System.Random rng, Color32 umbrella, bool withUmbrella)
        {
            Color32 metal = Col(44, 44, 50);
            Color32 top = Col(118, 100, 78);
            LP(b, p, yaw, new Vector3(0f, 0.74f, 0f), Vector3.up, 0.46f, 0.04f, 10, top, true);
            LP(b, p, yaw, new Vector3(0f, 0.37f, 0f), Vector3.up, 0.04f, 0.74f, 6, metal, false);
            LP(b, p, yaw, new Vector3(0f, 0.02f, 0f), Vector3.up, 0.24f, 0.04f, 8, metal, true);
            for (int i = 0; i < 2; i++)
            {
                float a = (i == 0 ? 0.6f : 3.4f) + Rng(rng, -0.4f, 0.4f);
                Vector3 c = new Vector3(Mathf.Cos(a) * 0.72f, 0f, Mathf.Sin(a) * 0.72f);
                LB(b, p, yaw, c + new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.04f, 0.42f), Dim(top, 0.9f));
                LB(b, p, yaw, c + new Vector3(0.18f * Mathf.Cos(a), 0.72f, 0.18f * Mathf.Sin(a)), new Vector3(0.36f, 0.4f, 0.04f), Dim(top, 0.8f));
                for (int l = 0; l < 4; l++)
                {
                    float lx = (l % 2 == 0 ? -0.17f : 0.17f);
                    float lz = (l < 2 ? -0.17f : 0.17f);
                    LRod(b, p, yaw, c + new Vector3(lx, 0f, lz), c + new Vector3(lx, 0.44f, lz), 0.014f, metal);
                }
            }
            if (withUmbrella)
            {
                LRod(b, p, yaw, new Vector3(0f, 0.04f, 0f), new Vector3(0f, 2.35f, 0f), 0.025f, metal);
                b.Frustum(p + Models.RotY(new Vector3(0f, 2.3f, 0f), yaw), Vector3.up, 1.35f, 0.05f, 0.4f, 8, umbrella, true, true);
            }
        }

        public static void Mailbox(MeshBatch b, Vector3 p, float yaw)
        {
            Color32 blue = Col(34, 62, 112);
            LB(b, p, yaw, new Vector3(-0.2f, 0.45f, 0f), new Vector3(0.06f, 0.9f, 0.06f), Col(40, 40, 44));
            LB(b, p, yaw, new Vector3(0.2f, 0.45f, 0f), new Vector3(0.06f, 0.9f, 0.06f), Col(40, 40, 44));
            LB(b, p, yaw, new Vector3(0f, 1.05f, 0f), new Vector3(0.62f, 0.5f, 0.36f), blue);
            LP(b, p, yaw, new Vector3(0f, 1.3f, 0f), Vector3.right, 0.18f, 0.62f, 8, Dim(blue, 1.1f), true);
            LB(b, p, yaw, new Vector3(0f, 1.1f, 0.19f), new Vector3(0.34f, 0.05f, 0.02f), Col(10, 10, 12));
            LB(b, p, yaw, new Vector3(0f, 0.9f, 0.19f), new Vector3(0.2f, 0.12f, 0.02f), Col(200, 190, 120));
        }

        public static void PhoneBooth(MeshBatch b, Vector3 p, float yaw)
        {
            Color32 red = Col(130, 30, 28);
            Color32 glass = Col(20, 30, 34);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                    LB(b, p, yaw, new Vector3(sx * 0.42f, 1.15f, sz * 0.42f), new Vector3(0.08f, 2.3f, 0.08f), red);
            }
            LB(b, p, yaw, new Vector3(0f, 2.34f, 0f), new Vector3(0.98f, 0.14f, 0.98f), Dim(red, 0.9f));
            LB(b, p, yaw, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.4f, 0.9f), Dim(red, 0.8f));
            LB(b, p, yaw, new Vector3(0f, 1.3f, -0.4f), new Vector3(0.8f, 1.9f, 0.04f), glass);
            LB(b, p, yaw, new Vector3(0.4f, 1.3f, 0f), new Vector3(0.04f, 1.9f, 0.8f), glass);
            LB(b, p, yaw, new Vector3(-0.4f, 1.3f, 0f), new Vector3(0.04f, 1.9f, 0.8f), glass);
            LB(b, p, yaw, new Vector3(0f, 0.8f, 0.42f), new Vector3(0.8f, 0.04f, 0.04f), red);
            LB(b, p, yaw, new Vector3(0f, 1.35f, 0.42f), new Vector3(0.8f, 0.04f, 0.04f), red);
            LB(b, p, yaw, new Vector3(0f, 1.9f, 0.42f), new Vector3(0.8f, 0.04f, 0.04f), red);
            LB(b, p, yaw, new Vector3(0f, 1.15f, -0.36f), new Vector3(0.22f, 0.3f, 0.06f), Col(70, 72, 70));
        }

        /// <summary>Велопарковка: две дуги и ржавый велосипед.</summary>
        public static void BikeRack(MeshBatch b, Vector3 p, float yaw)
        {
            Color32 metal = Col(60, 62, 66);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.7f + i * 0.7f;
                LRod(b, p, yaw, new Vector3(x, 0f, 0f), new Vector3(x, 0.75f, 0f), 0.025f, metal);
                LRod(b, p, yaw, new Vector3(x, 0.75f, 0f), new Vector3(x + 0.0f, 0.75f, 0.4f), 0.025f, metal);
                LRod(b, p, yaw, new Vector3(x, 0.75f, 0.4f), new Vector3(x, 0f, 0.4f), 0.025f, metal);
            }
            Color32 frame = Col(110, 56, 36);
            LP(b, p, yaw, new Vector3(-0.7f, 0.33f, 0.2f), Vector3.forward, 0.33f, 0.03f, 10, Col(20, 20, 22), false);
            LP(b, p, yaw, new Vector3(-0.7f, 0.33f, -0.3f), Vector3.forward, 0.33f, 0.03f, 10, Col(20, 20, 22), false);
            LRod(b, p, yaw, new Vector3(-0.7f, 0.33f, -0.3f), new Vector3(-0.7f, 0.7f, -0.05f), 0.02f, frame);
            LRod(b, p, yaw, new Vector3(-0.7f, 0.7f, -0.05f), new Vector3(-0.7f, 0.33f, 0.2f), 0.02f, frame);
            LRod(b, p, yaw, new Vector3(-0.7f, 0.7f, -0.05f), new Vector3(-0.7f, 0.76f, 0.15f), 0.02f, frame);
            LRod(b, p, yaw, new Vector3(-0.8f, 0.9f, 0.2f), new Vector3(-0.6f, 0.9f, 0.2f), 0.015f, metal);
        }

        /// <summary>Светофор на столбе: три погасшие линзы (свет отключён). yaw — куда смотрят линзы.</summary>
        public static void TrafficLight(MeshBatch b, Vector3 p, float yaw)
        {
            Color32 pole = Col(50, 52, 56);
            b.Prism(p + new Vector3(0f, 1.9f, 0f), Vector3.up, 0.07f, 3.8f, 6, pole, true);
            LB(b, p, yaw, new Vector3(0f, 3.4f, 0.14f), new Vector3(0.34f, 1.0f, 0.22f), Col(26, 26, 30));
            Color32[] lens = { Col(78, 18, 16), Col(72, 58, 14), Col(14, 56, 28) };
            for (int i = 0; i < 3; i++)
                LP(b, p, yaw, new Vector3(0f, 3.68f - i * 0.28f, 0.26f), Vector3.forward, 0.08f, 0.05f, 8, lens[i], true);
            LB(b, p, yaw, new Vector3(0f, 3.4f, 0.03f), new Vector3(0.5f, 1.2f, 0.04f), Col(16, 16, 18));
        }

        /// <summary>Газетный киоск: будка с окном, козырьком и стопками журналов. +Z — окно.</summary>
        public static void Kiosk(MeshBatch b, Vector3 p, float yaw, Color32 body, System.Random rng)
        {
            LB(b, p, yaw, new Vector3(0f, 1.15f, 0f), new Vector3(1.8f, 2.3f, 1.5f), body);
            LB(b, p, yaw, new Vector3(0f, 2.38f, 0f), new Vector3(2.0f, 0.16f, 1.7f), Dim(body, 0.7f));
            LB(b, p, yaw, new Vector3(0f, 1.55f, 0.76f), new Vector3(1.3f, 0.9f, 0.04f), Col(16, 20, 24));
            LB(b, p, yaw, new Vector3(0f, 1.05f, 0.9f), new Vector3(1.5f, 0.06f, 0.4f), Dim(body, 0.55f));
            Color32[] mags = { Col(160, 50, 44), Col(50, 90, 140), Col(200, 170, 50), Col(60, 120, 70), Col(200, 200, 190) };
            for (int i = 0; i < 6; i++)
                LB(b, p, yaw, new Vector3(-0.6f + i * 0.24f, 1.12f, 0.92f), new Vector3(0.2f, 0.04f + 0.01f * i % 3, 0.26f), mags[rng.Next(mags.Length)]);
            LB(b, p, yaw, new Vector3(0f, 2.05f, 0.82f), new Vector3(1.4f, 0.5f, 0.06f), Dim(body, 0.8f));
        }

        public static void Vending(MeshBatch b, Vector3 p, float yaw, Color32 body)
        {
            LB(b, p, yaw, new Vector3(0f, 0.9f, 0f), new Vector3(0.95f, 1.8f, 0.8f), body);
            LB(b, p, yaw, new Vector3(-0.12f, 1.15f, 0.41f), new Vector3(0.55f, 1.1f, 0.03f), Col(18, 24, 28));
            LB(b, p, yaw, new Vector3(0.3f, 1.2f, 0.41f), new Vector3(0.22f, 0.7f, 0.03f), Dim(body, 0.7f));
            LB(b, p, yaw, new Vector3(0f, 0.25f, 0.41f), new Vector3(0.5f, 0.14f, 0.03f), Col(10, 10, 12));
        }

        /// <summary>Рекламная тумба-«афишная»: цилиндр (на нём плакаты) и крыша.</summary>
        public static void PosterColumn(MeshBatch b, Vector3 p)
        {
            b.Prism(p + new Vector3(0f, 1.2f, 0f), Vector3.up, 0.6f, 2.4f, 12, Col(70, 68, 62), false);
            b.Prism(p + new Vector3(0f, 2.5f, 0f), Vector3.up, 0.7f, 0.22f, 12, Col(44, 44, 48), true);
            b.Frustum(p + new Vector3(0f, 2.75f, 0f), Vector3.up, 0.66f, 0.12f, 0.3f, 12, Col(50, 50, 54), false, true);
            b.Prism(p + new Vector3(0f, 0.06f, 0f), Vector3.up, 0.7f, 0.12f, 12, Col(44, 44, 48), true);
        }

        // ------------------------------------------------------------------ провода, бельё, плющ

        /// <summary>Провисающий провод между двумя точками: цепочка стержней по параболе.</summary>
        public static void Wire(MeshBatch b, Vector3 a, Vector3 c, float sag, Color32 col)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt((c - a).magnitude / 2.2f), 4, 12);
            Vector3 prev = a;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 q = Vector3.Lerp(a, c, t);
                q.y -= sag * 4f * t * (1f - t);
                b.Rod(prev, q, 0.018f, 4, col);
                prev = q;
            }
        }

        /// <summary>Верёвка с бельём между двумя точками (стена — стена): простыни и рубашки разных цветов.</summary>
        public static void Laundry(MeshBatch b, Vector3 a, Vector3 c, System.Random rng)
        {
            Wire(b, a, c, 0.18f, Col(120, 110, 96));
            Color32[] cl = { Col(190, 186, 176), Col(150, 60, 56), Col(60, 90, 140), Col(190, 170, 70), Col(100, 130, 90), Col(200, 150, 150) };
            Vector3 d = c - a;
            float len = d.magnitude;
            Vector3 dir = d / len;
            int n = Mathf.Clamp((int)(len / 0.9f), 3, 6);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.7f) / (n + 0.4f);
                Vector3 q = Vector3.Lerp(a, c, t);
                q.y -= 0.18f * 4f * t * (1f - t);
                float w = Rng(rng, 0.4f, 0.7f);
                float h = Rng(rng, 0.45f, 0.9f);
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                b.BoxYaw(q + new Vector3(0f, -h * 0.5f - 0.03f, 0f), new Vector3(0.03f, h, w), yaw + 90f, cl[rng.Next(cl.Length)], ALL);
            }
        }

        /// <summary>Лист плюща: плоский ромб, лежащий на стене и повёрнутый на случайный угол (виден только снаружи).</summary>
        static void Leaf(MeshBatch b, Vector3 c, Vector3 vr, float size, Color32 col, System.Random rng)
        {
            float th = Rnd(rng) * 6.2831853f;
            Vector3 u = Vector3.up * Mathf.Cos(th) + vr * Mathf.Sin(th);
            Vector3 v = vr * Mathf.Cos(th) - Vector3.up * Mathf.Sin(th);
            float l = size;
            float w = size * 0.62f;
            b.Quad(c - u * l, c - v * w, c + u * l, c + v * w, col);
        }

        /// <summary>
        /// Плющ на стене: несколько тонких лоз, ползущих вверх с изгибами, и россыпь плоских листьев вдоль них
        /// (листья прижаты к стене, не торчат шарами). n — нормаль стены (наружу).
        /// </summary>
        public static void Ivy(MeshBatch b, Vector3 wallPoint, Vector3 n, Vector3 t, float height, System.Random rng)
        {
            Vector3 vr = Vector3.Cross(n, Vector3.up).normalized;   // «вправо» для того, кто смотрит на стену
            Color32 stem = Col(40, 44, 30);
            Color32[] leaves = { Col(40, 68, 34), Col(50, 78, 38), Col(34, 58, 32), Col(60, 86, 42), Col(74, 82, 38) };
            int vines = 2 + rng.Next(3);
            for (int v = 0; v < vines; v++)
            {
                float x = Rng(rng, -1.7f, 1.7f);
                float h = height * Rng(rng, 0.5f, 1f);
                Vector3 prev = wallPoint + vr * x + n * 0.035f;
                float y = 0f;
                while (y < h)
                {
                    y += Rng(rng, 0.35f, 0.6f);
                    x += Rng(rng, -0.14f, 0.14f);
                    Vector3 q = wallPoint + vr * x + Vector3.up * y + n * 0.035f;
                    // стебель: узкая лента вдоль лозы
                    float sw = 0.016f;
                    b.Quad(prev - vr * sw, q - vr * sw, q + vr * sw, prev + vr * sw, stem);
                    int cnt = 3 + rng.Next(3);
                    float thin = 1f - 0.5f * (y / Mathf.Max(h, 0.1f));   // выше — листьев меньше
                    for (int k = 0; k < cnt; k++)
                    {
                        if (Rnd(rng) > thin + 0.25f) continue;
                        Vector3 c = Vector3.Lerp(prev, q, (k + 0.5f) / cnt) + vr * Rng(rng, -0.17f, 0.17f) + n * Rng(rng, 0.005f, 0.04f);
                        Leaf(b, c, vr, Rng(rng, 0.08f, 0.15f), Jit(leaves[rng.Next(leaves.Length)], rng, 0.18f), rng);
                    }
                    prev = q;
                }
            }
        }

        // ------------------------------------------------------------------ вывески и плакаты

        /// <summary>Лицевая сторона вывески/плаката с центром в c, смотрит по горизонтальной нормали n.</summary>
        public static void SignQuad(MeshBatch sb, Vector3 c, Vector3 n, float w, float h, Vector2 uv0, Vector2 uv1, Color32 tint)
        {
            Vector3 vr = Vector3.Cross(n, Vector3.up).normalized * (w * 0.5f);
            Vector3 vu = Vector3.up * (h * 0.5f);
            sb.QuadUV(c - vr - vu, c - vr + vu, c + vr + vu, c + vr - vu,
                new Vector2(uv0.x, uv0.y), new Vector2(uv0.x, uv1.y), new Vector2(uv1.x, uv1.y), new Vector2(uv1.x, uv0.y), tint);
        }

        /// <summary>Вывеска с текстом на стене: тёмный короб-основание и доска с буквами. maxW — наибольшая ширина, м.</summary>
        public static void WallSign(MeshBatch pb, MeshBatch sb, Vector3 center, Vector3 n, string text, Color32 board, Color32 ink, int scale, float height, float maxW)
        {
            Vector2 uv0, uv1;
            float aspect;
            SignAtlas.Sign(text, board, ink, scale, out uv0, out uv1, out aspect);
            float h = height;
            float w = h * aspect;
            if (w > maxW)
            {
                w = maxW;
                h = w / aspect;
            }
            float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
            pb.BoxYaw(center + n * 0.05f, new Vector3(w + 0.16f, h + 0.16f, 0.1f), yaw, Col(26, 24, 24), ALL);
            SignQuad(sb, center + n * 0.103f, n, w, h, uv0, uv1, new Color32(255, 255, 255, 255));
        }

        /// <summary>Подвесная вывеска, перпендикулярная стене: читается с обеих сторон улицы.</summary>
        public static void BladeSign(MeshBatch pb, MeshBatch sb, Vector3 wallPoint, Vector3 n, Vector3 along, string text, Color32 board, Color32 ink, float height)
        {
            Vector2 uv0, uv1;
            float aspect;
            SignAtlas.Sign(text, board, ink, 2, out uv0, out uv1, out aspect);
            float h = height;
            float w = Mathf.Min(h * aspect, 1.4f);
            h = w / aspect;
            Vector3 c = wallPoint + n * (0.15f + w * 0.5f);
            pb.Rod(wallPoint + Vector3.up * (h * 0.5f + 0.1f), wallPoint + n * (w + 0.3f) + Vector3.up * (h * 0.5f + 0.1f), 0.025f, 4, Col(40, 40, 44));
            pb.BoxYaw(c, new Vector3(0.1f, h + 0.1f, w + 0.1f), Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg + 90f, Col(26, 24, 24), ALL);
            // две лицевые стороны (нормали +along и -along)
            SignQuad(sb, c + along * 0.055f, along, w, h, uv0, uv1, new Color32(255, 255, 255, 255));
            SignQuad(sb, c - along * 0.055f, -along, w, h, uv0, uv1, new Color32(255, 255, 255, 255));
        }

        /// <summary>Афиша/плакат на стене.</summary>
        public static void PosterOnWall(MeshBatch sb, Vector3 center, Vector3 n, int variant, float height)
        {
            Vector2 uv0, uv1;
            float aspect;
            SignAtlas.Poster(variant, out uv0, out uv1, out aspect);
            SignQuad(sb, center + n * 0.03f, n, height * aspect, height, uv0, uv1, new Color32(255, 255, 255, 255));
        }

        /// <summary>Складная уличная доска-«меню» с текстом: две створки и доска.</summary>
        public static void SandwichBoard(MeshBatch pb, MeshBatch sb, Vector3 p, float yaw, string text, Color32 board, Color32 ink)
        {
            Color32 wood = Col(90, 66, 44);
            Vector3 n = Models.RotY(Vector3.forward, yaw);
            LRod(pb, p, yaw, new Vector3(-0.3f, 0f, 0.18f), new Vector3(-0.28f, 0.95f, 0.03f), 0.025f, wood);
            LRod(pb, p, yaw, new Vector3(0.3f, 0f, 0.18f), new Vector3(0.28f, 0.95f, 0.03f), 0.025f, wood);
            LRod(pb, p, yaw, new Vector3(-0.3f, 0f, -0.18f), new Vector3(-0.28f, 0.95f, -0.03f), 0.025f, wood);
            LRod(pb, p, yaw, new Vector3(0.3f, 0f, -0.18f), new Vector3(0.28f, 0.95f, -0.03f), 0.025f, wood);
            LB(pb, p, yaw, new Vector3(0f, 0.6f, 0f), new Vector3(0.62f, 0.62f, 0.08f), Dim(wood, 0.8f));
            Vector2 uv0, uv1;
            float aspect;
            SignAtlas.Sign(text, board, ink, 2, out uv0, out uv1, out aspect);
            float w = 0.56f;
            float h = Mathf.Min(0.5f, w / aspect);
            SignQuad(sb, p + n * 0.045f + Vector3.up * 0.62f, n, w, h, uv0, uv1, new Color32(255, 255, 255, 255));
            SignQuad(sb, p - n * 0.045f + Vector3.up * 0.62f, -n, w, h, uv0, uv1, new Color32(255, 255, 255, 255));
        }

        // ------------------------------------------------------------------ витрины

        /// <summary>Манекен в одежде.</summary>
        public static void Mannequin(MeshBatch b, Vector3 p, float yaw, Color32 cloth, bool dress, System.Random rng)
        {
            Color32 skin = Col(176, 168, 158);
            LP(b, p, yaw, new Vector3(0f, 0.025f, 0f), Vector3.up, 0.2f, 0.05f, 8, Col(40, 40, 44), true);
            LRod(b, p, yaw, new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.95f, 0f), 0.025f, Col(60, 60, 64));
            if (dress)
            {
                b.Frustum(p + Models.RotY(new Vector3(0f, 0.85f, 0f), yaw), Vector3.up, 0.26f, 0.12f, 0.8f, 8, cloth, false, false);
                LB(b, p, yaw, new Vector3(0f, 1.35f, 0f), new Vector3(0.3f, 0.45f, 0.18f), cloth);
            }
            else
            {
                LB(b, p, yaw, new Vector3(0f, 1.25f, 0f), new Vector3(0.34f, 0.7f, 0.2f), cloth);
                LB(b, p, yaw, new Vector3(0f, 0.7f, 0f), new Vector3(0.32f, 0.5f, 0.18f), Col(40, 44, 60));
            }
            LRod(b, p, yaw, new Vector3(-0.2f, 1.5f, 0f), new Vector3(-0.26f, 1.05f, 0.04f), 0.03f, cloth);
            LRod(b, p, yaw, new Vector3(0.2f, 1.5f, 0f), new Vector3(0.26f, 1.05f, 0.04f), 0.03f, cloth);
            Blob(b, p + Models.RotY(new Vector3(0f, 1.68f, 0f), yaw), 0.1f, 0.13f, 0.1f, skin, rng, 6, 4, 0.04f);
        }

        /// <summary>
        /// Предмет на полке витрины: kind — тип магазина (0 продукты, 1 хлеб, 2 аптека, 3 овощи, 4 мясо, 5 кафе, 6 бар, 7 цветы,
        /// 8 книги, 9 обувь, 10 одежда, 11 часы, 12 почта, 13 игрушки, 14 рыба, 15 парикмахерская). Возвращает ширину предмета.
        /// </summary>
        static float ShelfItem(MeshBatch b, Vector3 o, float yaw, int kind, Vector3 at, System.Random rng)
        {
            Color32[] bright = { Col(170, 56, 48), Col(60, 100, 150), Col(190, 160, 50), Col(70, 130, 80), Col(190, 190, 180), Col(150, 80, 140) };
            Color32 c = bright[rng.Next(bright.Length)];
            switch (kind)
            {
                case 0:   // консервы с этикетками, банки с крышками, пачки
                    {
                        int r = rng.Next(4);
                        if (r <= 1)
                        {
                            LP(b, o, yaw, at + new Vector3(0f, 0.065f, 0f), Vector3.up, 0.05f, 0.13f, 7, Dim(c, 0.9f), true);
                            LP(b, o, yaw, at + new Vector3(0f, 0.065f, 0f), Vector3.up, 0.0515f, 0.06f, 7, Col(214, 206, 176), false);
                            return 0.115f;
                        }
                        if (r == 2)
                        {
                            LP(b, o, yaw, at + new Vector3(0f, 0.06f, 0f), Vector3.up, 0.045f, 0.12f, 7, Col(150, 112, 62), true);
                            LP(b, o, yaw, at + new Vector3(0f, 0.13f, 0f), Vector3.up, 0.048f, 0.025f, 7, c, true);
                            return 0.11f;
                        }
                        LB(b, o, yaw, at + new Vector3(0f, 0.1f, 0f), new Vector3(0.17f, 0.2f, 0.09f), Dim(c, 0.85f));
                        LB(b, o, yaw, at + new Vector3(0f, 0.1f, 0.047f), new Vector3(0.15f, 0.08f, 0.01f), Col(228, 220, 190));
                        return 0.19f;
                    }
                case 1:   // булки и батоны
                    {
                        Color32 crust = Col(150 + rng.Next(30), 100 + rng.Next(20), 54);
                        if (rng.Next(3) == 0) { LP(b, o, yaw, at + new Vector3(0f, 0.04f, 0f), Vector3.right, 0.045f, 0.4f, 6, crust, true); return 0.44f; }
                        LB(b, o, yaw, at + new Vector3(0f, 0.07f, 0f), new Vector3(0.28f, 0.14f, 0.16f), crust);
                        LB(b, o, yaw, at + new Vector3(0f, 0.15f, 0f), new Vector3(0.22f, 0.04f, 0.12f), Dim(crust, 0.8f));
                        return 0.32f;
                    }
                case 2:   // коробочки лекарств
                    LB(b, o, yaw, at + new Vector3(0f, 0.06f, 0f), new Vector3(0.12f, 0.12f, 0.06f), Col(214, 214, 206));
                    LB(b, o, yaw, at + new Vector3(0f, 0.06f, 0.032f), new Vector3(0.12f, 0.04f, 0.01f), rng.Next(2) == 0 ? Col(40, 120, 70) : Col(50, 90, 150));
                    return 0.14f;
                case 3:   // ящики с овощами и фруктами
                    {
                        LB(b, o, yaw, at + new Vector3(0f, 0.05f, 0f), new Vector3(0.42f, 0.1f, 0.3f), Col(110, 84, 52));
                        Color32[] veg = { Col(170, 50, 40), Col(200, 120, 30), Col(70, 130, 50), Col(190, 170, 50) };
                        Color32 v = veg[rng.Next(veg.Length)];
                        for (int i = 0; i < 5; i++)
                            Blob(b, o + Models.RotY(at + new Vector3(Rng(rng, -0.14f, 0.14f), 0.14f, Rng(rng, -0.08f, 0.08f)), yaw), 0.07f, 0.07f, 0.07f, v, rng, 5, 3, 0.08f);
                        return 0.46f;
                    }
                case 4:   // мясо: куски на лотке
                    LB(b, o, yaw, at + new Vector3(0f, 0.04f, 0f), new Vector3(0.34f, 0.08f, 0.22f), Col(170, 66, 62));
                    LB(b, o, yaw, at + new Vector3(0.04f, 0.1f, 0f), new Vector3(0.18f, 0.06f, 0.14f), Col(200, 120, 110));
                    return 0.38f;
                case 5:   // пироги и чашки
                    if (rng.Next(2) == 0)
                    {
                        LP(b, o, yaw, at + new Vector3(0f, 0.05f, 0f), Vector3.up, 0.13f, 0.1f, 10, Col(200, 160, 110), true);
                        LP(b, o, yaw, at + new Vector3(0f, 0.11f, 0f), Vector3.up, 0.11f, 0.03f, 10, rng.Next(2) == 0 ? Col(190, 100, 130) : Col(90, 56, 40), true);
                        return 0.3f;
                    }
                    LP(b, o, yaw, at + new Vector3(0f, 0.04f, 0f), Vector3.up, 0.04f, 0.08f, 7, Col(220, 220, 214), true);
                    return 0.12f;
                case 6:   // бутылки
                    {
                        Color32[] gl = { Col(40, 90, 50), Col(120, 70, 26), Col(150, 160, 160), Col(90, 30, 40) };
                        Color32 g = gl[rng.Next(gl.Length)];
                        LP(b, o, yaw, at + new Vector3(0f, 0.12f, 0f), Vector3.up, 0.04f, 0.24f, 6, g, true);
                        LP(b, o, yaw, at + new Vector3(0f, 0.3f, 0f), Vector3.up, 0.018f, 0.12f, 5, g, true);
                        return 0.1f;
                    }
                case 7:   // вазы с цветами
                    {
                        Color32[] fl = { Col(176, 52, 48), Col(190, 158, 44), Col(128, 70, 148), Col(206, 200, 186), Col(196, 104, 132) };
                        LP(b, o, yaw, at + new Vector3(0f, 0.09f, 0f), Vector3.up, 0.07f, 0.18f, 7, Col(100, 110, 120), true);
                        for (int i = 0; i < 4; i++)
                            Blob(b, o + Models.RotY(at + new Vector3(Rng(rng, -0.07f, 0.07f), 0.24f + Rnd(rng) * 0.1f, Rng(rng, -0.05f, 0.05f)), yaw), 0.07f, 0.06f, 0.07f, fl[rng.Next(fl.Length)], rng, 5, 3, 0.08f);
                        return 0.22f;
                    }
                case 8:   // книги
                    {
                        float w = Rng(rng, 0.035f, 0.07f);
                        float h = Rng(rng, 0.2f, 0.3f);
                        LB(b, o, yaw, at + new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, 0.18f), c);
                        return w + 0.006f;
                    }
                case 9:   // обувь
                    LB(b, o, yaw, at + new Vector3(0f, 0.05f, 0f), new Vector3(0.12f, 0.1f, 0.28f), rng.Next(2) == 0 ? Col(36, 30, 28) : Col(110, 70, 44));
                    LB(b, o, yaw, at + new Vector3(0f, 0.01f, 0f), new Vector3(0.13f, 0.02f, 0.3f), Col(20, 20, 22));
                    return 0.15f;
                case 10:  // сложенная одежда
                    for (int i = 0; i < 3; i++) LB(b, o, yaw, at + new Vector3(0f, 0.04f + i * 0.08f, 0f), new Vector3(0.36f, 0.07f, 0.28f), Jit(c, rng, 0.1f));
                    return 0.4f;
                case 11:  // часы
                    LB(b, o, yaw, at + new Vector3(0f, 0.04f, 0f), new Vector3(0.16f, 0.08f, 0.1f), Col(46, 40, 36));
                    LP(b, o, yaw, at + new Vector3(0f, 0.12f, 0.02f), Vector3.forward, 0.065f, 0.03f, 10, Col(200, 190, 150), true);
                    return 0.2f;
                case 12:  // посылки
                    LB(b, o, yaw, at + new Vector3(0f, 0.1f, 0f), new Vector3(0.28f, 0.2f, 0.2f), Col(150, 116, 78));
                    LB(b, o, yaw, at + new Vector3(0f, 0.1f, 0.101f), new Vector3(0.04f, 0.2f, 0.01f), Col(190, 180, 150));
                    return 0.32f;
                case 13:  // игрушки: кубики и мишка
                    if (rng.Next(3) == 0)
                    {
                        Blob(b, o + Models.RotY(at + new Vector3(0f, 0.1f, 0f), yaw), 0.09f, 0.1f, 0.08f, Col(130, 90, 56), rng, 6, 4, 0.05f);
                        Blob(b, o + Models.RotY(at + new Vector3(0f, 0.22f, 0f), yaw), 0.06f, 0.06f, 0.06f, Col(130, 90, 56), rng, 6, 3, 0.05f);
                        return 0.22f;
                    }
                    LB(b, o, yaw, at + new Vector3(0f, 0.07f, 0f), new Vector3(0.14f, 0.14f, 0.14f), c);
                    return 0.17f;
                case 14:  // рыба на льду
                    LB(b, o, yaw, at + new Vector3(0f, 0.03f, 0f), new Vector3(0.4f, 0.06f, 0.26f), Col(190, 200, 204));
                    LB(b, o, yaw, at + new Vector3(0f, 0.09f, 0f), new Vector3(0.3f, 0.06f, 0.08f), Col(90, 110, 130));
                    LB(b, o, yaw, at + new Vector3(0.18f, 0.09f, 0f), new Vector3(0.06f, 0.1f, 0.01f), Col(80, 100, 120));
                    return 0.44f;
            }
            return 0.15f;
        }

        /// <summary>
        /// Витрина магазина: задняя стенка, три полки с товаром (или манекены), рамы-переплёты без стекла, дверь, ступенька.
        /// o — точка на стене на уровне земли в середине витрины; yaw — наружу; kind — тип (см. ShelfItem).
        /// Ширина 4.2 м, высота до 2.5 м, глубина до 0.5 м.
        /// </summary>
        public static void ShopWindow(MeshBatch b, Vector3 o, float yaw, int kind, Color32 trim, System.Random rng)
        {
            Color32 back = kind == 2 ? Col(150, 156, 152) : (kind == 4 || kind == 14 ? Col(160, 160, 154) : Col(34, 30, 30));
            Color32 shelf = Col(96, 72, 48);
            // стенка, плинтус, пилястры
            LB(b, o, yaw, new Vector3(-0.35f, 1.35f, 0.1f), new Vector3(3.2f, 1.9f, 0.06f), back);
            LB(b, o, yaw, new Vector3(0f, 0.2f, 0.12f), new Vector3(4.2f, 0.4f, 0.24f), Dim(trim, 0.8f));
            LB(b, o, yaw, new Vector3(-2.07f, 1.3f, 0.13f), new Vector3(0.22f, 2.6f, 0.26f), trim);
            LB(b, o, yaw, new Vector3(2.07f, 1.3f, 0.13f), new Vector3(0.22f, 2.6f, 0.26f), trim);
            LB(b, o, yaw, new Vector3(0f, 2.46f, 0.14f), new Vector3(4.36f, 0.22f, 0.28f), trim);
            // дверь справа
            LB(b, o, yaw, new Vector3(1.5f, 1.15f, 0.12f), new Vector3(1.0f, 2.1f, 0.06f), Col(22, 18, 18));
            LB(b, o, yaw, new Vector3(1.5f, 1.15f, 0.16f), new Vector3(0.86f, 1.9f, 0.03f), Col(36, 40, 44));
            LB(b, o, yaw, new Vector3(1.15f, 1.05f, 0.2f), new Vector3(0.05f, 0.18f, 0.04f), Col(150, 140, 100));
            LB(b, o, yaw, new Vector3(1.5f, 0.06f, 0.45f), new Vector3(1.5f, 0.12f, 0.6f), Dim(trim, 0.9f));
            if (kind == 10)
            {
                Color32[] cloth = { Col(150, 50, 60), Col(60, 70, 130), Col(190, 170, 80) };
                for (int i = 0; i < 3; i++)
                    Mannequin(b, o + Models.RotY(new Vector3(-1.4f + i * 1.0f, 0.1f, 0.3f), yaw), yaw, cloth[i], i != 1, rng);
            }
            else if (kind == 15)
            {
                for (int i = 0; i < 2; i++)
                {
                    float x = -1.3f + i * 1.3f;
                    LB(b, o, yaw, new Vector3(x, 1.6f, 0.14f), new Vector3(0.7f, 0.9f, 0.03f), Col(196, 196, 190));
                    LB(b, o, yaw, new Vector3(x, 1.6f, 0.13f), new Vector3(0.8f, 1.0f, 0.02f), Col(40, 36, 34));
                    LB(b, o, yaw, new Vector3(x, 0.42f, 0.4f), new Vector3(0.5f, 0.1f, 0.5f), Col(150, 44, 40));
                    LB(b, o, yaw, new Vector3(x, 0.7f, 0.4f), new Vector3(0.1f, 0.5f, 0.1f), Col(60, 60, 64));
                    LB(b, o, yaw, new Vector3(x, 0.96f, 0.46f), new Vector3(0.5f, 0.45f, 0.06f), Col(150, 44, 40));
                }
            }
            else
            {
                float[] ys = { 0.62f, 1.18f, 1.74f };
                for (int s = 0; s < 3; s++)
                {
                    LB(b, o, yaw, new Vector3(-0.35f, ys[s], 0.34f), new Vector3(3.2f, 0.04f, 0.4f), shelf);
                    float x = -1.9f;
                    int guard = 0;
                    while (x < 1.0f && guard++ < 40)
                    {
                        float w = ShelfItem(b, o, yaw, kind, new Vector3(x + 0.1f, ys[s] + 0.02f, 0.34f), rng);
                        x += w + 0.04f;
                    }
                }
            }
            // переплёты без стекла: рамки поверх товара
            for (int i = 0; i < 4; i++)
                LB(b, o, yaw, new Vector3(-1.9f + i * 1.0f, 1.3f, 0.52f), new Vector3(0.05f, 1.9f, 0.05f), Dim(trim, 0.7f));
            LB(b, o, yaw, new Vector3(-0.35f, 0.46f, 0.52f), new Vector3(3.2f, 0.05f, 0.05f), Dim(trim, 0.7f));
            LB(b, o, yaw, new Vector3(-0.35f, 2.24f, 0.52f), new Vector3(3.2f, 0.05f, 0.05f), Dim(trim, 0.7f));
        }

        /// <summary>Полосатый тент над витриной (шесть полос двух оттенков, наклон, бахрома).</summary>
        public static void StripedAwning(MeshBatch b, Vector3 o, float yaw, float width, Color32 col)
        {
            int n = Mathf.RoundToInt(width / 0.7f);
            float w = width / n;
            for (int i = 0; i < n; i++)
            {
                Color32 c = (i % 2 == 0) ? col : Dim(col, 0.62f);
                float x = -width * 0.5f + (i + 0.5f) * w;
                LB(b, o, yaw, new Vector3(x, 2.68f, 0.62f), new Vector3(w, 0.07f, 1.24f), c);
                LB(b, o, yaw, new Vector3(x, 2.52f, 1.22f), new Vector3(w, 0.26f, 0.04f), c);
            }
        }
    }
}
