using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Тело героя от первого лица. Здоровье читается по самому телу (интерфейса нет): на левом предплечье, как таймер в
    /// фильме «Время» (2011), горит полоска «Перья» (FeatherHealthBar). Опустишь взгляд (или зажмёшь Tab) — герой поднимает
    /// левую руку перед собой, как смотрят на часы, и видно, сколько света осталось.
    /// Руки собраны под контракт PalmShot.cs (ссылка): HandsRoot — корень рук (отдача), у каждой кисти есть ладонь-пустышка
    /// (её +Z смотрит из ладони) и суставы пальцев; пальцы раскрываются на openAngle. Обычная поза — «готовность»,
    /// как в PalmShot: ладони вперёд, пальцы вверх. Вдох — чаша, выдох — подношение, прикрыть огонь — ладони на груди.
    /// Лучи, вспышка и отдача — PalmShot. Рукава закатаны: предплечья — кожа с манжетой у локтя и ремешком на запястье,
    /// сужаются к кисти (не тёмные цилиндры). При ходьбе кисти качаются в такт шагам.
    /// </summary>
    public class HeroBody : MonoBehaviour
    {
        /// <summary>Рука: корень (центр ладони), якорь ладони и суставы пальцев (контракт PalmShot).</summary>
        public class HandRig
        {
            public float Side;                 // +1 правая, -1 левая
            public Transform Root;
            public Transform Palm;
            public PalmShot.HandFx Fx;         // ладонь + суставы пальцев для PalmShot
            public float[] CurlMax;            // угол сустава в кулаке
            public float[] CurlExt;            // угол сустава в раскрытой кисти
            public float[] BaseY;              // доп. поворот вокруг Y (большой палец)
            public Vector3 Pos;                // положение в пространстве тела (до отдачи)
            public Quaternion Rot = Quaternion.identity;
            public float Open = ReadyOpen;     // 0 — кулак, 1 — раскрытая кисть
        }

        const float ReadyOpen = 0.72f;
        const float HandScale = 1.12f;
        const float MaxReach = 0.84f;

        /// <summary>Положение глаз в пространстве тела: вокруг этой точки поворачиваются руки при взгляде вверх и в отдаче.</summary>
        public static readonly Vector3 EyeLocal = new Vector3(0f, 1.62f, 0.16f);

        // 14 суставов кисти: 4 пальца x 3 фаланги и большой палец x 2
        static readonly float[] FingerLen =
        {
            0.036f, 0.024f, 0.020f,   // указательный
            0.040f, 0.027f, 0.022f,   // средний
            0.036f, 0.025f, 0.020f,   // безымянный
            0.029f, 0.019f, 0.017f,   // мизинец
            0.034f, 0.029f            // большой
        };

        public PalmShot Combat;
        public Transform HandsRoot;
        /// <summary>Только для автотестов: 1 — прикрыть огонь, 2 — выдох, 3 — вдох (0 — по игре).</summary>
        public int DebugPose;
        public readonly HandRig[] Hands = new HandRig[2];

        // Предыстория (кухня): сценарий сам ведёт руки. Индекс 0 — левая, 1 — правая; координаты — в пространстве тела
        // (корень игрока). OverrideW — насколько руки подчиняются сценарию (0 — игра, 1 — сценарий). HideBody — тело не рисуется.
        public float OverrideW;
        public readonly Vector3[] OverridePos = new Vector3[2];
        public readonly Quaternion[] OverrideRot = { Quaternion.identity, Quaternion.identity };
        public readonly float[] OverrideOpen = { 0.5f, 0.5f };
        public bool HideBody;
        public float ArmScale = 1f;     // толщина рук (в предыстории тоньше: кухня рядом с камерой)

        /// <summary>0..1: насколько поднята левая рука для взгляда на полоску здоровья (для автотестов и других систем).</summary>
        public float ArmRaise { get { return armRaise; } }

        static readonly Color SkinBase = new Color(0.40f, 0.31f, 0.25f, 1f);
        static readonly Color FabricBase = new Color(0.36f, 0.33f, 0.27f, 1f);   // рукав и куртка: тёплый хаки, не чёрный
        Material skinMat;

        /// <summary>Яркость кожи (1 — как в игре). В тёмной кухне предыстории руки светлее, иначе они тонут в темноте.</summary>
        public void SetSkinTone(float k)
        {
            if (skinMat != null) skinMat.SetColor("_Color", new Color(SkinBase.r * k, SkinBase.g * k, SkinBase.b * k, 1f));
        }

        // Полоска здоровья на левом предплечье: размер картинки (пиксели), доля картинки по длине и охват предплечья
        const int ArmPxW = 272;
        const int ArmPxH = 80;
        const float ArmU0 = 0.20f;
        const float ArmU1 = 0.80f;
        const float ArmT0 = 0.17f;       // от локтя до запястья (доли длины предплечья)
        const float ArmT1 = 0.89f;
        const float ArmArcDeg = 128f;    // охват предплечья со стороны ладони

        PlayerController p;
        Transform bodyRoot;
        Transform bodyVis;      // торс, ноги, воротник: видны только при взгляде вниз
        Transform legL;
        Transform legR;
        readonly Transform[] upper = new Transform[2];
        readonly Transform[] fore = new Transform[2];
        readonly Vector3[] shoulder = new Vector3[2];

        // полоска на предплечье
        GameObject armDisplay;
        GameObject armInlay;
        Material armDisplayMat;
        float armRaise;

        public void Init(PlayerController pc)
        {
            p = pc;
            GameObject rootGo = new GameObject("Body");
            bodyRoot = rootGo.transform;
            bodyRoot.SetParent(pc.transform, false);

            Material fabric = Gfx.Lit(FabricBase);
            Material pants = Gfx.Lit(new Color(0.14f, 0.14f, 0.19f, 1f));
            Material skin = Gfx.Lit(SkinBase);
            skinMat = skin;
            Material metal = Gfx.Lit(new Color(0.26f, 0.26f, 0.29f, 1f));

            // Торс, бёдра, ноги, воротник — это видно только когда смотришь вниз: прячем, пока герой смотрит вперёд
            GameObject bv = new GameObject("BodyVisual");
            bodyVis = bv.transform;
            bodyVis.SetParent(bodyRoot, false);
            Material jacketDark = Gfx.Lit(new Color(0.24f, 0.22f, 0.18f, 1f));
            Material belt = Gfx.Lit(new Color(0.17f, 0.12f, 0.09f, 1f));
            Material brass = Gfx.Lit(new Color(0.58f, 0.44f, 0.20f, 1f));
            // куртка: торс, плечи, стойка воротника, молния, карманы; ремень с пряжкой; штаны
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(0f, 1.13f, -0.05f), new Vector3(0.42f, 0.56f, 0.22f), fabric, false);
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(-0.235f, 1.35f, -0.04f), new Vector3(0.10f, 0.12f, 0.20f), fabric, false);
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(0.235f, 1.35f, -0.04f), new Vector3(0.10f, 0.12f, 0.20f), fabric, false);
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(0f, 1.405f, -0.03f), new Vector3(0.20f, 0.06f, 0.17f), jacketDark, false);
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(0f, 1.12f, 0.062f), new Vector3(0.014f, 0.52f, 0.010f), metal, false);
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(-0.12f, 0.96f, 0.062f), new Vector3(0.11f, 0.09f, 0.010f), jacketDark, false);
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(0.12f, 0.96f, 0.062f), new Vector3(0.11f, 0.09f, 0.010f), jacketDark, false);
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(0f, 0.865f, -0.045f), new Vector3(0.43f, 0.045f, 0.23f), belt, false);
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(0f, 0.865f, 0.072f), new Vector3(0.05f, 0.035f, 0.012f), brass, false);
            Gfx.Prim(PrimitiveType.Cube, bodyVis, new Vector3(0f, 0.79f, -0.04f), new Vector3(0.38f, 0.18f, 0.22f), pants, false);
            Material boots = Gfx.Lit(new Color(0.11f, 0.10f, 0.10f, 1f));
            legL = MakeLeg(-0.10f, pants, boots);
            legR = MakeLeg(0.10f, pants, boots);

            // Корень рук (PalmShot.handsRoot): двигается и запрокидывается в отдаче вокруг глаз
            GameObject hr = new GameObject("HandsRoot");
            HandsRoot = hr.transform;
            HandsRoot.SetParent(bodyRoot, false);
            HandsRoot.localPosition = EyeLocal;

            // Руки: плечо (рукав), предплечье (кожа, манжета, ремешок), кисть с пальцами
            Mesh upperMesh = BuildUpperMesh();
            Mesh foreSkin = BuildForeSkinMesh();
            Mesh foreFabric = BuildForeFabricMesh();
            shoulder[0] = new Vector3(-0.25f, 1.37f, 0f);
            shoulder[1] = new Vector3(0.25f, 1.37f, 0f);
            for (int a = 0; a < 2; a++)
            {
                upper[a] = MakeArmPart("Upper" + a, null, upperMesh, skin, fabric);
                fore[a] = MakeArmPart("Fore" + a, foreSkin, foreFabric, skin, fabric);
                Hands[a] = BuildHand(a, skin);
                Vector3 ready;
                Quaternion readyRot;
                float readyOpen;
                ComputePose(Hands[a].Side, 0f, 0f, out ready, out readyRot, out readyOpen);
                Hands[a].Pos = ready;
                Hands[a].Rot = readyRot;
                FinishJoints(Hands[a]);
            }

            BuildArmDisplay();
        }

        /// <summary>Нога с сапогом, качается в бедре (шаг): сустав на высоте бедра, брюки вниз, носок вперёд.</summary>
        Transform MakeLeg(float x, Material pants, Material boots)
        {
            GameObject pv = new GameObject("Leg");
            pv.transform.SetParent(bodyVis, false);
            pv.transform.localPosition = new Vector3(x, 0.76f, 0f);
            Gfx.Prim(PrimitiveType.Cube, pv.transform, new Vector3(0f, -0.38f, 0f), new Vector3(0.16f, 0.76f, 0.20f), pants, false);
            Gfx.Prim(PrimitiveType.Cube, pv.transform, new Vector3(0f, -0.715f, 0.05f), new Vector3(0.175f, 0.11f, 0.32f), boots, false);
            return pv.transform;
        }

        // ------------------------------------------------------------------ рука: рукав, предплечье, полоска здоровья

        static readonly Color32 White32 = new Color32(255, 255, 255, 255);

        Transform MakeArmPart(string name, Mesh skinMesh, Mesh fabricMesh, Material skin, Material fabric)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(bodyRoot, false);
            if (skinMesh != null) Gfx.MeshObject(root.transform, skinMesh, skin, "Skin");
            if (fabricMesh != null) Gfx.MeshObject(root.transform, fabricMesh, fabric, "Fabric");
            return root.transform;
        }

        /// <summary>Рукав от плеча (y = 0) к локтю (y = 1): слегка сужается, у локтя закатанный край.</summary>
        static Mesh BuildUpperMesh()
        {
            MeshBatch b = new MeshBatch();
            b.Frustum(new Vector3(0f, 0.5f, 0f), Vector3.up, 0.057f, 0.050f, 1.0f, 10, White32, false, true);
            b.Frustum(new Vector3(0f, 0.93f, 0f), Vector3.up, 0.0535f, 0.0535f, 0.14f, 10, new Color32(212, 212, 212, 255), false, true);
            Mesh m = b.Build("PG_ArmUpper");
            return m;
        }

        /// <summary>Кожа предплечья от локтя (y = 0) к запястью (y = 1): у локтя утолщение, к запястью сужение.</summary>
        static Mesh BuildForeSkinMesh()
        {
            MeshBatch b = new MeshBatch();
            b.Frustum(new Vector3(0f, 0.225f, 0f), Vector3.up, 0.040f, 0.046f, 0.45f, 10, White32, false, false);
            b.Frustum(new Vector3(0f, 0.725f, 0f), Vector3.up, 0.046f, 0.028f, 0.55f, 10, White32, false, true);
            return b.Build("PG_ArmForeSkin");
        }

        /// <summary>Закатанный рукав у локтя и тёмный ремешок на запястье.</summary>
        static Mesh BuildForeFabricMesh()
        {
            MeshBatch b = new MeshBatch();
            b.Frustum(new Vector3(0f, 0.06f, 0f), Vector3.up, 0.054f, 0.052f, 0.13f, 10, White32, true, true);
            b.Frustum(new Vector3(0f, 0.12f, 0f), Vector3.up, 0.0565f, 0.0565f, 0.035f, 10, new Color32(190, 190, 190, 255), false, true);
            b.Frustum(new Vector3(0f, 0.948f, 0f), Vector3.up, 0.0305f, 0.0295f, 0.075f, 10, new Color32(86, 66, 52, 255), true, true);
            return b.Build("PG_ArmForeFabric");
        }

        static float ForeRadius(float t)
        {
            return t <= 0.45f ? Mathf.Lerp(0.040f, 0.046f, t / 0.45f) : Mathf.Lerp(0.046f, 0.028f, (t - 0.45f) / 0.55f);
        }

        /// <summary>
        /// Полоса на предплечье со стороны ладони (локальная +Z предплечья), повторяющая его сужение. Картинка: вдоль руки —
        /// длина полоски здоровья (от локтя к запястью растут перья), поперёк — высота перьев.
        /// </summary>
        static float Soft(float f, float margin)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f / margin)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - f) / margin));
        }

        /// <summary>
        /// inlay = true — подложка: мягкое тёмное пятно (альфа в цвете вершин, края тают), иначе — светящаяся картинка
        /// (яркость в цвете вершин, края тают): ни рамки, ни прямоугольного экрана, полоска будто вжата в кожу.
        /// </summary>
        static Mesh BuildArmDisplayMesh(float lift, bool inlay)
        {
            int nu = 14;
            int nv = 6;
            Vector3[] vs = new Vector3[(nu + 1) * (nv + 1)];
            Vector2[] uvs = new Vector2[vs.Length];
            Color32[] cs = new Color32[vs.Length];
            for (int i = 0; i <= nu; i++)
            {
                float f = i / (float)nu;
                float t = Mathf.Lerp(ArmT0, ArmT1, f);
                float r = ForeRadius(t) + lift;
                for (int j = 0; j <= nv; j++)
                {
                    float g = j / (float)nv;
                    float ang = Mathf.Lerp(-ArmArcDeg * 0.5f, ArmArcDeg * 0.5f, g) * Mathf.Deg2Rad;
                    int vi = i * (nv + 1) + j;
                    vs[vi] = new Vector3(Mathf.Sin(ang) * r, t, Mathf.Cos(ang) * r);
                    uvs[vi] = new Vector2(Mathf.Lerp(ArmU0, ArmU1, f), g);
                    if (inlay)
                    {
                        byte al = (byte)(255f * Soft(f, 0.22f) * Soft(g, 0.5f));
                        cs[vi] = new Color32(255, 255, 255, al);
                    }
                    else
                    {
                        byte lum = (byte)(255f * Soft(f, 0.10f) * Soft(g, 0.20f));
                        cs[vi] = new Color32(lum, lum, lum, 255);
                    }
                }
            }
            int[] tris = new int[nu * nv * 6];
            int k = 0;
            for (int i = 0; i < nu; i++)
            {
                for (int j = 0; j < nv; j++)
                {
                    int a0 = i * (nv + 1) + j;
                    int a1 = a0 + 1;
                    int b0 = a0 + (nv + 1);
                    int b1 = b0 + 1;
                    tris[k++] = a0; tris[k++] = b1; tris[k++] = b0;
                    tris[k++] = a0; tris[k++] = a1; tris[k++] = b1;
                }
            }
            Mesh m = new Mesh();
            m.name = "PG_ArmDisplay";
            m.vertices = vs;
            m.uv = uvs;
            m.colors32 = cs;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Полоска здоровья на левом предплечье: светящиеся перья прямо на коже (как таймер в «Времени») и тёмная подложка под ними.</summary>
        void BuildArmDisplay()
        {
            Material inlay = Gfx.Unlit(new Color(0.025f, 0.015f, 0.012f, 0.93f), null, false, true, 0f);
            armInlay = Gfx.MeshObject(fore[0], BuildArmDisplayMesh(0.0016f, true), inlay, "HealthInlay");
            armDisplayMat = Gfx.Unlit(new Color(0.9f, 0.9f, 0.9f, 1f), null, true, false, 0f);
            armDisplayMat.SetFloat("_AlphaOne", 1f);   // картинка складывается с кожей по цвету: чёрное не видно
            armDisplay = Gfx.MeshObject(fore[0], BuildArmDisplayMesh(0.0034f, false), armDisplayMat, "HealthDisplay");
            armInlay.SetActive(false);
            armDisplay.SetActive(false);
        }

        /// <summary>Показать полоску, пока рука поднята: просим у неё рендер и кладём картинку на предплечье.</summary>
        void UpdateArmDisplay(float raise)
        {
            if (armDisplay == null) return;
            GameManager gm = GameManager.Instance;
            FeatherHealthBar bar = gm != null ? gm.Bar : null;
            bool show = bar != null && raise > 0.2f && !HideBody;
            if (show)
            {
                bar.Request(ArmPxW, ArmPxH);
                RenderTexture t = bar.Texture;
                if (t == null) show = false;   // первый кадр: картинки ещё нет
                else if (armDisplayMat.mainTexture != t) armDisplayMat.mainTexture = t;
            }
            if (armDisplay.activeSelf != show)
            {
                armDisplay.SetActive(show);
                armInlay.SetActive(show);
            }
            if (show)
            {
                float k = Mathf.InverseLerp(0.2f, 0.85f, raise);
                armDisplayMat.SetColor("_Color", new Color(0.9f * k, 0.9f * k, 0.9f * k, 1f));
            }
        }

        // ------------------------------------------------------------------ кисть

        static Transform MakeJoint(Transform parent, Vector3 localPos)
        {
            GameObject g = new GameObject("Joint");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            return g.transform;
        }

        static void AddSeg(Transform joint, float len, float th, Material m)
        {
            Gfx.Prim(PrimitiveType.Cube, joint, new Vector3(0f, 0f, len * 0.5f), new Vector3(th, th * 0.9f, len * 1.03f), m, false);
        }

        /// <summary>
        /// Локальные оси кисти: +Z вдоль пальцев, +Y — тыльная сторона, ладонь смотрит в -Y.
        /// Якорь ладони (Palm) повёрнут так, что его +Z — это нормаль ладони (оттуда вылетают лучи).
        /// Суставы собираются в PalmShot.HandFx: baseRot задаёт позу, openAngle — на сколько раскрывается палец.
        /// </summary>
        HandRig BuildHand(int a, Material skin)
        {
            float side = a == 0 ? -1f : 1f;
            float s = HandScale;
            HandRig h = new HandRig();
            h.Side = side;
            GameObject rg = new GameObject(a == 0 ? "HandL" : "HandR");
            h.Root = rg.transform;
            h.Root.SetParent(HandsRoot, false);

            Gfx.Prim(PrimitiveType.Cube, h.Root, Vector3.zero, new Vector3(0.086f, 0.026f, 0.090f) * s, skin, false);

            // Якорь ладони (контракт PalmShot): пустышка, ничего не рисует
            GameObject pg = new GameObject("Palm");
            h.Palm = pg.transform;
            h.Palm.SetParent(h.Root, false);
            h.Palm.localPosition = new Vector3(0f, -0.013f * s, 0f);
            h.Palm.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            int jn = FingerLen.Length;
            h.CurlMax = new float[jn];
            h.CurlExt = new float[jn];
            h.BaseY = new float[jn];
            h.Fx = new PalmShot.HandFx();
            h.Fx.palm = h.Palm;
            h.Fx.joints = new PalmShot.FingerJoint[jn];

            float[] curlMaxPhalanx = { 84f, 98f, 64f };
            float[] curlExtPhalanx = { -4f, -2f, -2f };
            float[] thick = { 0.017f, 0.0155f, 0.014f };
            int ji = 0;
            for (int f = 0; f < 4; f++)
            {
                float fx = side * (-0.027f + 0.018f * f);
                Transform parent = h.Root;
                Vector3 at = new Vector3(fx * s, 0f, 0.043f * s);
                for (int k = 0; k < 3; k++)
                {
                    float len = FingerLen[ji] * s;
                    Transform jt = MakeJoint(parent, at);
                    AddSeg(jt, len, thick[k] * s, skin);
                    h.CurlMax[ji] = curlMaxPhalanx[k];
                    h.CurlExt[ji] = curlExtPhalanx[k];
                    h.BaseY[ji] = 0f;
                    SetFxJoint(h, ji, jt);
                    parent = jt;
                    at = new Vector3(0f, 0f, len);
                    ji++;
                }
            }

            // большой палец: основание сбоку ладони, повёрнуто к центру
            Transform tb = MakeJoint(h.Root, new Vector3(-side * 0.045f * s, -0.004f * s, -0.010f * s));
            AddSeg(tb, FingerLen[12] * s, 0.019f * s, skin);
            h.CurlMax[12] = 44f;
            h.CurlExt[12] = 0f;
            h.BaseY[12] = -side * 32f;
            SetFxJoint(h, 12, tb);
            Transform tt = MakeJoint(tb, new Vector3(0f, 0f, FingerLen[12] * s));
            AddSeg(tt, FingerLen[13] * s, 0.017f * s, skin);
            h.CurlMax[13] = 62f;
            h.CurlExt[13] = -6f;
            h.BaseY[13] = 0f;
            SetFxJoint(h, 13, tt);
            return h;
        }

        /// <summary>Сустав для PalmShot: openAngle — поворот от «готовности» до полностью раскрытой кисти.</summary>
        static void SetFxJoint(HandRig h, int i, Transform jt)
        {
            PalmShot.FingerJoint fj = new PalmShot.FingerJoint();
            fj.t = jt;
            fj.openAngle = 0f;   // уточняется в FinishJoints
            fj.baseRot = Quaternion.identity;
            h.Fx.joints[i] = fj;
        }

        static void FinishJoints(HandRig h)
        {
            for (int i = 0; i < h.Fx.joints.Length; i++)
            {
                float ready = Mathf.Lerp(h.CurlMax[i], h.CurlExt[i], ReadyOpen);
                h.Fx.joints[i].openAngle = -(ready - h.CurlExt[i]) * 1.1f;
            }
        }

        /// <summary>Опорная поза пальцев (open: 0 — кулак, 1 — раскрытая кисть). PalmShot добавит к ней открытие при выстреле.</summary>
        static void SetFingerBase(HandRig h, float open)
        {
            for (int i = 0; i < h.Fx.joints.Length; i++)
            {
                float c = Mathf.Lerp(h.CurlMax[i], h.CurlExt[i], open);
                Quaternion q = Quaternion.Euler(c, h.BaseY[i], 0f);
                h.Fx.joints[i].baseRot = q;
                h.Fx.joints[i].t.localRotation = q;
            }
        }

        // ------------------------------------------------------------------ позы рук

        /// <summary>
        /// Позиция и поворот кисти в пространстве тела. Обычная поза — «готовность» PalmShot: ладони вперёд, пальцы вверх.
        /// </summary>
        void ComputePose(float sx, float ld, float attackW, out Vector3 pos, out Quaternion rot, out float open)
        {
            Vector3 readyPos = new Vector3(sx * 0.25f, 1.38f, 0.45f);
            Quaternion readyRot = Quaternion.LookRotation(new Vector3(sx * 0.14f, 1f, 0.30f), new Vector3(sx * 0.15f, 0f, -1f));
            Vector3 lowPos = new Vector3(sx * 0.21f, 1.14f, 0.30f);
            Quaternion lowRot = Quaternion.LookRotation(new Vector3(-sx * 0.05f, -0.6f, 1f), new Vector3(sx, 0.2f, 0f));
            pos = Vector3.Lerp(readyPos, lowPos, ld);
            rot = Quaternion.Slerp(readyRot, lowRot, ld);
            open = Mathf.Lerp(ReadyOpen, 0.45f, ld);

            bool cover = DebugPose == 1 || (p != null && p.Hero.Covered);
            bool exhale = DebugPose == 2 || (p != null && p.Exhaling);
            bool inhale = DebugPose == 3 || (p != null && p.Inhaling);

            if (cover)
            {
                // ладони на груди: прикрыть огонь
                pos = new Vector3(sx * 0.07f, 1.25f, 0.25f);
                rot = Quaternion.LookRotation(new Vector3(-sx, 0.25f, 0.1f), Vector3.forward);
                open = 0.4f;
            }
            else if (exhale)
            {
                // выдох: подношение ладонями вперёд-вверх
                pos = new Vector3(sx * 0.14f, 1.40f, 0.66f);
                rot = Quaternion.LookRotation(new Vector3(-sx * 0.1f, 0.35f, 1f), new Vector3(0f, -0.7f, -0.3f));
                open = 0.75f;
            }
            else if (inhale)
            {
                // вдох: ладони чашей у груди
                pos = new Vector3(sx * 0.11f, 1.27f, 0.23f);
                rot = Quaternion.LookRotation(new Vector3(-sx * 0.35f, 0.15f, 1f), Vector3.down);
                open = 0.2f;
            }

            if (attackW > 0.01f)
            {
                // выстрел/вспышка: кисти чуть вперёд, ладони на цель
                Vector3 ap = new Vector3(sx * 0.22f, 1.43f, 0.58f);
                Quaternion ar = Quaternion.LookRotation(new Vector3(sx * 0.10f, 1f, 0.10f), new Vector3(sx * 0.10f, 0f, -1f));
                pos = Vector3.Lerp(pos, ap, attackW);
                rot = Quaternion.Slerp(rot, ar, attackW);
                open = Mathf.Lerp(open, ReadyOpen, attackW);
            }
        }

        /// <summary>
        /// Поза «посмотреть на часы»: левое предплечье лежит поперёк взгляда чуть ниже его центра, ладонью к глазам, пальцы вправо.
        /// Всё считается в системе головы (x вправо, y вверх, z по взгляду) и переводится в пространство тела: при любом взгляде вниз
        /// предплечье остаётся целиком в кадре, а полоска на нём смотрит на героя.
        /// </summary>
        static void CheckPose(float pitchDeg, out Vector3 palmPos, out Quaternion rot, out Vector3 elbow)
        {
            Quaternion hq = Quaternion.Euler(pitchDeg, 0f, 0f);
            Vector3 dir = new Vector3(0.93f, 0.12f, 0.34f).normalized;      // вдоль предплечья: к запястью
            Vector3 wristH = new Vector3(0.085f, -0.125f, 0.40f);
            Vector3 elbowH = wristH - dir * 0.30f;
            palmPos = EyeLocal + hq * (wristH + dir * 0.05f);
            elbow = EyeLocal + hq * elbowH;
            // пальцы вдоль предплечья, тыл кисти — от глаз (ладонь смотрит на героя)
            rot = hq * Quaternion.LookRotation(dir, new Vector3(0f, 0.2f, 1f));
        }

        void LateUpdate()
        {
            if (p == null || bodyRoot == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float ld = p.LookDownT;
            float attackW = Combat != null ? Combat.PoseWeight : 0f;

            // Взгляд вверх: руки идут за взглядом и остаются в кадре (вниз — наоборот, там тело)
            float pit = p.Head.localEulerAngles.x;
            if (pit > 180f) pit -= 360f;

            // Тело (торс, ноги) попадает в кадр только при взгляде вниз: иначе не рисуем
            bool seeBody = !HideBody && (pit > 12f || ld > 0.02f || p.Inhaling);
            if (bodyVis != null && bodyVis.gameObject.activeSelf != seeBody) bodyVis.gameObject.SetActive(seeBody);
            float upDeg = Mathf.Clamp(-pit, 0f, 80f);
            Quaternion pr = Quaternion.Euler(-upDeg * 0.92f, 0f, 0f);
            if (OverrideW > 0.001f) pr = Quaternion.Slerp(pr, Quaternion.identity, OverrideW);   // сценарий ведёт руки сам

            // Левая рука поднимается к глазам, когда герой смотрит на себя (не при вдохе, выдохе, прикрытии и стрельбе)
            bool busy = DebugPose != 0 || p.Inhaling || p.Exhaling || p.Hero.Covered;
            float raiseTarget = (HideBody || OverrideW > 0.5f || busy) ? 0f : Mathf.InverseLerp(24f, 48f, pit);
            raiseTarget *= 1f - Mathf.Clamp01(attackW * 1.5f);
            armRaise = Mathf.Lerp(armRaise, raiseTarget, 1f - Mathf.Exp(-9f * dt));
            if (armRaise < 0.001f) armRaise = 0f;
            if (armRaise > 0.85f && GameManager.Instance != null) GameManager.Instance.Think("arm", "На руке — мой свет. Пока он горит, я жив.");

            float mv = p.MoveAmount;
            float ph = p.WalkPhase;
            float checkW = Mathf.SmoothStep(0f, 1f, armRaise);
            Vector3 checkElbow = Vector3.zero;
            for (int a = 0; a < 2; a++)
            {
                HandRig h = Hands[a];
                float sx = h.Side;
                Vector3 pos;
                Quaternion rot;
                float open;
                ComputePose(sx, ld, attackW, out pos, out rot, out open);
                if (a == 0 && armRaise > 0.001f)
                {
                    Vector3 cp;
                    Quaternion cr;
                    CheckPose(pit, out cp, out cr, out checkElbow);
                    pos = Vector3.Lerp(pos, cp, checkW);
                    rot = Quaternion.Slerp(rot, cr, checkW);
                    open = Mathf.Lerp(open, 0.62f, checkW);
                }
                if (OverrideW > 0.001f)
                {
                    pos = Vector3.Lerp(pos, OverridePos[a], OverrideW);
                    rot = Quaternion.Slerp(rot, OverrideRot[a], OverrideW);
                    open = Mathf.Lerp(open, OverrideOpen[a], OverrideW);
                }

                // ходьба: кисти качаются в такт шагам (вверх-вниз, вперёд-назад в противофазе, чуть в стороны, поворачиваются)
                float walk = mv * (1f - 0.8f * armRaise * (a == 0 ? 1f : 0f));
                pos.y += Mathf.Sin(ph * 2f + a * Mathf.PI) * 0.030f * walk;
                pos.z += Mathf.Cos(ph + a * Mathf.PI) * 0.040f * walk;
                pos.x += Mathf.Sin(ph) * 0.014f * walk * sx;
                rot = rot * Quaternion.Euler(0f, 0f, Mathf.Sin(ph + a * Mathf.PI) * 6f * walk);
                // дыхание
                pos.y += Mathf.Sin(Time.time * 1.6f + a) * 0.004f;

                h.Pos = Vector3.Lerp(h.Pos, pos, 1f - Mathf.Exp(-14f * dt));
                h.Rot = Quaternion.Slerp(h.Rot, rot, 1f - Mathf.Exp(-12f * dt));
                h.Open = Mathf.Lerp(h.Open, open, 1f - Mathf.Exp(-12f * dt));

                Vector3 pp = EyeLocal + pr * (h.Pos - EyeLocal);
                Quaternion rr = pr * h.Rot;
                Vector3 reach = pp - shoulder[a];
                float maxReach = MaxReach + OverrideW * 0.25f;   // сценарий может тянуться чуть дальше, чем в игре
                if (reach.magnitude > maxReach) pp = shoulder[a] + reach.normalized * maxReach;

                // HandsRoot стоит в точке глаз и без поворота: локальные координаты кисти — смещение от глаз
                h.Root.localPosition = pp - EyeLocal;
                h.Root.localRotation = rr;
                SetFingerBase(h, h.Open);
            }

            // Лучи, шары, вспышка, отдача рук и раскрытие пальцев (PalmShot) — после того, как кисти заняли позу
            if (Combat != null) Combat.Tick(dt);

            for (int a = 0; a < 2; a++)
            {
                HandRig h = Hands[a];
                float sx = h.Side;

                // рука: плечо -> локоть -> запястье (после отдачи); сторона ладони задаёт, куда смотрит внутренняя сторона предплечья
                Vector3 wrist = bodyRoot.InverseTransformPoint(h.Root.TransformPoint(new Vector3(0f, 0f, -0.05f)));
                Vector3 mid = (shoulder[a] + wrist) * 0.5f;
                Vector3 elbow = mid + new Vector3(sx * 0.07f, -0.11f, -0.04f);
                if (a == 0 && checkW > 0.001f) elbow = Vector3.Lerp(elbow, checkElbow, checkW);   // «часы»: локоть выставлен вперёд, предплечье поперёк взгляда
                Vector3 palmDir = bodyRoot.InverseTransformDirection(h.Root.TransformDirection(Vector3.down));
                if (a == 0 && checkW > 0.001f)
                {
                    // «часы»: полоска смотрит прямо в глаза, а не туда, куда повёрнута ладонь
                    Vector3 toEye = (EyeLocal - (elbow + wrist) * 0.5f).normalized;
                    palmDir = Vector3.Slerp(palmDir, toEye, checkW);
                }
                PlaceSegment(upper[a], shoulder[a], elbow, Vector3.back, ArmScale);
                PlaceSegment(fore[a], elbow, wrist, palmDir, ArmScale);
            }

            // Ноги шагают при ходьбе (видно, если смотреть вниз): качаются в бёдрах, в противофазе
            float swing = Mathf.Sin(ph) * 30f * mv;
            legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            UpdateArmDisplay(armRaise);
        }

        /// <summary>
        /// Ставит сегмент руки: начало в точке a, ось +Y — к точке b (длина вдоль Y), +Z меша — в сторону normalHint
        /// (внутренняя сторона предплечья). thick — множитель толщины.
        /// </summary>
        static void PlaceSegment(Transform t, Vector3 a, Vector3 b, Vector3 normalHint, float thick)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) return;
            Vector3 axis = d / len;
            Vector3 n = normalHint - axis * Vector3.Dot(normalHint, axis);
            if (n.sqrMagnitude < 1e-4f) n = Vector3.Cross(axis, Vector3.right);
            t.localPosition = a;
            t.localRotation = Quaternion.LookRotation(n.normalized, axis);
            t.localScale = new Vector3(thick, len, thick);
        }
    }
}
