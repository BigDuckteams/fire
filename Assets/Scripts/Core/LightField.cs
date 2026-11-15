using System.Collections.Generic;
using UnityEngine;

namespace PokaGoryu
{
    /// <summary>Всё, что излучает свет в мире: лампы, герой, выжившие, огоньки-осколки.</summary>
    public interface ILightSource
    {
        Vector3 LightPosition { get; }
        float LightRadius { get; }
        float LightIntensity { get; }
        Color LightColor { get; }
    }

    /// <summary>
    /// Игровая логика света отдельно от рендера.
    /// Sample(point) аналитически считает яркость в точке: по ней живут враги (боятся/тянутся),
    /// выжившие ("стой в свете"), Тьма (съедает вне света). PushToShaders() отдаёт ближайшие
    /// источники шейдерам, поэтому картинка и логика всегда совпадают.
    /// </summary>
    public static class LightField
    {
        public const int MaxShaderLights = 16;

        static readonly List<ILightSource> sources = new List<ILightSource>(64);
        static readonly List<ILightSource> scratch = new List<ILightSource>(64);
        static readonly Vector4[] posArr = new Vector4[MaxShaderLights];
        static readonly Vector4[] colArr = new Vector4[MaxShaderLights];
        static readonly System.Comparison<ILightSource> byDist = CompareByDist;
        static Vector3 sortOrigin;
        static int idPos = -1;
        static int idCol;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sources.Clear();
            scratch.Clear();
            idPos = -1;
        }

        public static void Register(ILightSource s)
        {
            if (s == null) return;
            if (!sources.Contains(s)) sources.Add(s);
        }

        public static void Unregister(ILightSource s)
        {
            sources.Remove(s);
        }

        public static void Clear()
        {
            sources.Clear();
        }

        public static int Count { get { return sources.Count; } }

        public static float Falloff(float dist, float radius)
        {
            if (radius <= 0.001f || dist >= radius) return 0f;
            float t = 1f - dist / radius;
            return t * t;
        }

        /// <summary>Яркость в точке (0 — темнота, ~1 — рядом с полной лампой).</summary>
        public static float Sample(Vector3 p)
        {
            return Sample(p, null);
        }

        /// <summary>То же, но без вклада одного источника (чтобы выживший не освещал сам себя).</summary>
        public static float Sample(Vector3 p, ILightSource ignore)
        {
            float sum = 0f;
            for (int i = 0; i < sources.Count; i++)
            {
                ILightSource s = sources[i];
                if (s == ignore) continue;
                float inten = s.LightIntensity;
                if (inten <= 0.001f) continue;
                sum += inten * Falloff((s.LightPosition - p).magnitude, s.LightRadius);
            }
            return sum;
        }

        /// <summary>Передать шейдерам до 16 ближайших источников. Герой (first) всегда первый.</summary>
        public static void PushToShaders(Vector3 camPos, ILightSource first)
        {
            if (idPos < 0)
            {
                idPos = Shader.PropertyToID("_PG_Lights");
                idCol = Shader.PropertyToID("_PG_LightColors");
            }

            scratch.Clear();
            for (int i = 0; i < sources.Count; i++)
            {
                ILightSource s = sources[i];
                if (s == first) continue;
                if (s.LightIntensity <= 0.01f || s.LightRadius <= 0.1f) continue;
                scratch.Add(s);
            }
            sortOrigin = camPos;
            scratch.Sort(byDist);

            int n = 0;
            if (first != null)
            {
                Fill(n, first);
                n++;
            }
            for (int i = 0; i < scratch.Count && n < MaxShaderLights; i++)
            {
                Fill(n, scratch[i]);
                n++;
            }
            for (; n < MaxShaderLights; n++)
            {
                posArr[n] = Vector4.zero;
                colArr[n] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(idPos, posArr);
            Shader.SetGlobalVectorArray(idCol, colArr);
        }

        static void Fill(int n, ILightSource s)
        {
            Vector3 p = s.LightPosition;
            posArr[n] = new Vector4(p.x, p.y, p.z, s.LightRadius);
            Color c = s.LightColor;
            float k = s.LightIntensity;
            colArr[n] = new Vector4(c.r * k, c.g * k, c.b * k, 1f);
        }

        static int CompareByDist(ILightSource a, ILightSource b)
        {
            float da = (a.LightPosition - sortOrigin).magnitude - a.LightRadius * 0.5f;
            float db = (b.LightPosition - sortOrigin).magnitude - b.LightRadius * 0.5f;
            return da.CompareTo(db);
        }
    }
}
