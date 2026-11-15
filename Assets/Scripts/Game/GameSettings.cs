using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Настройки игрока (меню «Настройки»): громкость, чувствительность мыши, яркость, эффект камкордера,
    /// подписи в мире, тряска камеры. Сохраняются между запусками (PlayerPrefs).
    /// </summary>
    public static class GameSettings
    {
        public const float BrightnessMin = 0.6f;
        public const float BrightnessMax = 1.6f;
        public const float SensitivityMin = 0.5f;
        public const float SensitivityMax = 4.0f;
        public const float FovMin = 60f;
        public const float FovMax = 100f;

        public static float Volume = 0.8f;          // 0..1
        public static float MusicVolume = MusicDirector.DefaultVolume;
        public static float EffectsVolume = 1f;
        public static float AmbienceVolume = 1f;
        public static float InterfaceVolume = 1f;
        public static float FieldOfView = 76f;
        public static float HintScale = 1f;
        public static bool InvertY;
        public static bool PauseOnFocusLost = true;
        public static Challenge Difficulty = Challenge.Standard;
        public static float Sensitivity = 2.0f;     // множитель обзора мышью
        public static float Brightness = 1.0f;      // яркость картинки (клавиши - и = меняют её же)
        public static bool Hints = true;            // подписи над важными местами в мире
        public static bool Shake = true;            // тряска камеры от вспышек и лучей

        static bool camcorder = true;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLoaded() { loaded = false; }

        /// <summary>Эффект камкордера: линза, зерно, строки. Меняется и клавишей F2.</summary>
        public static bool Camcorder
        {
            get { return LowRes.Instance != null ? LowRes.Instance.Enabled : camcorder; }
            set
            {
                camcorder = value;
                if (LowRes.Instance != null) LowRes.Instance.Enabled = value;
            }
        }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                Volume = Mathf.Clamp01(PlayerPrefs.GetFloat("pg_volume", 0.8f));
                MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("pg_music", MusicDirector.DefaultVolume));
                EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("pg_effects", 1f));
                AmbienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("pg_ambience", 1f));
                InterfaceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("pg_interface", 1f));
                FieldOfView = Mathf.Clamp(PlayerPrefs.GetFloat("pg_fov", 76f), FovMin, FovMax);
                HintScale = Mathf.Clamp(PlayerPrefs.GetFloat("pg_text", 1f), 0.8f, 1.5f);
                InvertY = PlayerPrefs.GetInt("pg_invert_y", 0) != 0;
                PauseOnFocusLost = PlayerPrefs.GetInt("pg_pause_focus", 1) != 0;
                Difficulty = (Challenge)Mathf.Clamp(PlayerPrefs.GetInt("pg_difficulty", 1), 0, 2);
                Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("pg_sens", 2.0f), SensitivityMin, SensitivityMax);
                Brightness = Mathf.Clamp(PlayerPrefs.GetFloat("pg_bright", 1.0f), BrightnessMin, BrightnessMax);
                camcorder = PlayerPrefs.GetInt("pg_cam", 1) != 0;
                Hints = PlayerPrefs.GetInt("pg_hints", 1) != 0;
                Shake = PlayerPrefs.GetInt("pg_shake", 1) != 0;
            }
            catch (System.Exception)
            {
                // без сохранённых настроек: значения по умолчанию
            }
        }

        public static void Save()
        {
            try
            {
                PlayerPrefs.SetFloat("pg_volume", Volume);
                PlayerPrefs.SetFloat("pg_music", MusicVolume);
                PlayerPrefs.SetFloat("pg_effects", EffectsVolume);
                PlayerPrefs.SetFloat("pg_ambience", AmbienceVolume);
                PlayerPrefs.SetFloat("pg_interface", InterfaceVolume);
                PlayerPrefs.SetFloat("pg_fov", FieldOfView);
                PlayerPrefs.SetFloat("pg_text", HintScale);
                PlayerPrefs.SetInt("pg_invert_y", InvertY ? 1 : 0);
                PlayerPrefs.SetInt("pg_pause_focus", PauseOnFocusLost ? 1 : 0);
                PlayerPrefs.SetInt("pg_difficulty", (int)Difficulty);
                PlayerPrefs.SetFloat("pg_sens", Sensitivity);
                PlayerPrefs.SetFloat("pg_bright", Brightness);
                PlayerPrefs.SetInt("pg_cam", camcorder ? 1 : 0);
                PlayerPrefs.SetInt("pg_hints", Hints ? 1 : 0);
                PlayerPrefs.SetInt("pg_shake", Shake ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (System.Exception)
            {
                // сохранить не вышло: игра продолжается с текущими значениями
            }
        }

        /// <summary>Применить настройки к живой игре.</summary>
        public static void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(Volume);
            AudioMix.ApplyAll();
            GameDifficulty.Mode = Difficulty;
            Gfx.SetBrightness(Brightness);
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Player != null) gm.Player.MouseSens = Sensitivity;
            if (gm != null && gm.Player != null && gm.State != GameState.Intro)
                gm.Player.Cam.fieldOfView = Mathf.Clamp(FieldOfView, FovMin, FovMax);
            if (LowRes.Instance != null) LowRes.Instance.Enabled = camcorder;
        }

        /// <summary>Клавиши - и =: яркость на шаг.</summary>
        public static void AddBrightness(float delta)
        {
            Brightness = Mathf.Clamp(Mathf.Round((Brightness + delta) * 100f) / 100f, BrightnessMin, BrightnessMax);
            Gfx.SetBrightness(Brightness);
            Save();
        }
    }
}
