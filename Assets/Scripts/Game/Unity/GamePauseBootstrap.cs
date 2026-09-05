using UnityEngine;

namespace CUC260905.Game
{
    /// <summary>确保场景运行时自动挂载 GamePauseController，无需手工编辑场景。</summary>
    public static class GamePauseBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGamePauseController()
        {
            if (Object.FindObjectOfType<GamePauseController>() != null)
            {
                return;
            }

            new GameObject("GamePauseController").AddComponent<GamePauseController>();
        }
    }
}
