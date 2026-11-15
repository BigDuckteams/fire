using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Быстрый шум для процедурных текстур: хеш целых чисел и сглаженная интерполяция. В разы быстрее Mathf.PerlinNoise,
    /// а варианты Tile делают шум бесшовным (текстуры города повторяются).
    /// </summary>
    public static class Noise
    {
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h = h ^ (h >> 16);
                return (h & 0x7fffffff) * (1f / 2147483647f);
            }
        }

        static int Wrap(int i, int period)
        {
            int m = i % period;
            return m < 0 ? m + period : m;
        }

        /// <summary>Обычный (небесшовный) шум значений: x, y в клетках сетки.</summary>
        public static float Value(float x, float y, int seed)
        {
            int xi = (int)Mathf.Floor(x);
            int yi = (int)Mathf.Floor(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi, seed);
            float b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed);
            float d = Hash(xi + 1, yi + 1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        /// <summary>Бесшовный шум значений: x, y в клетках сетки, period — размер периода в клетках (целое).</summary>
        public static float ValueTile(float x, float y, int periodX, int periodY, int seed)
        {
            int xi = (int)Mathf.Floor(x);
            int yi = (int)Mathf.Floor(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = Wrap(xi, periodX);
            int x1 = Wrap(xi + 1, periodX);
            int y0 = Wrap(yi, periodY);
            int y1 = Wrap(yi + 1, periodY);
            float a = Hash(x0, y0, seed);
            float b = Hash(x1, y0, seed);
            float c = Hash(x0, y1, seed);
            float d = Hash(x1, y1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        /// <summary>Бесшовный фрактальный шум для u, v в 0..1: baseFreq клеток на текстуру, дальше частота удваивается.</summary>
        public static float FbmTile(float u, float v, int baseFreq, int octaves, int seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float norm = 0f;
            int freq = baseFreq;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * ValueTile(u * freq, v * freq, freq, freq, seed + i * 17);
                norm += amp;
                amp *= 0.5f;
                freq *= 2;
            }
            return sum / norm;
        }

        /// <summary>Готовое поле шума n x n (бесшовное): потом его дёшево читать билинейно, вместо шума на каждый пиксель.</summary>
        public static float[] Field(int n, int baseFreq, int octaves, int seed)
        {
            float[] f = new float[n * n];
            float inv = 1f / n;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++) f[y * n + x] = FbmTile(x * inv, y * inv, baseFreq, octaves, seed);
            }
            return f;
        }

        /// <summary>Поле, вытянутое по одной оси (потёки): частоты по x и y разные.</summary>
        public static float[] FieldStretched(int n, int freqX, int freqY, int seed)
        {
            float[] f = new float[n * n];
            float inv = 1f / n;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float a = ValueTile(x * inv * freqX, y * inv * freqY, freqX, freqY, seed);
                    float b = ValueTile(x * inv * freqX * 2f, y * inv * freqY * 2f, freqX * 2, freqY * 2, seed + 5);
                    f[y * n + x] = (a * 2f + b) / 3f;
                }
            }
            return f;
        }

        /// <summary>Билинейное чтение поля (u, v в 0..1, бесшовно).</summary>
        public static float Sample(float[] field, int n, float u, float v)
        {
            float fx = u * n;
            float fy = v * n;
            int x0 = (int)Mathf.Floor(fx);
            int y0 = (int)Mathf.Floor(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            int x1 = Wrap(x0 + 1, n);
            int y1 = Wrap(y0 + 1, n);
            x0 = Wrap(x0, n);
            y0 = Wrap(y0, n);
            float a = field[y0 * n + x0];
            float b = field[y0 * n + x1];
            float c = field[y1 * n + x0];
            float d = field[y1 * n + x1];
            return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
        }
    }
}
