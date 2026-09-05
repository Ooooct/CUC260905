using CUC260905.Visual;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CUC260905.Tests
{
    /// <summary>
    /// 节点悬浮放大反馈装配契约：UserNode/ServerNode prefab 根必须挂载
    /// NodeHoverScaleFeedback，且默认参数为：悬浮 scale 系数 1.2（放大 20%）、
    /// 时长 0.2s、缓动 Ease.OutBack。
    /// 装配由 Visual/Editor/NodeHoverScaleSetup 完成，此处锁定"装配后"的契约。
    /// </summary>
    public sealed class NodeHoverScaleWiringTests
    {
        private const string UserNodePrefabPath = "Assets/Resources/Prefabs/UserNode.prefab";
        private const string ServerNodePrefabPath = "Assets/Resources/Prefabs/ServerNode.prefab";

        [Test]
        public void UserNodePrefab_ShouldCarryHoverScaleFeedback()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UserNodePrefabPath);
            Assert.That(prefab, Is.Not.Null, "应能找到 UserNode prefab。");

            Assert.That(prefab.GetComponent<NodeHoverScaleFeedback>(), Is.Not.Null,
                "UserNode prefab 根应挂载 NodeHoverScaleFeedback。");
        }

        [Test]
        public void ServerNodePrefab_ShouldCarryHoverScaleFeedback()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ServerNodePrefabPath);
            Assert.That(prefab, Is.Not.Null, "应能找到 ServerNode prefab。");

            Assert.That(prefab.GetComponent<NodeHoverScaleFeedback>(), Is.Not.Null,
                "ServerNode prefab 根应挂载 NodeHoverScaleFeedback。");
        }

        [Test]
        public void UserNodePrefab_ShouldHaveDefaultHoverScaleParameters()
        {
            AssertHoverScaleDefaults(UserNodePrefabPath);
        }

        [Test]
        public void ServerNodePrefab_ShouldHaveDefaultHoverScaleParameters()
        {
            AssertHoverScaleDefaults(ServerNodePrefabPath);
        }

        private static void AssertHoverScaleDefaults(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            NodeHoverScaleFeedback feedback =
                prefab != null ? prefab.GetComponent<NodeHoverScaleFeedback>() : null;
            Assert.That(feedback, Is.Not.Null, $"prefab 应挂载 NodeHoverScaleFeedback：{prefabPath}");

            SerializedObject so = new SerializedObject(feedback);
            Assert.That(so.FindProperty("mHoverScaleFactor").floatValue, Is.EqualTo(1.2f).Within(1e-4f),
                "悬浮整体 scale 系数默认应为 1.2（放大 20%）。");
            Assert.That(so.FindProperty("mDuration").floatValue, Is.EqualTo(0.2f).Within(1e-4f),
                "悬浮/恢复动画时长默认应为 0.2s。");
            Assert.That(so.FindProperty("mEase").intValue, Is.EqualTo((int)Ease.OutBack),
                "悬浮缓动曲线默认应为 Ease.OutBack。");
        }
    }
}
