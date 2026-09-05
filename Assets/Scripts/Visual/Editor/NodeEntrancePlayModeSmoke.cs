using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CUC260905.Visual.EditorTools
{
    /// <summary>
    /// 入场动画 PlayMode 冒烟检查（批处理入口）：
    /// 打开 SampleScene 进入 PlayMode，采样场景预置 ServerNode 根 scale 约 0.8s，
    /// 断言起始约 0.5（≤0.6）、结束回到 1.0（±0.05），验证"放置/出现时 scale 0.5→1.0"行为。
    /// 用法：Unity -batchmode -quit -projectPath ... -executeMethod CUC260905.Visual.EditorTools.NodeEntrancePlayModeSmoke.Run
    /// </summary>
    public static class NodeEntrancePlayModeSmoke
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const float SampleDuration = 0.8f;
        private const float MinScaleThreshold = 0.6f;
        private const float FinalScaleTolerance = 0.05f;

        private static bool mSamplingStarted;
        private static double mStartTime;
        private static Transform mNodeRoot;
        private static float mMinScale;

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[NodeEntranceSmoke] 已在播放状态，跳过。");
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.update += UpdateStep;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void UpdateStep()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (!mSamplingStarted)
            {
                mSamplingStarted = true;
                mStartTime = EditorApplication.timeSinceStartup;
                mNodeRoot = FindSceneServerNodeRoot();
                if (mNodeRoot == null)
                {
                    Fail("场景中未找到预置 ServerNode 根。");
                    return;
                }

                mMinScale = float.MaxValue;
                return;
            }

            float scale = mNodeRoot.localScale.x;
            mMinScale = Mathf.Min(mMinScale, scale);

            if (EditorApplication.timeSinceStartup - mStartTime < SampleDuration)
            {
                return;
            }

            float finalScale = mNodeRoot.localScale.x;
            bool passed = mMinScale <= MinScaleThreshold &&
                          Mathf.Abs(finalScale - 1.0f) <= FinalScaleTolerance;
            Debug.Log($"[NodeEntranceSmoke] minScale={mMinScale:F3} finalScale={finalScale:F3} " +
                      $"{(passed ? "PASS" : "FAIL")}（期望起始≤{MinScaleThreshold}，结束≈1.0）");

            if (passed)
            {
                Success();
            }
            else
            {
                Fail("入场动画冒烟检查未通过。");
            }
        }

        private static Transform FindSceneServerNodeRoot()
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                if (root.name == "ServerNode" && root.GetComponent<Network.NetworkNodeRegistrar>() != null)
                {
                    return root.transform;
                }
            }

            return null;
        }

        private static void Success()
        {
            EditorApplication.update -= UpdateStep;
            EditorApplication.isPlaying = false;
            Debug.Log("[NodeEntranceSmoke] PASS：入场动画正常播放。");
            EditorApplication.Exit(0);
        }

        private static void Fail(string message)
        {
            EditorApplication.update -= UpdateStep;
            EditorApplication.isPlaying = false;
            Debug.LogError("[NodeEntranceSmoke] FAIL：" + message);
            EditorApplication.Exit(1);
        }
    }
}
