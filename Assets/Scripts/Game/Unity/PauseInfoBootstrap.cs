using UnityEngine;
using UnityEngine.SceneManagement;

namespace CUC260905.Game
{
    /// <summary>
    /// 运行时把 PauseInfoController 自动挂到场景已有的 PauseInfo 对象上，
    /// 无需手工编辑场景文件（与 GamePauseBootstrap 同一机制）。
    /// </summary>
    public static class PauseInfoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneLoadHandler()
        {
            SceneManager.sceneLoaded -= EnsurePauseInfoController;
            SceneManager.sceneLoaded += EnsurePauseInfoController;
        }

        private static void EnsurePauseInfoController(Scene scene, LoadSceneMode _)
        {
            if (!string.Equals(scene.name, "SampleScene", System.StringComparison.Ordinal))
            {
                return;
            }

            GameObject pauseInfo = GameObject.Find("PauseInfo");
            if (pauseInfo == null)
            {
                Debug.LogError("PauseInfoController 初始化失败：场景中未找到名为 PauseInfo 的对象。");
                return;
            }

            if (pauseInfo.GetComponent<PauseInfoController>() == null)
            {
                pauseInfo.AddComponent<PauseInfoController>();
            }
        }
    }
}
