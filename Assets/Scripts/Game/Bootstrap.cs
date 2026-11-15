using UnityEngine;

namespace PokaGoryu
{
    /// <summary>
    /// Игра запускается сама при нажатии Play в ЛЮБОЙ сцене (даже пустой): мир строится кодом.
    /// Отдельная сцена не нужна; меню Pokagoryu/Create Main Scene нужно только для сборки (Build Settings).
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (GameManager.Instance != null) return;
            GameObject go = new GameObject("PokaGoryuGame");
            go.AddComponent<GameManager>();
        }
    }
}
