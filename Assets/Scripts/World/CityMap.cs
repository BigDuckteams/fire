using System;
using System.Collections.Generic;

namespace PokaGoryu
{
    // Чистый C# без UnityEngine: карту и поиск пути можно тестировать вне Unity.

    public struct Pt
    {
        public float X;
        public float Z;
        public Pt(float x, float z) { X = x; Z = z; }
    }

    public struct CellPos
    {
        public int X;
        public int Z;
        public CellPos(int x, int z) { X = x; Z = z; }
    }

    public struct CellRect
    {
        public int X0;
        public int Z0;
        public int X1;
        public int Z1;   // включительно
        public CellRect(int x0, int z0, int x1, int z1) { X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; }
        public Pt Center
        {
            get { return new Pt((X0 + X1 + 1) * 0.5f * CityMap.Cell, (Z0 + Z1 + 1) * 0.5f * CityMap.Cell); }
        }
        public float SizeX { get { return (X1 - X0 + 1) * CityMap.Cell; } }
        public float SizeZ { get { return (Z1 - Z0 + 1) * CityMap.Cell; } }
    }

    /// <summary>
    /// Карта города на сетке 6x6 м. Всё, что не дорога — здание.
    /// Главный маршрут — широкие улицы (2 клетки), боковые — узкие переулки (1 клетка).
    /// Редактировать город = править Layout(). Карту можно напечатать (ToAscii) и проверить поиск пути.
    /// </summary>
    public static class CityMap
    {
        public const float Cell = 6f;
        public const int W = 40;
        public const int H = 68;

        public const byte Building = 0;
        public const byte Street = 1;
        public const byte Alley = 2;
        public const byte Plaza = 3;
        public const byte Tunnel = 4;

        public static readonly byte[,] Kind = new byte[W, H];
        public static readonly byte[,] District = new byte[W, H];
        public static readonly float[,] DistBus = new float[W, H];
        public static readonly bool[,] Blocked = new bool[W, H];     // клетки с живым наростом Тьмы

        public static int StartX, StartZ, BusX, BusZ;
        public static float MaxDist = 1f;

        public static readonly List<CellRect> Walls = new List<CellRect>();
        public static readonly List<CellPos> Hearths = new List<CellPos>();
        public static readonly List<CellPos> Survivors = new List<CellPos>();
        public static readonly List<CellPos> Generators = new List<CellPos>();
        public static readonly List<CellPos> Pizzas = new List<CellPos>();   // переулки, где лежит пицца (находка; точку внутри клетки выбирает LevelBuilder)
        public static CellRect Station;          // площадь вокзала
        public static CellRect ExitRoad;         // дорога из города (куда уедет автобус)

        static bool built;
        const float Inf = 1e9f;

        static readonly float[] pdist = new float[W * H];
        static readonly int[] pprev = new int[W * H];
        static readonly int[] heapNode = new int[W * H * 5];
        static readonly float[] heapKey = new float[W * H * 5];

        public static void Build()
        {
            if (!built)
            {
                built = true;
                Layout();
                ComputeFields();
            }
            ResetWalls();
        }

        /// <summary>Нарост Тьмы снова на месте (новая игра): клетки стен непроходимы.</summary>
        public static void ResetWalls()
        {
            for (int i = 0; i < Walls.Count; i++)
            {
                CellRect r = Walls[i];
                for (int x = r.X0; x <= r.X1; x++)
                {
                    for (int z = r.Z0; z <= r.Z1; z++) Blocked[x, z] = true;
                }
            }
        }

        // ------------------------------------------------------------------ раскладка города

        static void Fill(int x0, int z0, int x1, int z1, byte k)
        {
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    if (x >= 0 && x < W && z >= 0 && z < H) Kind[x, z] = k;
                }
            }
        }

        static void Wall(int x0, int z0, int x1, int z1)
        {
            Walls.Add(new CellRect(x0, z0, x1, z1));
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++) Blocked[x, z] = true;
            }
        }

        static void Layout()
        {
            // ===== Район 1: жилой. Старт на дворе, дальше на север. =====
            Fill(2, 2, 9, 8, Plaza);          // P0: стартовый двор (очаг)
            Fill(5, 9, 6, 24, Street);        // N1: проспект на север
            Fill(2, 25, 11, 29, Plaza);       // J1: площадь-перекрёсток (очаг)
            Fill(2, 9, 2, 24, Alley);         // L1: западная петля-переулок (двор <-> J1)
            Fill(7, 14, 10, 14, Alley);       // D1b: тупик к востоку от проспекта
            Fill(8, 19, 8, 24, Alley);        // D1a: тупик (человек)
            // Двор-колодец: петля севернее J1
            Fill(4, 30, 4, 38, Alley);
            Fill(4, 38, 9, 38, Alley);
            Fill(9, 30, 9, 38, Alley);

            // ===== Район 2: рынок =====
            Fill(12, 27, 29, 28, Street);     // E1: улица на восток
            Wall(20, 27, 20, 28);             // W1: НАРОСТ ТЬМЫ, обойти нельзя — выжечь лучом
            Fill(16, 29, 16, 31, Alley);      // D2b: тупик-закуток (осколки света)
            Fill(25, 23, 25, 26, Alley);      // D2c: тупик к югу от улицы
            Fill(30, 24, 37, 31, Plaza);      // J2: рыночная площадь (очаг)
            Fill(30, 20, 30, 23, Alley);      // D2a: тупик к югу от площади
            // Петля вокруг квартала к северу от E1
            Fill(22, 29, 22, 33, Alley);
            Fill(22, 33, 27, 33, Alley);
            Fill(27, 29, 27, 33, Alley);

            // ===== Район 3: больница и тоннель =====
            Fill(33, 32, 34, 47, Street);     // N2: на север
            Fill(33, 38, 34, 43, Tunnel);     // тоннель под путями
            Fill(35, 36, 37, 36, Alley);      // G2: ниша с генератором (тупик)
            // Обход тоннеля: узкий переулок (длиннее)
            Fill(28, 34, 32, 34, Alley);
            Fill(28, 34, 28, 46, Alley);
            Fill(28, 46, 32, 46, Alley);
            Fill(30, 48, 37, 53, Plaza);      // J3: площадь-перекрёсток (очаг)

            // ===== Район 4: промзона и вокзал =====
            Fill(14, 50, 29, 51, Street);     // E2: улица на запад
            Wall(22, 50, 22, 51);             // W2: нарост; можно выжечь или обойти переулком
            Fill(16, 46, 16, 49, Alley);      // D4c: тупик к югу
            Fill(21, 52, 21, 53, Alley);      // D4d: закуток внутри петли обхода
            Fill(26, 52, 26, 54, Alley);      // обход W2 (петля севернее)
            Fill(18, 54, 26, 54, Alley);
            Fill(18, 52, 18, 54, Alley);
            Fill(8, 48, 13, 55, Plaza);       // J4: площадь
            Fill(4, 50, 7, 50, Alley);        // D4a: тупик на запад (человек)
            Fill(10, 56, 11, 59, Street);     // N3: к вокзалу
            Fill(3, 60, 18, 65, Plaza);       // ST: площадь автовокзала
            Fill(19, 62, 22, 62, Alley);      // D4b: тупик у вокзала (человек)
            Fill(10, 66, 11, 67, Street);     // дорога из города

            Station = new CellRect(3, 60, 18, 65);
            ExitRoad = new CellRect(10, 66, 11, 67);

            StartX = 5; StartZ = 6;
            BusX = 10; BusZ = 63;

            // Очаги, люди, генератор
            Hearths.Add(new CellPos(4, 7));
            Hearths.Add(new CellPos(3, 26));
            Hearths.Add(new CellPos(31, 25));
            Hearths.Add(new CellPos(31, 49));

            Survivors.Add(new CellPos(6, 13));    // S1: на главной улице у старта — виден с первых шагов (обучение)
            Survivors.Add(new CellPos(2, 17));    // S2: западная петля
            Survivors.Add(new CellPos(8, 20));    // S2: тупик D1a
            Survivors.Add(new CellPos(7, 38));    // S3: двор-колодец
            Survivors.Add(new CellPos(34, 27));   // S4: рыночная площадь
            Survivors.Add(new CellPos(24, 33));   // S5: петля у рынка
            Survivors.Add(new CellPos(37, 36));   // S6: ниша с генератором
            Survivors.Add(new CellPos(28, 40));   // S7: обход тоннеля
            Survivors.Add(new CellPos(4, 50));    // S8: тупик D4a
            Survivors.Add(new CellPos(22, 62));   // S9: тупик у вокзала

            Generators.Add(new CellPos(37, 36));

            // Пицца: по одной в переулках, в стороне от людей и ламп (награда за то, что свернул с проспекта)
            Pizzas.Add(new CellPos(9, 14));       // D1b: тупик у старта
            Pizzas.Add(new CellPos(2, 12));       // L1: западный переулок
            Pizzas.Add(new CellPos(16, 30));      // D2b: закуток у рынка
            Pizzas.Add(new CellPos(25, 25));      // D2c: тупик к югу от улицы
            Pizzas.Add(new CellPos(30, 22));      // D2a: тупик к югу от площади
            Pizzas.Add(new CellPos(28, 44));      // обход тоннеля
            Pizzas.Add(new CellPos(16, 48));      // D4c: тупик к югу
            Pizzas.Add(new CellPos(21, 53));      // D4d: закуток у обхода
        }

        // ------------------------------------------------------------------ служебные поля

        static void ComputeFields()
        {
            // Расстояние до автобуса по дорогам (стены считаем проходимыми: их выжигают)
            for (int x = 0; x < W; x++)
            {
                for (int z = 0; z < H; z++) DistBus[x, z] = Inf;
            }
            Queue<int> q = new Queue<int>();
            DistBus[BusX, BusZ] = 0f;
            q.Enqueue(BusX * H + BusZ);
            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                int n = q.Dequeue();
                int cx = n / H;
                int cz = n % H;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d];
                    int nz = cz + dz[d];
                    if (nx < 0 || nx >= W || nz < 0 || nz >= H) continue;
                    if (Kind[nx, nz] == Building) continue;
                    if (DistBus[nx, nz] < Inf) continue;
                    DistBus[nx, nz] = DistBus[cx, cz] + Cell;
                    q.Enqueue(nx * H + nz);
                }
            }
            MaxDist = DistBus[StartX, StartZ];
            if (MaxDist >= Inf || MaxDist < 1f) MaxDist = 1f;

            // Район по прогрессу; здания наследуют район ближайшей дороги
            bool[,] done = new bool[W, H];
            Queue<int> qd = new Queue<int>();
            for (int x = 0; x < W; x++)
            {
                for (int z = 0; z < H; z++)
                {
                    if (Kind[x, z] == Building) continue;
                    float p = ProgressOfCell(x, z);
                    byte dist = p < 0.28f ? (byte)0 : (p < 0.52f ? (byte)1 : (p < 0.78f ? (byte)2 : (byte)3));
                    District[x, z] = dist;
                    done[x, z] = true;
                    qd.Enqueue(x * H + z);
                }
            }
            while (qd.Count > 0)
            {
                int n = qd.Dequeue();
                int cx = n / H;
                int cz = n % H;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d];
                    int nz = cz + dz[d];
                    if (nx < 0 || nx >= W || nz < 0 || nz >= H) continue;
                    if (done[nx, nz]) continue;
                    done[nx, nz] = true;
                    District[nx, nz] = District[cx, cz];
                    qd.Enqueue(nx * H + nz);
                }
            }
        }

        static float ProgressOfCell(int x, int z)
        {
            float d = DistBus[x, z];
            if (d >= Inf) return 0f;
            float p = 1f - d / MaxDist;
            if (p < 0f) p = 0f;
            if (p > 1f) p = 1f;
            return p;
        }

        // ------------------------------------------------------------------ запросы

        public static int CX(float wx) { return (int)Math.Floor(wx / Cell); }
        public static int CZ(float wz) { return (int)Math.Floor(wz / Cell); }
        public static Pt CenterOf(int cx, int cz) { return new Pt((cx + 0.5f) * Cell, (cz + 0.5f) * Cell); }

        public static bool InGrid(int cx, int cz) { return cx >= 0 && cx < W && cz >= 0 && cz < H; }

        public static bool IsRoadCell(int cx, int cz)
        {
            return InGrid(cx, cz) && Kind[cx, cz] != Building;
        }

        public static bool IsRoad(float wx, float wz, bool respectWalls)
        {
            int cx = CX(wx);
            int cz = CZ(wz);
            if (!IsRoadCell(cx, cz)) return false;
            if (respectWalls && Blocked[cx, cz]) return false;
            return true;
        }

        /// <summary>Доля пути до автобуса: 0 — старт, 1 — автобус.</summary>
        public static float Progress(float wx, float wz)
        {
            int cx, cz;
            if (!NearestRoad(wx, wz, out cx, out cz)) return 0f;
            return ProgressOfCell(cx, cz);
        }

        public static int DistrictAt(float wx, float wz)
        {
            int cx = Math.Max(0, Math.Min(W - 1, CX(wx)));
            int cz = Math.Max(0, Math.Min(H - 1, CZ(wz)));
            return District[cx, cz];
        }

        public static bool NearestRoad(float wx, float wz, out int rx, out int rz)
        {
            int cx = CX(wx);
            int cz = CZ(wz);
            if (IsRoadCell(cx, cz)) { rx = cx; rz = cz; return true; }
            float best = Inf;
            rx = cx;
            rz = cz;
            for (int r = 1; r <= 3; r++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    for (int z = cz - r; z <= cz + r; z++)
                    {
                        if (!IsRoadCell(x, z)) continue;
                        Pt c = CenterOf(x, z);
                        float d = (c.X - wx) * (c.X - wx) + (c.Z - wz) * (c.Z - wz);
                        if (d < best) { best = d; rx = x; rz = z; }
                    }
                }
                if (best < Inf) return true;
            }
            return false;
        }

        /// <summary>
        /// Можно ли стоять в точке существу с радиусом r: центр и восемь точек по кругу должны быть дорогой
        /// (здания и, если respectWalls, живые наросты Тьмы — непроходимы).
        /// </summary>
        public static bool CanStand(float wx, float wz, float r, bool respectWalls)
        {
            if (!IsRoad(wx, wz, respectWalls)) return false;
            if (r < 0.05f) return true;
            float d = r * 0.7071f;
            return IsRoad(wx + r, wz, respectWalls) && IsRoad(wx - r, wz, respectWalls)
                && IsRoad(wx, wz + r, respectWalls) && IsRoad(wx, wz - r, respectWalls)
                && IsRoad(wx + d, wz + d, respectWalls) && IsRoad(wx - d, wz + d, respectWalls)
                && IsRoad(wx + d, wz - d, respectWalls) && IsRoad(wx - d, wz - d, respectWalls);
        }

        /// <summary>Есть ли путь по дорогам (наросты Тьмы непроходимы).</summary>
        public static bool HasPath(float ax, float az, float bx, float bz)
        {
            return FindPath(new Pt(ax, az), new Pt(bx, bz), false, 0f) != null;
        }

        /// <summary>Можно ли пройти по прямой, не заходя в здания (с запасом margin метров).</summary>
        public static bool LineClear(float ax, float az, float bx, float bz, float margin, bool respectWalls)
        {
            float dx = bx - ax;
            float dz = bz - az;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.01f) return IsRoad(ax, az, respectWalls);
            float nx = -dz / len;
            float nz = dx / len;
            int steps = (int)(len / 1.0f) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float x = ax + dx * t;
                float z = az + dz * t;
                if (!IsRoad(x, z, respectWalls)) return false;
                if (margin > 0.01f)
                {
                    if (!IsRoad(x + nx * margin, z + nz * margin, respectWalls)) return false;
                    if (!IsRoad(x - nx * margin, z - nz * margin, respectWalls)) return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ поиск пути

        static void HeapPush(ref int count, int node, float key)
        {
            int i = count++;
            heapNode[i] = node;
            heapKey[i] = key;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (heapKey[p] <= heapKey[i]) break;
                int tn = heapNode[p]; heapNode[p] = heapNode[i]; heapNode[i] = tn;
                float tk = heapKey[p]; heapKey[p] = heapKey[i]; heapKey[i] = tk;
                i = p;
            }
        }

        static void HeapPop(ref int count, out int node, out float key)
        {
            node = heapNode[0];
            key = heapKey[0];
            count--;
            heapNode[0] = heapNode[count];
            heapKey[0] = heapKey[count];
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1;
                int r = l + 1;
                int m = i;
                if (l < count && heapKey[l] < heapKey[m]) m = l;
                if (r < count && heapKey[r] < heapKey[m]) m = r;
                if (m == i) break;
                int tn = heapNode[m]; heapNode[m] = heapNode[i]; heapNode[i] = tn;
                float tk = heapKey[m]; heapKey[m] = heapKey[i]; heapKey[i] = tk;
                i = m;
            }
        }

        /// <summary>
        /// Путь по дорогам. allowWalls=false — нарост Тьмы непроходим (люди);
        /// true — проходим, но дорог (wallCost) — так проводник ведёт в обход, а если обхода нет, то сквозь стену.
        /// </summary>
        public static List<Pt> FindPath(Pt from, Pt to, bool allowWalls, float wallCost)
        {
            int sx, sz, tx, tz;
            if (!NearestRoad(from.X, from.Z, out sx, out sz)) return null;
            if (!NearestRoad(to.X, to.Z, out tx, out tz)) return null;

            for (int i = 0; i < W * H; i++)
            {
                pdist[i] = Inf;
                pprev[i] = -1;
            }
            int start = sx * H + sz;
            int goal = tx * H + tz;
            pdist[start] = 0f;
            int count = 0;
            HeapPush(ref count, start, 0f);

            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };
            while (count > 0)
            {
                int n;
                float d;
                HeapPop(ref count, out n, out d);
                if (d > pdist[n]) continue;
                if (n == goal) break;
                int cx = n / H;
                int cz = n % H;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + dx[k];
                    int nz = cz + dz[k];
                    if (!IsRoadCell(nx, nz)) continue;
                    float c = 1f;
                    if (Blocked[nx, nz])
                    {
                        if (!allowWalls) continue;
                        c += wallCost;
                    }
                    int nn = nx * H + nz;
                    float nd = d + c;
                    if (nd < pdist[nn])
                    {
                        pdist[nn] = nd;
                        pprev[nn] = n;
                        if (count < heapNode.Length - 1) HeapPush(ref count, nn, nd);
                    }
                }
            }
            if (pdist[goal] >= Inf) return null;

            List<Pt> cells = new List<Pt>();
            int cur = goal;
            while (cur != -1)
            {
                cells.Add(CenterOf(cur / H, cur % H));
                cur = pprev[cur];
            }
            cells.Reverse();
            cells[0] = from;
            if (cells.Count > 1) cells[cells.Count - 1] = to;
            // Сглаживаем всегда с учётом стен: иначе путь срежет обход прямо сквозь нарост.
            return Smooth(cells, 0.5f, true);
        }

        /// <summary>Выпрямляет путь по клеткам: убирает лишние повороты там, где видно напрямую.</summary>
        static List<Pt> Smooth(List<Pt> pts, float margin, bool respectWalls)
        {
            List<Pt> res = new List<Pt>();
            res.Add(pts[0]);
            int i = 0;
            while (i < pts.Count - 1)
            {
                int j = pts.Count - 1;
                while (j > i + 1 && !LineClear(pts[i].X, pts[i].Z, pts[j].X, pts[j].Z, margin, respectWalls)) j--;
                res.Add(pts[j]);
                i = j;
            }
            return res;
        }

        public static float PathLength(List<Pt> p)
        {
            if (p == null) return -1f;
            float s = 0f;
            for (int i = 1; i < p.Count; i++)
            {
                float dx = p[i].X - p[i - 1].X;
                float dz = p[i].Z - p[i - 1].Z;
                s += (float)Math.Sqrt(dx * dx + dz * dz);
            }
            return s;
        }

        // ------------------------------------------------------------------ отладка

        public static string ToAscii()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int z = H - 1; z >= 0; z--)
            {
                sb.Append(z.ToString("00")).Append(' ');
                for (int x = 0; x < W; x++)
                {
                    char c = '#';
                    byte k = Kind[x, z];
                    if (k == Street) c = '.';
                    else if (k == Alley) c = ',';
                    else if (k == Plaza) c = 'o';
                    else if (k == Tunnel) c = '=';
                    if (Blocked[x, z]) c = 'W';
                    for (int i = 0; i < Hearths.Count; i++) if (Hearths[i].X == x && Hearths[i].Z == z) c = 'H';
                    for (int i = 0; i < Survivors.Count; i++) if (Survivors[i].X == x && Survivors[i].Z == z) c = i < 9 ? (char)('1' + i) : (char)('A' + (i - 9));   // 1..9, затем A (десятый)
                    for (int i = 0; i < Generators.Count; i++) if (Generators[i].X == x && Generators[i].Z == z) c = 'g';
                    if (x == StartX && z == StartZ) c = 'S';
                    if (x == BusX && z == BusZ) c = 'B';
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
