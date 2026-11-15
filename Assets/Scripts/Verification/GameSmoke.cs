using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace PokaGoryu
{
    /// <summary>Opt-in player regression: -pg-smoke -pg-smoke-out "folder". Uses the real gameplay input path.</summary>
    public sealed class GameSmoke : MonoBehaviour
    {
        public static bool Running { get; private set; }
        public static string OutputDirectory { get; private set; }
        readonly StringBuilder report = new StringBuilder();
        GameManager gm;
        ReplayInput input;
        string output;
        int failures;
        bool finished;
        float began;

        public static void AttachIfRequested(GameManager manager)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-pg-smoke") < 0) return;
            manager.gameObject.AddComponent<GameSmoke>();
        }
        void Awake()
        {
            Running = true;
            gm = GetComponent<GameManager>();
            input = new ReplayInput();
            GameInput.Provider = input;
            output = Path.Combine(Application.persistentDataPath, "Smoke");
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pg-smoke-out");
            if (i >= 0 && i + 1 < args.Length) output = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(output);
            OutputDirectory = output;
            Application.runInBackground = true;
            // Keep the scenario reproducible without changing saved user preferences.
            GameSettings.Difficulty = Challenge.Standard;
            GameSettings.EffectsVolume = GameSettings.AmbienceVolume = GameSettings.InterfaceVolume = 1f;
            GameSettings.PauseOnFocusLost = false;
            GameSettings.Apply();
            began = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
        }
        void OnLog(string message, string stack, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)
            {
                failures++;
                report.AppendLine(kind + ": " + message + "\n" + stack);
            }
        }
        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - began > 120f)
            { Check(false, "timeout"); Finish(); }
        }
        void Check(bool condition, string message)
        {
            if (!condition) failures++;
            string line = (condition ? "PASS " : "FAIL ") + message;
            report.AppendLine(line);
            Debug.Log("[PGSmoke] " + line);
        }
        IEnumerator Key(GKey key)
        {
            input.Key(key, true);
            yield return null;
            yield return null;
            input.Key(key, false);
        }

        void Approach(Vector3 focus, float distance)
        {
            Vector3 chosen = focus + Vector3.back * distance;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector3 point = new Vector3(focus.x + Mathf.Sin(angle) * distance, 0.1f, focus.z - Mathf.Cos(angle) * distance);
                if (!CityMap.CanStand(point.x, point.z, 0.5f, true)) continue;
                bool blocked = false;
                foreach (Collider c in Physics.OverlapSphere(point + Vector3.up * 0.9f, 0.42f))
                    if (!c.isTrigger && !c.transform.IsChildOf(gm.Player.transform)) { blocked = true; break; }
                if (blocked) continue;
                chosen = point;
                break;
            }
            chosen.y = 0.1f;
            Vector3 toward = focus - chosen;
            float yaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            gm.Player.TeleportTo(chosen, yaw);
            Vector3 look = focus - gm.Player.Cam.transform.position;
            float pitch = -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
            input.LookOnce(new Vector2(0f, -pitch / gm.Player.MouseSens * (GameSettings.InvertY ? -1f : 1f)));
        }

        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pg-ai") >= 0)
            {
                yield return AiChecks();
                Finish();
                yield break;
            }
            // Settings are never saved by this scenario. Store anything temporarily adjusted for a check.
            yield return new WaitForSecondsRealtime(1.5f);
            Check(gm.State == GameState.Title && FindMusic().Length == 4, "title and four music stems");
            Check(LevelBuilder.PizzasPlaced == 8 && LevelBuilder.PizzasMissed == 0 && Survivor.All.Count == 10,
                "golden layout: 8 pizzas, 10 survivors");
            yield return Key(GKey.Enter);
            yield return new WaitForSecondsRealtime(1.2f);
            Check(gm.State == GameState.Intro, "Enter starts intro through menu input");
            yield return Key(GKey.Space);
            yield return new WaitForSecondsRealtime(0.8f);
            Check(gm.State == GameState.Playing && !gm.Intro.Active, "Space skips intro through input");

            float fov = GameSettings.FieldOfView;
            GameSettings.FieldOfView = 92f; GameSettings.Apply();
            Check(Mathf.Abs(gm.Player.Cam.fieldOfView - 92f) < 0.01f, "FOV applies live");
            GameSettings.FieldOfView = fov; GameSettings.Apply();
            bool inverted = GameSettings.InvertY;
            gm.Player.TeleportTo(LevelBuilder.StartPos, 0f);
            GameSettings.InvertY = false;
            input.LookOnce(new Vector2(0f, 4f));
            yield return new WaitForSecondsRealtime(0.1f);
            float normalY = gm.Player.Cam.transform.forward.y;
            gm.Player.TeleportTo(LevelBuilder.StartPos, 0f);
            GameSettings.InvertY = true;
            input.LookOnce(new Vector2(0f, 4f));
            yield return new WaitForSecondsRealtime(0.1f);
            Check(normalY > 0f && gm.Player.Cam.transform.forward.y < 0f, "mouse Y inversion reverses live look input");
            GameSettings.InvertY = inverted;

            float effect = GameSettings.EffectsVolume, ambience = GameSettings.AmbienceVolume, ui = GameSettings.InterfaceVolume;
            GameSettings.EffectsVolume = 0f; AudioMix.ApplyAll();
            Check(gm.Player.Sfx.volume == 0f && gm.Player.DroneSrc.volume > 0f, "effect bus mutes independently of ambience");
            GameSettings.EffectsVolume = effect; GameSettings.AmbienceVolume = 0f; AudioMix.ApplyAll();
            Check(gm.Player.DroneSrc.volume == 0f && gm.Player.Sfx.volume > 0f, "ambience bus mutes independently of effects");
            GameSettings.AmbienceVolume = ambience; GameSettings.InterfaceVolume = ui; AudioMix.ApplyAll();
            Check(FindMusic()[0].isPlaying, "bus controls preserve scheduled music");

            Lamp lamp = null;
            foreach (Lamp l in Lamp.All) if (!l.IsHearth && !l.IsDecoy && !l.Dead && l.Charge > 0.8f) { lamp = l; break; }
            Check(lamp != null, "full usable lamp exists");
            if (lamp != null)
            {
                gm.Hero.SetLife(35f, LightReason.Debug);
                Approach(lamp.FocusPoint, 2.1f);
                yield return new WaitForSecondsRealtime(0.2f);
                float charge = lamp.Charge;
                Check(gm.Player.FocusType == FocusKind.Lamp, "gaze selects lamp");
                input.Key(GKey.E, true);
                yield return new WaitForSecondsRealtime(0.7f);
                input.Key(GKey.E, false);
                Check(lamp.Charge < charge && gm.Economy.Amount(LightReason.Lamp, true, false) > 2f,
                    "holding E transfers lamp light and records actual income");
                gm.SetPaused(true);
                yield return new WaitForSecondsRealtime(0.6f);
                bool quiet = true;
                foreach (AudioSource source in gm.Player.GetComponentsInChildren<AudioSource>())
                    if (source.clip == ProcAudio.Get("inhale") && source.volume > 0.001f) quiet = false;
                Check(quiet, "pausing during inhale fades its loop with unscaled time");
                gm.SetPaused(false);
            }

            yield return new WaitForSecondsRealtime(0.3f);
            Survivor sleeper = Survivor.All[0];
            gm.Hero.SetLife(90f, LightReason.Debug);
            Approach(sleeper.FocusPoint, 2f);
            yield return new WaitForSecondsRealtime(0.2f);
            Check(gm.Player.FocusType == FocusKind.Sleeper, "gaze selects sleeping survivor");
            input.Key(GKey.F, true);
            yield return new WaitForSecondsRealtime(1.5f);
            input.Key(GKey.F, false);
            Check(sleeper.IsAwake && gm.SurvivorsWoken > 0 && gm.Economy.Amount(LightReason.Wake, false, false) >= 11f,
                "holding F wakes a survivor through focus / actions / economy");

            Pizza pizza = Pizza.All[0];
            gm.Hero.SetLife(40f, LightReason.Debug);
            Approach(pizza.FocusPoint, 2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Check(gm.Player.FocusType == FocusKind.Pizza, "gaze selects pizza");
            yield return Key(GKey.E);
            yield return new WaitForSecondsRealtime(2f);
            Check(pizza.Finished && pizza.BitesDone == 3 && Math.Abs(gm.Economy.Amount(LightReason.Pizza, true, false) - 24) < 0.01,
                "E eats pizza: three bites, exactly 24 light");

            gm.Hero.SetLife(100f, LightReason.Debug);
            gm.Player.TeleportTo(LevelBuilder.StartPos, 0f);
            yield return null;
            Vector3 before = gm.Player.transform.position;
            input.Key(GKey.W, true); input.Key(GKey.Q, true);
            yield return new WaitForSecondsRealtime(0.8f);
            input.Key(GKey.W, false); input.Key(GKey.Q, false);
            Check(Vector3.Distance(before, gm.Player.transform.position) > 1f, "W moves through native character controller");
            yield return new WaitForSecondsRealtime(0.2f);

            Growth wall = Growth.All[0];
            Vector3 target = wall.Center + Vector3.up * 2f;
            Approach(target, 8f);
            gm.Hero.SetLife(100f, LightReason.Debug);
            input.Mouse(0, true);
            yield return new WaitForSecondsRealtime(4.5f);
            input.Mouse(0, false);
            yield return new WaitForSecondsRealtime(0.5f);
            Check(wall.Dead && gm.Economy.Amount(LightReason.Shot, false, false) >= 4.9 && gm.Economy.Amount(LightReason.Beam, false, false) > 15,
                "holding LMB merges beam and burns blocking darkness wall");
            gm.Hero.SetLife(90f, LightReason.Debug);
            input.Mouse(1, true);
            yield return null; yield return null;
            input.Mouse(1, false);
            yield return new WaitForSecondsRealtime(0.6f);
            Check(gm.Economy.Amount(LightReason.Flash, false, false) >= 19.9, "RMB flash uses its own expense category");

            yield return Key(GKey.Esc);
            yield return new WaitForSecondsRealtime(0.3f);
            Check(gm.Paused && Time.timeScale == 0f, "Escape pauses gameplay");
            double lightAtPause = gm.Economy.ExpectedLight;
            yield return new WaitForSecondsRealtime(0.3f);
            Check(Math.Abs(gm.Economy.ExpectedLight - lightAtPause) < 0.001, "pause does not spend or earn light");
            yield return Key(GKey.Esc);
            yield return new WaitForSecondsRealtime(0.2f);
            Check(!gm.Paused && Time.timeScale == 1f, "Escape resumes gameplay");
            GameSettings.PauseOnFocusLost = true;
            gm.SendMessage("OnApplicationFocus", false);
            Check(gm.Paused && Time.timeScale == 0f, "focus loss pauses an active game");
            gm.SendMessage("OnApplicationFocus", true);
            Check(gm.Paused, "focus return waits for the player to resume");
            gm.SetPaused(false);
            GameSettings.PauseOnFocusLost = false;
            Check(Math.Abs(gm.Economy.ExpectedLight - gm.Hero.Life) < 0.01, "ledger reconciles with actual hero light");

            Enemy.ClearAll();
            Bus bus = Bus.Instance;
            gm.Hero.SetLife(100f, LightReason.Debug);
            Approach(bus.FocusPoint, 1.2f);
            yield return new WaitForSecondsRealtime(0.2f);
            Check(gm.Player.FocusType == FocusKind.Bus, "gaze selects bus door");
            input.Key(GKey.F, true);
            yield return new WaitForSecondsRealtime(5.5f);
            input.Key(GKey.F, false);
            Check(bus.State == Bus.Mode.Running && gm.Dir.FinalStand && bus.Charge >= GameConfig.BusNeed && gm.Economy.Amount(LightReason.FeedBus, false, false) > 0,
                "F charges bus and starts final stand; hero contribution=" + gm.Economy.Amount(LightReason.FeedBus, false, false).ToString("0.0"));
            yield return new WaitForSecondsRealtime(3f);
            MusicDirector music = GetComponent<MusicDirector>();
            Check(music.TargetIntensity == 1f && music.Intensity > 0.85f, "final stand raises adaptive score");
            gm.OnHeroDied();
            yield return Key(GKey.R);
            yield return new WaitForSecondsRealtime(0.4f);
            Check(gm.State == GameState.Playing && gm.Hero.Life > 50f && gm.Economy.Amount(LightReason.Respawn, true, false) > 0,
                "R respawns and accounts for restored light");
            Check(Math.Abs(gm.Economy.ExpectedLight - gm.Hero.Life) < 0.01, "ledger reconciles after respawn");
            gm.Win();
            yield return new WaitForSecondsRealtime(2.8f);
            Check(gm.State == GameState.Victory && FindMusic()[0].volume < 0.001f, "victory fades score");
            string csvPath = Path.Combine(output, "last-run-light.csv");
            Check(File.Exists(csvPath) && File.ReadAllText(csvPath).Contains("Beam"), "local report includes per-action light transfers");
            yield return Key(GKey.Down);
            yield return Key(GKey.Down);
            yield return Key(GKey.Enter);
            var page = typeof(MenuUI).GetField("page", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check(page.GetValue(gm.Menu).ToString() == "Report", "victory menu opens light report through keyboard input");
            yield return Key(GKey.Tab);
            var recent = typeof(MenuUI).GetField("recentReport", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check((bool)recent.GetValue(gm.Menu), "Tab switches report to rolling minute");

            // Warm caches once, then assert that native resources stabilise across world rebuilds.
            gm.QuitToMenu();
            yield return new WaitForSecondsRealtime(0.8f);
            AudioMix.ApplyAll();
            int materialCount = Resources.FindObjectsOfTypeAll<Material>().Length;
            int voiceCount = AudioMix.VoiceCount;
            for (int repeat = 0; repeat < 3; repeat++)
            {
                gm.QuitToMenu();
                yield return new WaitForSecondsRealtime(0.8f);
            }
            AudioMix.ApplyAll();
            int afterMaterials = Resources.FindObjectsOfTypeAll<Material>().Length;
            Check(afterMaterials <= materialCount + 1, "materials stabilise across restarts: " + materialCount + " -> " + afterMaterials);
            Check(AudioMix.VoiceCount == voiceCount, "audio registry releases destroyed world voices");
            Check(FindMusic().Length == 4 && Math.Abs(gm.Economy.TotalIncome) < 0.001 && gm.Economy.TotalExpense == 0,
                "new run resets economy and reuses music stems");
            CheckEffects();

            int beforeOrbs = Resources.FindObjectsOfTypeAll<Material>().Length;
            for (int i = 0; i < 50; i++) LightOrb.Spawn(LevelBuilder.StartPos, 6f);
            yield return null;
            LightOrb.ClearAll();
            yield return null; yield return null;
            int afterOrbs = Resources.FindObjectsOfTypeAll<Material>().Length;
            Check(afterOrbs <= beforeOrbs, "50 discarded orbs release 100 native materials: " + beforeOrbs + " -> " + afterOrbs);
            yield return Key(GKey.Down);
            yield return Key(GKey.Down);
            yield return Key(GKey.Enter);
            Check(page.GetValue(gm.Menu).ToString() == "Settings", "title menu opens settings through keyboard input");
            yield return Key(GKey.Right);
            Check(GameSettings.Difficulty == Challenge.Harsh && GameDifficulty.Mode == Challenge.Harsh, "difficulty selector applies live");
            yield return Key(GKey.Left);
            yield return Key(GKey.Tab);
            yield return Key(GKey.Tab);
            var tab = typeof(MenuUI).GetField("settingsTab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check((int)tab.GetValue(gm.Menu) == 2, "Tab reaches sound settings");
            float oldVolume = GameSettings.Volume;
            yield return Key(oldVolume > 0.5f ? GKey.Left : GKey.Right);
            Check(Mathf.Abs(GameSettings.Volume - oldVolume) > 0.049f, "audio slider responds to keyboard");
            // Do not leave Settings via Back: that would save this synthetic session's values.
            Finish();
        }

        void CheckEffects()
        {
            string[] keys = { "drone", "heart", "hum", "crackle", "inhale", "exhale", "flash", "beam", "wake",
                "lampdie", "shadedie", "hit", "tide", "win", "eater", "shade", "step", "engine", "orb", "munch",
                "fridge", "tick", "powerdown", "rumble", "sizzle", "swell" };
            float maximum = 0f;
            foreach (string key in keys)
            {
                AudioClip clip = ProcAudio.Get(key);
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);
                foreach (float value in samples) maximum = Mathf.Max(maximum, Mathf.Abs(value));
            }
            Check(maximum <= 0.90001f && maximum > 0.1f, "26 procedural effects have sample headroom; max=" + maximum.ToString("0.000"));
        }

        bool Free(Vector3 point)
        {
            if (!CityMap.CanStand(point.x, point.z, 0.5f, true)) return false;
            foreach (Collider c in Physics.OverlapSphere(point + Vector3.up * 0.9f, 0.3f))
                if (!c.isTrigger && !c.transform.IsChildOf(gm.Player.transform) && c.GetComponentInParent<Enemy>() == null && c.GetComponentInParent<Survivor>() == null)
                    return false;
            return true;
        }
        Vector3 NearbyFree(Vector3 center, float radius)
        {
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI / 16f;
                Vector3 p = new Vector3(center.x + Mathf.Sin(angle) * radius, 0.1f, center.z + Mathf.Cos(angle) * radius);
                if (Free(p)) return p;
            }
            return center;
        }
        IEnumerator AiChecks()
        {
            gm.State = GameState.Playing;
            GameConfig.SmolderPerSec = GameConfig.AuraCostK = 0f;
            GameConfig.ShadesBase = GameConfig.ShadesPerDistrict = GameConfig.ShadesTide = GameConfig.ShadesFinal = 0;
            GameConfig.SpawnIntervalMin = GameConfig.SpawnIntervalMax = GameConfig.SnufferIntervalMin = GameConfig.SnufferIntervalMax = 10000f;
            GameConfig.EaterAtProgress = 2f;
            foreach (Growth wall in Growth.All) wall.Burn(10000f);
            yield return new WaitForSecondsRealtime(0.2f);
            int checkedPositions = 0, violations = 0;
            for (int district = 0; district < CityMap.Hearths.Count; district++)
            {
                Enemy.ClearAll();
                Pt p = CityMap.CenterOf(CityMap.Hearths[district].X, CityMap.Hearths[district].Z);
                Vector3 stand = NearbyFree(new Vector3(p.X, 0.1f, p.Z), 2.5f);
                gm.Player.TeleportTo(stand, 0f);
                gm.Hero.SetLife(100f, LightReason.Debug);
                yield return null;
                for (int kind = 0; kind < 3; kind++)
                {
                    Vector3 spawn = NearbyFree(stand, 7f + kind * 2f);
                    if (Free(spawn)) Enemy.Spawn((EnemyKind)kind, spawn);
                }
                yield return null; yield return null;
                float until = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < until)
                {
                    foreach (Enemy enemy in Enemy.All)
                    {
                        if (enemy == null || enemy.Health <= 0f) continue;
                        checkedPositions++;
                        if (!Free(enemy.transform.position)) violations++;
                    }
                    yield return null;
                }
            }
            Check(checkedPositions > 1000 && violations == 0, "enemy navigation in four districts: positions=" + checkedPositions + ", violations=" + violations);
            Enemy.ClearAll();
            yield return null;
            Pt destination = CityMap.CenterOf(CityMap.BusX, CityMap.BusZ);
            for (int i = 0; i < Survivor.All.Count; i++)
            {
                Survivor survivor = Survivor.All[i];
                Vector3 begin = survivor.transform.position;
                survivor.ReviveAt(begin);
                var path = CityMap.FindPath(new Pt(begin.x, begin.z), destination, false, 0f);
                Check(path != null && path.Count > 1, "survivor " + i + " has an open route after walls burn");
                if (path == null || path.Count < 2) continue;
                Vector3 segment = new Vector3(path[1].X, 0.1f, path[1].Z) - begin;
                segment.y = 0;
                Vector3 goal = begin + segment.normalized * Mathf.Min(12f, segment.magnitude);
                goal = NearbyFree(goal, 1.1f);
                gm.Player.TeleportTo(goal, 0f);
                gm.Hero.SetLife(100f, LightReason.Debug);
                float before = Vector3.Distance(begin, goal);
                yield return new WaitForSecondsRealtime(4.5f);
                float after = Vector3.Distance(survivor.transform.position, gm.Player.transform.position);
                Check(after <= 6f || after < before - 2f, "survivor " + i + " follows its route segment: " + before.ToString("0.0") + " -> " + after.ToString("0.0") + "m");
            }
        }

        AudioSource[] FindMusic()
        {
            System.Collections.Generic.List<AudioSource> sources = new System.Collections.Generic.List<AudioSource>();
            foreach (AudioSource s in gm.GetComponents<AudioSource>())
                if (s.clip != null && s.clip.name.StartsWith("Suspense_")) sources.Add(s);
            return sources.ToArray();
        }
        void Finish()
        {
            if (finished) return;
            finished = true;
            GameInput.Provider = null;
            Running = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            report.AppendLine("failures=" + failures);
            report.AppendLine(failures == 0 ? "PG REGRESSION: OK" : "PG REGRESSION: FAILED");
            File.WriteAllText(Path.Combine(output, "regression-result.txt"), report.ToString());
            Application.Quit(failures == 0 ? 0 : 1);
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            GameInput.Provider = null;
            Running = false;
        }

        sealed class ReplayInput : IGameInput
        {
            readonly bool[] held = new bool[64];
            readonly int[] pressed = new int[64];
            readonly bool[] mouseHeld = new bool[2];
            readonly int[] mousePressed = new int[2];
            Vector2 look;
            int lookFrame;
            public ReplayInput()
            {
                for (int i = 0; i < pressed.Length; i++) pressed[i] = -1;
                for (int i = 0; i < mousePressed.Length; i++) mousePressed[i] = -1;
                lookFrame = -1;
            }
            public void Key(GKey key, bool down)
            {
                if (down && !held[(int)key]) pressed[(int)key] = Time.frameCount + 1;
                held[(int)key] = down;
            }
            public void Mouse(int button, bool down)
            {
                if (down && !mouseHeld[button]) mousePressed[button] = Time.frameCount + 1;
                mouseHeld[button] = down;
            }
            public void LookOnce(Vector2 delta) { look = delta; lookFrame = Time.frameCount + 1; }
            public bool Held(GKey key) { return held[(int)key]; }
            public bool Down(GKey key) { return pressed[(int)key] == Time.frameCount; }
            public bool MouseHeld(int button) { return mouseHeld[button]; }
            public bool MouseDown(int button) { return mousePressed[button] == Time.frameCount; }
            public Vector2 LookDelta() { return lookFrame == Time.frameCount ? look : Vector2.zero; }
            public float Scroll() { return 0f; }
        }
    }
}
