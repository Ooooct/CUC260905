using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>确保场景已有屏幕 Canvas 时自动装配左上角"总传输量" HUD。</summary>
    public static class TrafficStatsHudBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureTrafficStatsHud()
        {
            Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (canvas.GetComponent<TrafficStatsHudController>() == null)
                {
                    canvas.gameObject.AddComponent<TrafficStatsHudController>();
                }

                return;
            }

            Debug.LogError("总传输量 HUD 初始化失败：场景中未找到屏幕空间 Canvas。");
        }
    }
}
