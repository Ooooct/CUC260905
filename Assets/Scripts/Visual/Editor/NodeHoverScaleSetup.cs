using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CUC260905.Visual.EditorTools
{
    /// <summary>
    /// 一键把 NodeHoverScaleFeedback 挂到所有节点根：
    /// 1) Assets/Resources/Prefabs/UserNode.prefab 根
    /// 2) Assets/Resources/Prefabs/ServerNode.prefab 根
    /// 3) SampleScene 中预置的 ServerNode 场景对象
    /// 幂等：已挂载则不重复添加；可作为菜单项或批处理 -executeMethod 入口。
    /// 之后放置/生成的节点都从 prefab 实例化，自动获得悬浮放大反馈。
    /// </summary>
    public static class NodeHoverScaleSetup
    {
        private const string UserNodePrefabPath = "Assets/Resources/Prefabs/UserNode.prefab";
        private const string ServerNodePrefabPath = "Assets/Resources/Prefabs/ServerNode.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("CUC260905/Visual/Apply Node Hover Scale Feedback")]
        public static void ApplyAll()
        {
            int added = 0;
            added += EnsureComponentOnPrefabRoot(UserNodePrefabPath);
            added += EnsureComponentOnPrefabRoot(ServerNodePrefabPath);
            added += EnsureComponentOnSceneServerNode();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"NodeHoverScaleFeedback 装配完成：新增组件 {added} 处（幂等，重复执行不叠加）。");
        }

        /// <summary>批处理入口：Unity -batchmode -quit -executeMethod CUC260905.Visual.EditorTools.NodeHoverScaleSetup.ApplyAll</summary>
        public static void ApplyAllBatch()
        {
            ApplyAll();
        }

        private static int EnsureComponentOnPrefabRoot(string prefabPath)
        {
            if (!File.Exists(prefabPath))
            {
                Debug.LogWarning($"未找到 prefab：{prefabPath}，跳过。");
                return 0;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            int added = 0;
            try
            {
                if (root.GetComponent<NodeHoverScaleFeedback>() == null)
                {
                    root.AddComponent<NodeHoverScaleFeedback>();
                    added = 1;
                }
            }
            finally
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }
            return added;
        }

        private static int EnsureComponentOnSceneServerNode()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning($"未找到场景：{ScenePath}，跳过。");
                return 0;
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject serverNode = FindRootServerNode(scene);
            if (serverNode == null)
            {
                Debug.LogWarning($"场景 {ScenePath} 未找到预置 ServerNode，跳过场景装配。");
                return 0;
            }

            if (serverNode.GetComponent<NodeHoverScaleFeedback>() == null)
            {
                serverNode.AddComponent<NodeHoverScaleFeedback>();
                EditorSceneManager.MarkSceneDirty(scene);
                return 1;
            }

            return 0;
        }

        /// <summary>按根对象名匹配场景预置服务器节点，避免误匹配 UI 上的 ServerNodeInfoPanel 等。</summary>
        private static GameObject FindRootServerNode(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                if (root.name == "ServerNode" && root.GetComponent<Network.NetworkNodeRegistrar>() != null)
                {
                    return root;
                }
            }

            return null;
        }
    }
}
