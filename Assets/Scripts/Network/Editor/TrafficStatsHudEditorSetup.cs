using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CUC260905.Network.EditorTools
{
    /// <summary>
    /// 编辑模式自动装配：让"总传输量"HUD 在 EditMode 下也可见。
    /// 非播放模式下把 TrafficStatsHudController 挂到首个非世界空间 Canvas；
    /// 组件自带 [ExecuteAlways]，挂载后立即在左上角创建文本视图（不写场景文件）。
    /// 保存场景后该组件随场景持久化，与 GlobalLoadBarController 的场景接线方式一致。
    /// Play 模式由 TrafficStatsHudBootstrap 兜底挂载，二者以组件存在性判重，不会重复。
    /// </summary>
    [InitializeOnLoad]
    public static class TrafficStatsHudEditorSetup
    {
        static TrafficStatsHudEditorSetup()
        {
            EditorApplication.delayCall += EnsureTrafficStatsHud;
            EditorApplication.hierarchyChanged += EnsureTrafficStatsHud;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            EnsureTrafficStatsHud();
        }

        private static void EnsureTrafficStatsHud()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (canvas.GetComponent<TrafficStatsHudController>() == null)
                {
                    canvas.gameObject.AddComponent<TrafficStatsHudController>();
                }

                return;
            }
        }
    }
}
