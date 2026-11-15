using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Жизненный свет героя: здоровье, аура, постоянный расход ("тление") и "последний вздох".
    /// Герой сам является источником света для игры и для шейдеров (ILightSource).
    /// </summary>
    public class HeroLight : MonoBehaviour, ILightSource
    {
        public static HeroLight Instance;

        public float Life = GameConfig.StartLight;
        public float AuraTarget = GameConfig.AuraStart;
        public bool Covered;
        public bool InLastBreath;
        public float LastBreathLeft;
        public float DamageFlash;   // 0..1, красная вспышка на экране
        public float FlashPulse;    // 0..1, вспышка после умения
        public float GlowScale = 1f; // 0..1: насколько герой светится (в предыстории свет появляется после ожога)

        float auraCurrent = GameConfig.AuraStart;
        bool hearthMsgShown;      // подсказка про очаг показывается один раз за игру
        float seed;
        float lastBreathElapsed;
        float hitSoundCd;
        Transform head;
        AudioSource sfx;

        public float Life01 { get { return Mathf.Clamp01(Life / GameConfig.MaxLight); } }

        public void Init(Transform headTransform)
        {
            Instance = this;
            head = headTransform;
            seed = Random.value * 100f;
            sfx = ProcAudio.Source(gameObject, "hit", false, 1f, false);
            Register();
        }

        public void Register()
        {
            LightField.Register(this);
        }

        void OnDestroy()
        {
            LightField.Unregister(this);
            if (Instance == this) Instance = null;
        }

        public void ResetFor(float life, bool respawn = false)
        {
            if (respawn) SetLife(life, LightReason.Respawn);
            else Life = life;
            InLastBreath = false;
            LastBreathLeft = 0f;
            Covered = false;
            DamageFlash = 0f;
            FlashPulse = 0f;
            GlowScale = 1f;
            hearthMsgShown = false;
            AuraTarget = GameConfig.AuraStart;
            auraCurrent = GameConfig.AuraStart;
        }

        /// <summary>Получить свет (вдох у лампы, спасённые рядом, осколки).</summary>
        public void Gain(float v, LightReason reason = LightReason.Other)
        {
            if (v <= 0f || InLastBreath) return;
            SetLife(Mathf.Min(GameConfig.MaxLight, Life + v), reason);
        }

        /// <summary>Добровольная трата (умения, выдох). Не убивает: оставляет хотя бы 0.5.</summary>
        public void Spend(float v, LightReason reason = LightReason.Other)
        {
            if (v <= 0f) return;
            SetLife(Mathf.Max(0.5f, Life - v), reason);
        }

        public void SetLife(float value, LightReason reason)
        {
            float before = Life;
            Life = Mathf.Clamp(value, 0f, GameConfig.MaxLight);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.Economy.Record(reason, Life - before, gm.PlayTime);
        }

        void Burn(float amount, LightReason reason)
        {
            if (amount > 0f) SetLife(Life - amount, reason);
        }

        /// <summary>Урон от врагов: вспышка на экране и звук.</summary>
        public void Damage(float v, LightReason reason = LightReason.Enemy)
        {
            if (v <= 0f) return;
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            Burn(v, reason);
            DamageFlash = Mathf.Clamp01(DamageFlash + v * 0.08f);
            if (hitSoundCd <= 0f)
            {
                hitSoundCd = 0.35f;
                ProcAudio.OneShot(sfx, "hit", 0.8f, Random.Range(0.9f, 1.1f));
            }
            if (Life <= 0f)
            {
                Life = 0f;
                EnterLastBreath();
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            DamageFlash = Mathf.MoveTowards(DamageFlash, 0f, dt * 2.0f);
            FlashPulse = Mathf.MoveTowards(FlashPulse, 0f, dt * 3.5f);
            hitSoundCd -= dt;

            // Радиус ауры плавно следует цели; при малом свете аура сжимается сама.
            float r = Covered ? GameConfig.CoverRadius : AuraTarget;
            r *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(Life01 * 3f));
            if (InLastBreath) r = 1.5f;
            auraCurrent = Mathf.Lerp(auraCurrent, r, 1f - Mathf.Exp(-9f * dt));

            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing || gm.Paused) return;

            if (InLastBreath)
            {
                UpdateLastBreath(dt);
                return;
            }

            // Расход: тление + содержание ауры (растёт с квадратом радиуса).
            float drain = GameConfig.SmolderPerSec;
            float rr = Covered ? GameConfig.CoverRadius : AuraTarget;
            float auraCost = GameConfig.AuraCostK * rr * rr;
            if (Covered) auraCost *= GameConfig.CoverCostMul;
            drain += auraCost;
            if (gm.Dir != null && gm.Dir.InHearth)
            {
                // у очага тление не капает, а свет понемногу возвращается — очень медленно
                drain = 0f;
                if (Life < GameConfig.MaxLight)
                {
                    Gain(GameConfig.HearthRegenPerSec * dt, LightReason.Hearth);
                    if (!hearthMsgShown && Life < GameConfig.MaxLight - 1f && gm.PlayTime > 14f)   // не раньше первой мысли («рука всё ещё светится»)
                    {
                        hearthMsgShown = true;
                        gm.Think("hearth", "У очага свет возвращается. Понемногу — но возвращается.");
                    }
                }
            }
            if (drain > 0f)
            {
                Burn(GameConfig.SmolderPerSec * GameDifficulty.DrainScale * dt, LightReason.Smolder);
                Burn(auraCost * GameDifficulty.DrainScale * dt, LightReason.Aura);
            }
            if (Life <= 0f)
            {
                Life = 0f;
                EnterLastBreath();
            }
        }

        void EnterLastBreath()
        {
            if (InLastBreath) return;
            InLastBreath = true;
            LastBreathLeft = GameConfig.LastBreathTime;
            lastBreathElapsed = 0f;
            Life = 0f;
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.Notify("Свет гаснет… последний вздох");
        }

        /// <summary>
        /// Свет на нуле: ~8 секунд герой ещё тлеет. Спасённые рядом могут раздуть его обратно,
        /// отдав часть своего света (тем больше, чем больше в них было вложено).
        /// </summary>
        void UpdateLastBreath(float dt)
        {
            LastBreathLeft -= dt;
            lastBreathElapsed += dt;
            Life = 0f;

            if (lastBreathElapsed >= GameConfig.LastBreathDelay)
            {
                float total = 0f;
                Vector3 p = transform.position;
                for (int i = 0; i < Survivor.All.Count; i++)
                {
                    Survivor s = Survivor.All[i];
                    if (!s.IsAwake || s.Health < 20f) continue;
                    if ((s.transform.position - p).magnitude > 6.5f) continue;
                    float give = Mathf.Min(s.Health * 0.5f, 10f + s.Invested * 0.8f);
                    s.GiveLight(give);
                    total += give * 0.8f;
                }
                if (total > 1f)
                {
                    SetLife(total, LightReason.Rescue);
                    InLastBreath = false;
                    FlashPulse = 1f;
                    DamageFlash = 0f;
                    GameManager gmOk = GameManager.Instance;
                    if (gmOk != null) gmOk.Notify("Они вернули долг света");
                    ProcAudio.OneShot(sfx, "wake", 0.9f, 1f);
                    TimedFx.Spawn(LightPosition, 2f, 14f, new Color(1f, 0.85f, 0.5f, 0.8f), 0.6f);
                    return;
                }
            }

            if (LastBreathLeft <= 0f)
            {
                InLastBreath = false;
                Life = 0f;
                GameManager gmDead = GameManager.Instance;
                if (gmDead != null) gmDead.OnHeroDied();
            }
        }

        // ------------------------------------------------------------- ILightSource

        public Vector3 LightPosition
        {
            get
            {
                if (head != null) return head.position + head.forward * 0.3f;
                return transform.position + Vector3.up * 1.6f;
            }
        }

        public float LightRadius
        {
            get { return (auraCurrent + FlashPulse * 6f) * GlowScale; }
        }

        public float LightIntensity
        {
            get
            {
                float b = Mathf.Lerp(0.55f, 1.05f, Mathf.Clamp01(Life01 * 2.5f));
                if (InLastBreath) b = 0.25f;
                else if (Covered) b = 0.45f;
                float flick = 1f - 0.10f * Mathf.PerlinNoise(Time.time * 9f, seed)
                                 - (1f - Life01) * 0.12f * Mathf.PerlinNoise(Time.time * 17f, seed + 5f);
                return (b * flick + FlashPulse * 0.8f) * GlowScale;
            }
        }

        public Color LightColor
        {
            get
            {
                Color c = Color.Lerp(new Color(1f, 0.35f, 0.12f, 1f), new Color(1f, 0.82f, 0.5f, 1f), Mathf.Clamp01(Life01 * 1.8f));
                return Color.Lerp(c, new Color(1f, 0.97f, 0.9f, 1f), FlashPulse * 0.6f);
            }
        }
    }
}
