using System.Collections.Generic;
using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Вывески и плакаты города. Шрифта и картинок в проекте нет, поэтому буквы рисуются кодом: пиксельный шрифт 5x7
    /// (русские заглавные, цифры, знаки) раскладывается по одной текстуре-атласу, каждая вывеска — свой прямоугольник
    /// (доска, рамка, буквы). Меш вывески — четырёхугольник с UV в этот прямоугольник (MeshBatch.QuadUV).
    /// Атлас собирается на время постройки города: Begin() -> Sign()/Poster() -> End() (тут текстура заливается в GPU).
    /// В альфа-канале нули: вывески не светятся сами, их видно только в свете ламп и героя, как и всё остальное.
    /// </summary>
    public static class SignAtlas
    {
        public const int Size = 1024;
        const int Gutter = 4;

        public static Texture2D Tex;

        struct Entry
        {
            public Vector2 UV0;
            public Vector2 UV1;
            public float Aspect;   // ширина / высота картинки
        }

        static Color32[] px;
        static int cx, cy, rowH;
        static bool open;
        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        static readonly Dictionary<char, string> glyphs = new Dictionary<char, string>();

        // 7 строк по 5 точек; '1' — краска
        static readonly string[] Font =
        {
            "А01110/10001/10001/11111/10001/10001/10001",
            "Б11111/10000/10000/11110/10001/10001/11110",
            "В11110/10001/10001/11110/10001/10001/11110",
            "Г11111/10000/10000/10000/10000/10000/10000",
            "Д00110/01010/01010/01010/01010/11111/10001",
            "Е11111/10000/10000/11110/10000/10000/11111",
            "Ё01010/11111/10000/11110/10000/10000/11111",
            "Ж10101/10101/10101/01110/10101/10101/10101",
            "З11110/00001/00001/01110/00001/00001/11110",
            "И10001/10001/10011/10101/11001/10001/10001",
            "Й01010/10001/10011/10101/11001/10001/10001",
            "К10001/10010/10100/11000/10100/10010/10001",
            "Л00111/01001/01001/01001/01001/01001/10001",
            "М10001/11011/10101/10101/10001/10001/10001",
            "Н10001/10001/10001/11111/10001/10001/10001",
            "О01110/10001/10001/10001/10001/10001/01110",
            "П11111/10001/10001/10001/10001/10001/10001",
            "Р11110/10001/10001/11110/10000/10000/10000",
            "С01110/10001/10000/10000/10000/10001/01110",
            "Т11111/00100/00100/00100/00100/00100/00100",
            "У10001/10001/10001/01111/00001/00001/01110",
            "Ф00100/01110/10101/10101/01110/00100/00100",
            "Х10001/10001/01010/00100/01010/10001/10001",
            "Ц10010/10010/10010/10010/10010/11111/00001",
            "Ч10001/10001/10001/01111/00001/00001/00001",
            "Ш10101/10101/10101/10101/10101/10101/11111",
            "Щ10101/10101/10101/10101/10101/11111/00001",
            "Ъ11000/01000/01000/01110/01001/01001/01110",
            "Ы10001/10001/10001/11001/10101/10101/11001",
            "Ь10000/10000/10000/11110/10001/10001/11110",
            "Э01110/10001/00001/01111/00001/10001/01110",
            "Ю10010/10101/10101/11101/10101/10101/10010",
            "Я01111/10001/10001/01111/00101/01001/10001",
            "001110/10001/10011/10101/11001/10001/01110",
            "100100/01100/00100/00100/00100/00100/01110",
            "201110/10001/00001/00010/00100/01000/11111",
            "311110/00001/00001/01110/00001/00001/11110",
            "400010/00110/01010/10010/11111/00010/00010",
            "511111/10000/11110/00001/00001/10001/01110",
            "600110/01000/10000/11110/10001/10001/01110",
            "711111/00001/00010/00100/01000/01000/01000",
            "801110/10001/10001/01110/10001/10001/01110",
            "901110/10001/10001/01111/00001/00010/01100",
            "-00000/00000/00000/11111/00000/00000/00000",
            ".00000/00000/00000/00000/00000/01100/01100",
            "!00100/00100/00100/00100/00100/00000/00100",
            ":00000/01100/01100/00000/01100/01100/00000",
            "/00001/00010/00010/00100/01000/01000/10000",
            "+00000/00100/00100/11111/00100/00100/00000",
            "?01110/10001/00001/00110/00100/00000/00100"
        };

        static SignAtlas()
        {
            for (int i = 0; i < Font.Length; i++) glyphs[Font[i][0]] = Font[i].Substring(1);
        }

        // ------------------------------------------------------------------ жизненный цикл

        /// <summary>Начать новый атлас (старый убирается). Белый квадрат 32x32 в углу — для коробок без картинки.</summary>
        public static void Begin()
        {
            if (Tex != null) { Gfx.ReleaseWorldTexture(Tex); Object.Destroy(Tex); }
            Tex = null;
            px = new Color32[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++) px[y * Size + x] = new Color32(255, 255, 255, 0);
            }
            cx = 36;
            cy = 0;
            rowH = 0;
            cache.Clear();
            open = true;
        }

        public static void End()
        {
            if (!open) return;
            open = false;
            Tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            Tex.SetPixels32(px);
            Tex.wrapMode = TextureWrapMode.Clamp;
            Tex.filterMode = FilterMode.Bilinear;
            Tex.anisoLevel = 2;
            Tex.Apply(true, true);
            px = null;
        }

        /// <summary>Сколько пикселей атласа занято (для статистики).</summary>
        public static int UsedRows { get { return cy + rowH; } }

        static bool Alloc(int w, int h, out int x, out int y)
        {
            if (cx + w + Gutter > Size)
            {
                cx = 0;
                cy += rowH + Gutter;
                rowH = 0;
            }
            if (cy + h + Gutter > Size)
            {
                x = 0;
                y = 0;
                return false;
            }
            x = cx;
            y = cy;
            cx += w + Gutter;
            if (h > rowH) rowH = h;
            return true;
        }

        static void Put(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            px[y * Size + x] = c;
        }

        static Color32 Mul(Color32 c, float k)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k, 0f, 255f), (byte)Mathf.Clamp(c.g * k, 0f, 255f), (byte)Mathf.Clamp(c.b * k, 0f, 255f), 255);
        }

        static int Hash(int a, int b)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }

        // ------------------------------------------------------------------ текст

        /// <summary>Ширина строки в пикселях при масштабе sc (буква 5 точек + 1 точка промежутка).</summary>
        public static int TextWidth(string text, int sc)
        {
            int w = 0;
            for (int i = 0; i < text.Length; i++) w += (text[i] == ' ' ? 4 : 6) * sc;
            return w - sc;
        }

        /// <summary>Нарисовать строку: x, y — левый нижний угол первой буквы (y растёт вверх).</summary>
        static void DrawText(string text, int x, int y, int sc, Color32 ink)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == ' ')
                {
                    x += 4 * sc;
                    continue;
                }
                string g;
                if (!glyphs.TryGetValue(ch, out g)) g = glyphs['?'];
                string[] rows = g.Split('/');
                for (int r = 0; r < 7; r++)
                {
                    for (int c = 0; c < 5; c++)
                    {
                        if (rows[r][c] != '1') continue;
                        for (int dy = 0; dy < sc; dy++)
                        {
                            for (int dx = 0; dx < sc; dx++) Put(x + c * sc + dx, y + (6 - r) * sc + dy, ink);
                        }
                    }
                }
                x += 6 * sc;
            }
        }

        static Entry Make(int x, int y, int w, int h)
        {
            Entry e = new Entry();
            // полпикселя внутрь: билинейный фильтр не цепляет соседей
            e.UV0 = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);
            e.UV1 = new Vector2((x + w - 0.5f) / Size, (y + h - 0.5f) / Size);
            e.Aspect = w / (float)h;
            return e;
        }

        static Entry WhiteEntry()
        {
            Entry e = new Entry();
            e.UV0 = new Vector2(4f / Size, 4f / Size);
            e.UV1 = new Vector2(28f / Size, 28f / Size);
            e.Aspect = 1f;
            return e;
        }

        // ------------------------------------------------------------------ вывески

        /// <summary>
        /// Вывеска: доска цвета board, рамка, буквы цвета ink. scale — размер точки шрифта в пикселях (3 — крупно, 2 — мелко).
        /// Возвращает UV-прямоугольник и отношение сторон (ширина / высота): по нему задаётся размер доски в метрах.
        /// </summary>
        public static void Sign(string text, Color32 board, Color32 ink, int scale, out Vector2 uv0, out Vector2 uv1, out float aspect)
        {
            string key = "S|" + text + "|" + board.r + "," + board.g + "," + board.b + "|" + ink.r + "," + ink.g + "," + ink.b + "|" + scale;
            Entry e;
            if (!cache.TryGetValue(key, out e))
            {
                int padX = 4 + scale * 2;
                int padY = 3 + scale * 2;
                int w = TextWidth(text, scale) + padX * 2;
                int h = 7 * scale + padY * 2;
                int x, y;
                if (!open || !Alloc(w, h, out x, out y))
                {
                    e = WhiteEntry();
                }
                else
                {
                    Color32 edge = Mul(board, 0.55f);
                    Color32 lite = Mul(board, 1.18f);
                    for (int j = 0; j < h; j++)
                    {
                        for (int i = 0; i < w; i++)
                        {
                            Color32 c;
                            int dEdge = Mathf.Min(Mathf.Min(i, w - 1 - i), Mathf.Min(j, h - 1 - j));
                            if (dEdge < 2) c = edge;
                            else if (dEdge == 2) c = lite;
                            else
                            {
                                // выцветание доски: вертикальный градиент и зерно краски
                                float k = 0.92f + 0.12f * (j / (float)h) + 0.05f * ((Hash(i, j + y) & 255) / 255f - 0.5f);
                                c = Mul(board, k);
                            }
                            Put(x + i, y + j, c);
                        }
                    }
                    DrawText(text, x + padX, y + padY, scale, ink);
                    e = Make(x, y, w, h);
                }
                cache[key] = e;
            }
            uv0 = e.UV0;
            uv1 = e.UV1;
            aspect = e.Aspect;
        }

        // ------------------------------------------------------------------ плакаты

        static readonly string[] PosterHead = { "КИНО", "ЦИРК", "КОНЦЕРТ", "ВЫБОРЫ", "ТЕАТР", "ЯРМАРКА", "ЦЕНЫ", "ВНИМАНИЕ", "НЕ ГАСНИ", "ПОМОГИТЕ", "ИЩУ ЛЮДЕЙ", "ЖДИ СВЕТА" };
        static readonly string[] PosterSub = { "СЕГОДНЯ", "ВХОД СВОБОДНЫЙ", "20:00", "ВСЕ НА РЫНОК", "БИЛЕТЫ", "ВЫХОД ЗАКРЫТ", "ОСТОРОЖНО", "ДОМ 12 КВ 7", "ТЕМНО", "ЛЮДИ ЖИВЫ" };

        /// <summary>Плакат (афиша, объявление, предупреждение): variant выбирает набор и оформление.</summary>
        public static void Poster(int variant, out Vector2 uv0, out Vector2 uv1, out float aspect)
        {
            variant = ((variant % PosterHead.Length) + PosterHead.Length) % PosterHead.Length;
            string key = "P|" + variant;
            Entry e;
            if (!cache.TryGetValue(key, out e))
            {
                string head = PosterHead[variant];
                string sub = PosterSub[variant % PosterSub.Length];
                int sc = head.Length > 8 ? 1 : 2;
                int w = Mathf.Max(TextWidth(head, sc), TextWidth(sub, 1)) + 14;
                int h = 96;
                int x, y;
                if (!open || !Alloc(w, h, out x, out y))
                {
                    e = WhiteEntry();
                }
                else
                {
                    Color32[] bgs = { new Color32(196, 188, 160, 255), new Color32(40, 70, 120, 255), new Color32(200, 170, 60, 255),
                                      new Color32(150, 44, 40, 255), new Color32(60, 90, 70, 255), new Color32(120, 110, 100, 255) };
                    Color32[] inks = { new Color32(150, 30, 28, 255), new Color32(240, 236, 220, 255), new Color32(30, 28, 26, 255),
                                       new Color32(240, 230, 210, 255), new Color32(236, 230, 200, 255), new Color32(24, 24, 28, 255) };
                    int ci = (variant * 5 + 1) % bgs.Length;
                    Color32 bg = bgs[ci];
                    Color32 ink = inks[ci];
                    // тёмные «серьёзные» плакаты: предупреждения и записки
                    if (variant >= 7)
                    {
                        bg = variant == 7 ? new Color32(206, 170, 50, 255) : new Color32(168, 160, 138, 255);
                        ink = new Color32(26, 24, 22, 255);
                    }
                    for (int j = 0; j < h; j++)
                    {
                        for (int i = 0; i < w; i++)
                        {
                            float k = 0.9f + 0.1f * ((Hash(i, j + variant * 131) & 255) / 255f) - 0.12f * (1f - j / (float)h) * ((Hash(i / 5, j / 7 + variant) & 255) / 255f);
                            int dEdge = Mathf.Min(Mathf.Min(i, w - 1 - i), Mathf.Min(j, h - 1 - j));
                            Color32 c = dEdge < 1 ? Mul(bg, 0.5f) : Mul(bg, k);
                            Put(x + i, y + j, c);
                        }
                    }
                    // заголовок
                    int hx = x + (w - TextWidth(head, sc)) / 2;
                    DrawText(head, hx, y + h - 7 * sc - 6, sc, ink);
                    // картинка: круг или треугольник акцентного цвета
                    Color32 acc = Mul(ink, 0.9f);
                    int gcx = x + w / 2;
                    int gcy = y + 50;
                    for (int j = -22; j <= 22; j++)
                    {
                        for (int i = -22; i <= 22; i++)
                        {
                            bool on;
                            if (variant % 3 == 0) on = i * i + j * j < 20 * 20 && i * i + j * j > 12 * 12;
                            else if (variant % 3 == 1) on = j > -18 && j < 18 && Mathf.Abs(i) < (18 - j) * 0.6f && Mathf.Abs(i) > (14 - j) * 0.6f - 3f;
                            else on = (i + j) % 8 == 0 && Mathf.Abs(i) < 20 && Mathf.Abs(j) < 20;
                            if (on) Put(gcx + i, gcy + j, acc);
                        }
                    }
                    // подпись и «мелкий шрифт»
                    DrawText(sub, x + (w - TextWidth(sub, 1)) / 2, y + 16, 1, ink);
                    for (int line = 0; line < 2; line++)
                    {
                        int ly = y + 6 + line * 5;
                        for (int i = 6; i < w - 6; i++)
                        {
                            if ((Hash(i / 3, line + variant * 7) & 3) != 0) Put(x + i, ly, Mul(ink, 1.25f));
                        }
                    }
                    e = Make(x, y, w, h);
                }
                cache[key] = e;
            }
            uv0 = e.UV0;
            uv1 = e.UV1;
            aspect = e.Aspect;
        }

        public static int PosterVariants { get { return PosterHead.Length; } }
    }
}
