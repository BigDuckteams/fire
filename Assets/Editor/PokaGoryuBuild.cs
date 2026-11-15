#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PokaGoryu.EditorTools
{
    /// <summary>
    /// Сборка Windows-плеера (64 бита, Mono) одной командой.
    /// Из редактора: меню Pokagoryu → Build Windows Player (результат — Builds/Windows/Pokagoryu рядом с Assets).
    /// Из командной строки, без экрана:
    ///   Unity.exe -batchmode -projectPath "папка проекта" -logFile build.log
    ///             -executeMethod PokaGoryu.EditorTools.PokaGoryuBuild.BuildWindowsCommandLine [-pgOut "папка"] [-pgDev]
    /// -pgOut — куда собирать (по умолчанию Builds/Windows/Pokagoryu); -pgDev — Development Build (включает отладочные
    /// клавиши F1–F6 и надпись Development Build). Если сцены Assets/Scenes/Main.unity нет, она создаётся, как в меню
    /// Create Main Scene. Код выхода из командной строки: 0 — сборка удалась, 1 — нет. Итог пишется в лог строкой
    /// «[PokaGoryu] Build Succeeded: ...».
    /// </summary>
    public static class PokaGoryuBuild
    {
        const string ExeName = "Pokagoryu.exe";
        const string ScenePath = "Assets/Scenes/Main.unity";

        static string ProjectRoot
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        static string DefaultOut
        {
            get { return Path.Combine(ProjectRoot, "Builds/Windows/Pokagoryu"); }
        }

        [MenuItem("Pokagoryu/Build Windows Player")]
        public static void BuildWindowsMenu()
        {
            Build(DefaultOut, false);
        }

        public static void BuildWindowsCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string outDir = DefaultOut;
            bool dev = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-pgOut" && i + 1 < args.Length) outDir = args[i + 1];
                else if (args[i] == "-pgDev") dev = true;
            }
            bool ok = false;
            try
            {
                ok = Build(outDir, dev);
            }
            catch (Exception e)
            {
                Debug.LogError("[PokaGoryu] Build crashed: " + e);
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static string[] EnabledScenes()
        {
            List<string> list = new List<string>();
            EditorBuildSettingsScene[] all = EditorBuildSettings.scenes;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].enabled) list.Add(all[i].path);
            }
            return list.ToArray();
        }

        /// <summary>Собирает плеер в outDir. Папку с прошлой сборкой (где лежит Pokagoryu.exe) предварительно очищает. true — успех.</summary>
        public static bool Build(string outDir, bool development)
        {
            outDir = Path.GetFullPath(outDir);
            PlayerSettings.bundleVersion = GameConfig.Version;
            if (!File.Exists(Path.Combine(ProjectRoot, ScenePath)) || EnabledScenes().Length == 0)
            {
                PokaGoryuMenu.CreateMainScene();   // для сборки нужна хотя бы одна сцена (пустая: игру строит Bootstrap)
            }
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[PokaGoryu] Build failed: no enabled scenes in Build Settings.");
                return false;
            }

            // чистим только прошлую нашу сборку: чужие папки не трогаем
            if (File.Exists(Path.Combine(outDir, ExeName))) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            BuildPlayerOptions opts = new BuildPlayerOptions();
            opts.scenes = scenes;
            opts.locationPathName = Path.Combine(outDir, ExeName);
            opts.target = BuildTarget.StandaloneWindows64;
            opts.targetGroup = BuildTargetGroup.Standalone;
            opts.options = development ? BuildOptions.Development : BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;
            Debug.Log("[PokaGoryu] Build " + s.result + (development ? " (Development)" : "") + ": "
                + (s.totalSize / 1048576.0).ToString("F1") + " MB, " + s.totalTime.TotalSeconds.ToString("F0") + " s, errors "
                + s.totalErrors + ", warnings " + s.totalWarnings + " -> " + outDir);
            return s.result == BuildResult.Succeeded;
        }
    }
}
#endif
