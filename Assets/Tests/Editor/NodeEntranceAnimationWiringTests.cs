using CUC260905.Visual;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CUC260905.Tests
{
    /// <summary>
    /// 节点入场动画装配契约：UserNode/ServerNode prefab 根必须挂载 NodeEntranceAnimation，
    /// 且默认参数为：起始 scale 系数 0.5、时长 0.5s、缓动 Ease.OutBack。
    /// 装配由 Visual/Editor/NodeEntranceAnimationSetup 完成，此处锁定"装配后"的契约。
    /// </summary>
    public sealed class NodeEntranceAnimationWiringTests
    {
        private const string UserNodePrefabPath = "Assets/Resources/Prefabs/UserNode.prefab";
        private const string ServerNodePrefabPath = "Assets/Resources/Prefabs/ServerNode.prefab";

        [Test]
        public void UserNodePrefab_ShouldCarryEntranceAnimation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UserNodePrefabPath);
            Assert.That(prefab, Is.Not.Null, "应能找到 UserNode prefab。");

            Assert.That(prefab.GetComponent<NodeEntranceAnimation>(), Is.Not.Null,
                "UserNode prefab 根应挂载 NodeEntranceAnimation。");
        }

        [Test]
        public void ServerNodePrefab_ShouldCarryEntranceAnimation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ServerNodePrefabPath);
            Assert.That(prefab, Is.Not.Null, "应能找到 ServerNode prefab。");

            Assert.That(prefab.GetComponent<NodeEntranceAnimation>(), Is.Not.Null,
                "ServerNode prefab 根应挂载 NodeEntranceAnimation。");
        }

        [Test]
        public void UserNodePrefab_ShouldHaveDefaultEntranceParameters()
        {
            AssertEntranceDefaults(UserNodePrefabPath);
        }

        [Test]
        public void ServerNodePrefab_ShouldHaveDefaultEntranceParameters()
        {
            AssertEntranceDefaults(ServerNodePrefabPath);
        }

        private static void AssertEntranceDefaults(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            NodeEntranceAnimation animation = prefab != null ? prefab.GetComponent<NodeEntranceAnimation>() : null;
            Assert.That(animation, Is.Not.Null, $"prefab 应挂载 NodeEntranceAnimation：{prefabPath}");

            SerializedObject so = new SerializedObject(animation);
            Assert.That(so.FindProperty("mStartScaleFactor").floatValue, Is.EqualTo(0.5f).Within(1e-4f),
                "入场起始整体 scale 系数默认应为约 0.5。");
            Assert.That(so.FindProperty("mDuration").floatValue, Is.EqualTo(0.5f).Within(1e-4f),
                "入场动画时长默认应为 0.5s。");
            Assert.That(so.FindProperty("mEase").intValue, Is.EqualTo((int)Ease.OutBack),
                "入场缓动曲线默认应为 Ease.OutBack。");
        }
    }
}
