using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CUC260905.Network.EditorTools
{
    /// <summary>
    /// 一键把连线系统接线进当前场景：确保存在挂 NetworkConnectionController 的对象。
    /// 节点上的拖拽能力由控制器在节点登记时自动注入，无需手工编辑 prefab。
    /// 连线/预览的粗细、颜色、材质可在该对象的 Inspector 中配置。
    /// </summary>
    public static class NetworkConnectionSceneSetup
    {
        [MenuItem("CUC260905/Network/Setup Connection Scene")]
        public static void SetupDemoScene()
        {
            NetworkConnectionController controller = EnsureController();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(
                "连线系统接线完成：NetworkConnectionController 已就位（" + controller.name + "）。\n" +
                "运行场景即可：左键从任一节点拖出预览线，松手落在另一节点上完成连线。\n" +
                "规则：用户↔用户、自连、重复连线、与既有连线交叉均会被拒绝；服务器↔服务器允许。\n" +
                "连线与预览的粗细、颜色、材质可在 Inspector 配置。");
        }

        private static NetworkConnectionController EnsureController()
        {
            NetworkConnectionController controller = Object.FindObjectOfType<NetworkConnectionController>();
            if (controller != null)
            {
                return controller;
            }

            GameObject go = new GameObject("Network Connections");
            Undo.RegisterCreatedObjectUndo(go, "Create Network Connections");
            return go.AddComponent<NetworkConnectionController>();
        }
    }
}
