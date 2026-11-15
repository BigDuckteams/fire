using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Предыстория, вид на улицу: камера летит над настоящим проспектом (там, где потом пойдёт игрок), огни горят — лампы и окна,
    /// и по улице с дальнего конца идёт Тьма: огни у фронта дёргаются и гаснут, за фронтом остаётся чёрная улица. Края города не
    /// видны: по бокам дома, вдали туман, улица упирается в фасад. Дома и лампы настоящие; огни в окнах — светящиеся прямоугольники
    /// ровно по окнам текстуры фасада (целые стёкла и занавески), лампами управляет маска Lamp.IntroMask.
    /// </summary>
    public class StreetBlackout
    {
        const float C = CityMap.Cell;

        public Transform Root;
        public float Front = 9999f;   // z: севернее — всё уже накрыто Тьмой
        float time;

        // окна
        float[] wz;
        float[] wbr;
        Color32[] wcol;
        int count;
        Color32[] vcols;
        Mesh mesh;

        // фронт Тьмы
        Material hazeMat;
        Transform[] haze;
        Material edgeMat;
        Transform edge;

        static float Hash01(int a, int b, int s)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ s * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        static Color32 K(int r, int g, int b)
        {
            return new Color32((byte)r, (byte)g, (byte)b, 255);
        }

        public static StreetBlackout Build()
        {
            StreetBlackout s = new StreetBlackout();
            GameObject root = new GameObject("IntroStreetBlackout");
            s.Root = root.transform;
            s.BuildWindows();
            s.BuildFront();
            root.SetActive(false);
            return s;
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
            if (mesh != null) Object.Destroy(mesh);
        }

        // ------------------------------------------------------------------ огни в окнах

        /// <summary>
        /// Окно фасада — клетка 3x3 м в текстуре (8x8 клеток на 24 м), окно внутри клетки: по длине стены 0.61..2.39 м,
        /// по высоте 0.80..2.48 м над полом этажа. Целое стекло (хеш клетки < 0.46) и занавески (0.72..0.86) могут светиться.
        /// Здесь те же клетки и тот же хеш, что в TexGen.Facade: прямоугольники ложатся точно на окна.
        /// </summary>
        void BuildWindows()
        {
            System.Collections.Generic.List<Vector3> v = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector2> uv = new System.Collections.Generic.List<Vector2>();
            System.Collections.Generic.List<int> tri = new System.Collections.Generic.List<int>();
            System.Collections.Generic.List<float> zs = new System.Collections.Generic.List<float>();
            System.Collections.Generic.List<float> brs = new System.Collections.Generic.List<float>();
            System.Collections.Generic.List<Color32> cs = new System.Collections.Generic.List<Color32>();
            Color32[] warm = { K(255, 196, 112), K(255, 178, 92), K(255, 210, 140), K(255, 188, 104), K(190, 214, 255) };
            Vector2 mid = new Vector2(0.5f, 0.5f);
            int[] nx = { 1, -1, 0, 0 };
            int[] nz = { 0, 0, 1, -1 };

            // участок вокруг проспекта и площади за ним (больше с камеры не видно)
            for (int z = 6; z < 34; z++)
            {
                for (int x = 0; x < 16; x++)
                {
                    if (CityMap.Kind[x, z] != CityMap.Building) continue;
                    float h = LevelBuilder.BuildingHeight[x, z];
                    if (h < 6f) continue;
                    int d = CityMap.District[x, z];
                    int seed = 100 + d * 37;
                    int floors = Mathf.FloorToInt(h / 3f);
                    for (int s = 0; s < 4; s++)
                    {
                        if (!CityMap.IsRoadCell(x + nx[s], z + nz[s])) continue;
                        Vector3 n = new Vector3(nx[s], 0f, nz[s]);
                        Vector3 t = nx[s] != 0 ? Vector3.forward : Vector3.right;   // вдоль стены в сторону РОСТА мировой координаты (так считает текстура)
                        for (int k = 0; k < 2; k++)
                        {
                            // координата начала клетки текстуры вдоль стены (мировая) и её номер в тайле 8x8
                            float a0 = (nx[s] != 0 ? z : x) * C + k * 3f;
                            int cx = Mathf.FloorToInt(a0 / 3f) & 7;
                            Vector3 wall = nx[s] != 0
                                ? new Vector3((nx[s] > 0 ? x + 1 : x) * C, 0f, a0)
                                : new Vector3(a0, 0f, (nz[s] > 0 ? z + 1 : z) * C);
                            for (int f = 1; f < floors; f++)
                            {
                                float hs = Noise.Hash(cx, f & 7, seed + 7);
                                bool candidate = hs < 0.46f || (hs >= 0.72f && hs < 0.86f);
                                if (!candidate) continue;
                                int key = x * 977 + z * 313 + s * 53 + k * 17 + f * 7;
                                if (Hash01(key, 1, 11) > 0.62f) continue;   // дома не все
                                Vector3 c = wall + t * (0.609f + 0.891f) + n * 0.04f + new Vector3(0f, 3f * f + 0.797f + 0.844f, 0f);
                                if (f == 1 && NearEntrance(c, 2.5f)) continue;   // над входами висят вывески
                                int i0 = v.Count;
                                v.Add(c - t * 0.891f - Vector3.up * 0.844f);
                                v.Add(c - t * 0.891f + Vector3.up * 0.844f);
                                v.Add(c + t * 0.891f + Vector3.up * 0.844f);
                                v.Add(c + t * 0.891f - Vector3.up * 0.844f);
                                for (int q = 0; q < 4; q++) uv.Add(mid);
                                tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2);
                                tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 3);
                                zs.Add(c.z + (Hash01(key, 2, 5) - 0.5f) * 14f);
                                brs.Add(0.32f + 0.40f * Hash01(key, 3, 9));
                                cs.Add(warm[(int)(Hash01(key, 4, 13) * warm.Length) % warm.Length]);
                            }
                        }
                    }
                }
            }

            count = zs.Count;
            wz = zs.ToArray();
            wbr = brs.ToArray();
            wcol = cs.ToArray();
            vcols = new Color32[v.Count];
            for (int i = 0; i < vcols.Length; i++) vcols[i] = new Color32(0, 0, 0, 255);

            mesh = new Mesh();
            mesh.name = "PG_StreetWindows";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.colors32 = vcols;
            mesh.bounds = new Bounds(new Vector3(40f, 15f, 120f), new Vector3(220f, 60f, 260f));
            Gfx.MeshObject(Root, mesh, OwnedResources.Keep(Root.gameObject, Gfx.Unlit(Color.white, Gfx.TexHalo, true, false, 0.5f)), "StreetWindows");
        }

        static bool NearEntrance(Vector3 p, float r)
        {
            System.Collections.Generic.IList<Vector3> e = LevelBuilder.Entrances;
            for (int i = 0; i < e.Count; i++)
            {
                float dx = e[i].x - p.x;
                float dz = e[i].z - p.z;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ фронт Тьмы

        void BuildFront()
        {
            // стена тёмно-фиолетовой дымки поперёк улицы (рисуется после огней и закрывает их)
            hazeMat = OwnedResources.Keep(Root.gameObject, Gfx.Unlit(new Color(0.10f, 0.04f, 0.17f, 0f), Gfx.TexHalo, false, true, 0f));
            hazeMat.renderQueue = 3200;
            haze = new Transform[9];
            for (int i = 0; i < haze.Length; i++)
            {
                GameObject h = Gfx.Sprite(Root, new Vector3(36f, 14f, 100f), new Vector2(22f, 36f), hazeMat, true);
                h.name = "StreetHaze";
                haze[i] = h.transform;
            }
            // светящаяся кромка Тьмы на мостовой
            edgeMat = OwnedResources.Keep(Root.gameObject, Gfx.Unlit(new Color(0.55f, 0.26f, 0.95f, 0f), Gfx.TexHalo, true, false, 0f));
            GameObject eg = Gfx.Prim(PrimitiveType.Quad, Root, new Vector3(36f, 0.35f, 100f), new Vector3(30f, 20f, 1f), edgeMat, false);
            eg.name = "StreetEdge";
            eg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            edge = eg.transform;
        }

        // ------------------------------------------------------------------ кадр

        /// <summary>Маска для ламп (Lamp.IntroMask): 1 — горит, 0 — погасла, между — дёргается перед фронтом Тьмы.</summary>
        public float LampMask(Vector3 p)
        {
            float jit = (Hash01((int)(p.x * 3f), (int)(p.z * 3f), 5) - 0.5f) * 12f;
            float e = (p.z + jit) - Front;
            if (e >= 0f) return 0f;
            if (e > -24f)
            {
                float t = -e / 24f;                       // 0 у фронта .. 1 вдали
                float n = Mathf.PerlinNoise(p.x * 0.7f + p.z * 0.31f, time * 12f);
                return n > 0.82f - 0.62f * t ? 1f : 0.10f;
            }
            return 1f;
        }

        /// <summary>front — z, севернее которого всё накрыто Тьмой; hazeAlpha — насколько видна стена дымки и кромка.</summary>
        public void SetWave(float front, float t, float hazeAlpha)
        {
            Front = front;
            time = t;
            for (int i = 0; i < count; i++)
            {
                float e = wz[i] - front;      // >= 0 — уже накрыто Тьмой
                float k;
                if (e >= 0f) k = 0f;
                else if (e > -22f)
                {
                    float p = -e / 22f;
                    float n = Mathf.PerlinNoise(i * 0.37f, t * 11f);
                    k = n > 0.80f - 0.64f * p ? 1f : 0.10f;
                }
                else k = 1f;
                float br = wbr[i] * k;
                Color32 c = wcol[i];
                Color32 o = new Color32((byte)(c.r * br), (byte)(c.g * br), (byte)(c.b * br), 255);
                int vi = i * 4;
                vcols[vi] = o;
                vcols[vi + 1] = o;
                vcols[vi + 2] = o;
                vcols[vi + 3] = o;
            }
            mesh.colors32 = vcols;

            hazeMat.SetColor("_Color", new Color(0.10f, 0.04f, 0.17f, hazeAlpha));
            for (int i = 0; i < haze.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                float wob = Mathf.Sin(t * 0.9f + i * 1.9f) * 1.5f;
                haze[i].localPosition = new Vector3(36f + (col - 1) * 9f, 10f + row * 9f, front + 3f + row * 4f + wob);
            }
            edgeMat.SetColor("_Color", new Color(0.55f, 0.26f, 0.95f, hazeAlpha * (0.7f + 0.3f * Mathf.Sin(t * 9f))));
            edge.localPosition = new Vector3(36f, 0.35f, front + 2f);
        }
    }
}
