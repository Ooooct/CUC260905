using UnityEngine;
using UnityEngine.SceneManagement;

namespace CUC260905.Game
{
    /// <summary>确保场景运行时自动挂载 GamePauseController，无需手工编辑场景。</summary>
    public static class GamePauseBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneLoadHandler()
        {
            SceneManager.sceneLoaded -= EnsureGamePauseController;
            SceneManager.sceneLoaded += EnsureGamePauseController;
        }

        private static void EnsureGamePauseController(Scene scene, LoadSceneMode _)
        {
            if (!string.Equals(scene.name, "SampleScene", System.StringComparison.Ordinal))
            {
                return;
            }

            if (Object.FindObjectOfType<GamePauseController>() != null)
            {
                return;
            }

            new GameObject("GamePauseController").AddComponent<GamePauseController>();
        }
    }
}
