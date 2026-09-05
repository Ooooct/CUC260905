using UnityEngine;
using UnityEngine.SceneManagement;

namespace CUC260905.Network
{
    /// <summary>确保场景已有屏幕 Canvas 时自动装配总体负载 HUD。</summary>
    public static class GlobalLoadHudBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneLoadHandler()
        {
            SceneManager.sceneLoaded -= EnsureGlobalLoadHud;
            SceneManager.sceneLoaded += EnsureGlobalLoadHud;
        }

        private static void EnsureGlobalLoadHud(Scene scene, LoadSceneMode _)
        {
            if (!string.Equals(scene.name, "SampleScene", System.StringComparison.Ordinal))
            {
                return;
            }

            Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (canvas.GetComponent<GlobalLoadBarController>() == null)
                {
                    canvas.gameObject.AddComponent<GlobalLoadBarController>();
                }

                return;
            }

            Debug.LogError("总体负载 HUD 初始化失败：场景中未找到屏幕空间 Canvas。");
        }
    }
}
