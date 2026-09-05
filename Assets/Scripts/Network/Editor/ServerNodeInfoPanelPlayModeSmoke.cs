using System;
using System.Globalization;
using System.Reflection;
using CUC260905.Interaction;
using CUC260905.Network;
using QFramework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CUC260905.Network.EditorTools
{
    /// <summary>
    /// 服务器信息面板 PlayMode 冒烟检查（批处理入口）：
    /// 打开 SampleScene 进入 PlayMode，验证
    ///   1) 面板两个吞吐文本（DataShowcur / DataShowmax）均已绑定，初始面板隐藏；
    ///   2) 选中服务器后面板显示，DataShowmax 从 0 向处理上限跳动（采样到中间值）；
    ///   3) 修改近 1 秒负载后，DataShowcur 从当前显示值向新值跳动（采样到中间值）。
    ///
    /// 域重载安全：进入 PlayMode 时的域重载会清空静态订阅，因此用 EditorPrefs 标记
    /// “检查进行中”，由 [InitializeOnLoadMethod] 在每次域重载后重新挂载；检查逻辑由
    /// 场景内 Runner 组件在玩家循环（Update）中执行，不依赖 EditorApplication.update。
    ///
    /// 用法（批处理）：Unity -batchmode -quit -projectPath ... -executeMethod CUC260905.Network.EditorTools.ServerNodeInfoPanelPlayModeSmoke.Run
    /// 用法（编辑器内）：菜单 CUC260905/Network/Server Info Panel Smoke。
    /// </summary>
    public static class ServerNodeInfoPanelPlayModeSmoke
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PendingPrefKey = "CUC260905.ServerInfoPanelSmoke.Pending";

        /// <summary>验证当前值跳动时，在“当前近 1 秒负载”上叠加的固定增量（Mbps）。</summary>
        internal const float TestLoadChange = 30f;

        /// <summary>编辑器菜单入口：批处理与 MCP 共用的同一套检查。</summary>
        [MenuItem("CUC260905/Network/Server Info Panel Smoke")]
        public static void RunFromMenu()
        {
            Run();
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[ServerInfoPanelSmoke] 已在播放状态，跳过。");
                Exit(1);
                return;
            }

            EditorPrefs.SetBool(PendingPrefKey, true);
            // 若当前场景就是 SampleScene（常见于编辑器内手动触发），不再重开，避免丢失未保存改动。
            if (!string.Equals(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                    ScenePath,
                    StringComparison.Ordinal))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            EditorApplication.update += UpdateStep;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>域重载后恢复挂载：编辑器更新路径 + 玩家循环 Runner 双保险。</summary>
        [InitializeOnLoadMethod]
        private static void Reattach()
        {
            if (!EditorPrefs.GetBool(PendingPrefKey, false))
            {
                return;
            }

            EditorApplication.update += UpdateStep;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EnsureRunner();
            }
        }

        private static void UpdateStep()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            EnsureRunner();
        }

        private static void EnsureRunner()
        {
            if (UnityEngine.Object.FindFirstObjectByType<ServerInfoPanelSmokeRunner>() != null)
            {
                return;
            }

            GameObject runnerObject = new GameObject("ServerInfoPanelSmokeRunner");
            // 运行时对象：不进入场景存档，PlayMode 结束后自动销毁。
            runnerObject.hideFlags = HideFlags.HideAndDontSave;
            runnerObject.AddComponent<ServerInfoPanelSmokeRunner>();
        }

        internal static void Check(bool condition, string message)
        {
            Debug.Log("[ServerInfoPanelSmoke] 检查：" + message + " → " + (condition ? "PASS" : "FAIL"));
            if (!condition)
            {
                throw new Exception("[ServerInfoPanelSmoke] " + message);
            }
        }

        internal static void Success()
        {
            Finish();
            Debug.Log("[ServerInfoPanelSmoke] PASS：服务器信息面板两端吞吐显示与数字跳动验证通过。");
            Exit(0);
        }

        internal static void Fail(string message)
        {
            Finish();
            Debug.LogError("[ServerInfoPanelSmoke] FAIL：" + message);
            Exit(1);
        }

        private static void Finish()
        {
            EditorApplication.update -= UpdateStep;
            EditorPrefs.SetBool(PendingPrefKey, false);
            EditorApplication.isPlaying = false;
        }

        /// <summary>批处理下退出进程；交互式编辑器内只记录结果，绝不关闭用户编辑器。</summary>
        private static void Exit(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }

    /// <summary>
    /// 冒烟检查的玩家循环驱动：随场景进入 PlayMode，在 Update 中推进阶段并执行检查。
    /// 检查窗口在部署接入完成（10s）之前结束，避免随机流量扰动当前负载。
    /// </summary>
    public sealed class ServerInfoPanelSmokeRunner : MonoBehaviour
    {
        private enum Stage
        {
            Settle,
            MaxJump,
            LoadJump,
        }

        private int mFrame;
        private Stage mStage = Stage.Settle;
        private double mWaitStart;
        private ServerNodeInfoPanelController mPanel;
        private TMP_Text mCurText;
        private TMP_Text mMaxText;
        private ServerNodeCapabilities mCapabilities;
        private float mCapacity;
        private float mLoadTarget;
        private float mLoadDisplayBeforeChange;
        private bool mMaxMidSampled;
        private bool mLoadMidSampled;

        private void Update()
        {
            mFrame++;

            switch (mStage)
            {
                case Stage.Settle:
                    // 跳过前几帧，确保场景各 Controller 的 Awake/Start 已完成。
                    if (mFrame < 5)
                    {
                        return;
                    }

                    SelectServer();
                    mStage = Stage.MaxJump;
                    mWaitStart = Time.unscaledTime;
                    break;

                case Stage.MaxJump:
                    TickMaxJump();
                    break;

                case Stage.LoadJump:
                    TickLoadJump();
                    break;
            }
        }

        /// <summary>准备阶段：校验面板绑定与初始隐藏态，选中一台服务器并发布点击事件。</summary>
        private void SelectServer()
        {
            mPanel = FindPanel();
            ServerNodeInfoPanelPlayModeSmoke.Check(
                mPanel != null,
                "场景中存在 ServerNodeInfoPanelController");
            ServerNodeInfoPanelPlayModeSmoke.Check(
                !mPanel.gameObject.activeSelf,
                "面板初始应隐藏");

            mCurText = ReadText(mPanel, "mDataShowcurText");
            mMaxText = ReadText(mPanel, "mDataShowmaxText");
            ServerNodeInfoPanelPlayModeSmoke.Check(mCurText != null, "DataShowcur 文本已绑定");
            ServerNodeInfoPanelPlayModeSmoke.Check(mMaxText != null, "DataShowmax 文本已绑定");

            INetworkTopologyModel model = GameArchitecture.Interface.GetModel<INetworkTopologyModel>();
            ServerNodeInfoPanelPlayModeSmoke.Check(
                TryGetServer(model, out NodeDescriptor node, out mCapabilities),
                "场景中存在已登记的服务器节点能力档案");
            mCapacity = mCapabilities.DataProcessingPerSecond.Value;
            mLoadTarget = mCapabilities.CurrentDataLoadPerSecond.Value +
                          ServerNodeInfoPanelPlayModeSmoke.TestLoadChange;

            GameArchitecture.Interface.SendEvent(new ServerNodeClickedEvent(node, mCapabilities));
            ServerNodeInfoPanelPlayModeSmoke.Check(
                mPanel.gameObject.activeSelf,
                "选中服务器后面板应显示");
        }

        /// <summary>校验 DataShowmax 从 0 向处理上限跳动：先采样中间值，再校验最终到位。</summary>
        private void TickMaxJump()
        {
            double elapsed = Time.unscaledTime - mWaitStart;
            if (!mMaxMidSampled && elapsed >= 0.15)
            {
                mMaxMidSampled = true;
                float mid = ParseMbps(mMaxText.text);
                ServerNodeInfoPanelPlayModeSmoke.Check(
                    mid > 0f && mid < mCapacity,
                    "DataShowmax 跳动中间值应介于 0 与 " + mCapacity + "（实际 " + mid + "）");
                return;
            }

            if (elapsed >= 1.0)
            {
                float final = ParseMbps(mMaxText.text);
                ServerNodeInfoPanelPlayModeSmoke.Check(
                    Mathf.Abs(final - mCapacity) <= 0.05f,
                    "DataShowmax 应跳到处理上限 " + mCapacity + "（实际 " + final + "）");

                // 记录负载变化前的当前显示值，再改写负载触发当前值跳动。
                mLoadDisplayBeforeChange = ParseMbps(mCurText.text);
                mCapabilities.CurrentDataLoadPerSecond.Value = mLoadTarget;
                mWaitStart = Time.unscaledTime;
                mStage = Stage.LoadJump;
            }
        }

        /// <summary>校验 DataShowcur 从当前显示值向新负载跳动：先采样中间值，再校验最终到位。</summary>
        private void TickLoadJump()
        {
            double elapsed = Time.unscaledTime - mWaitStart;
            if (!mLoadMidSampled && elapsed >= 0.15)
            {
                mLoadMidSampled = true;
                float mid = ParseMbps(mCurText.text);
                ServerNodeInfoPanelPlayModeSmoke.Check(
                    mid > mLoadDisplayBeforeChange && mid < mLoadTarget,
                    "DataShowcur 跳动中间值应介于 " + mLoadDisplayBeforeChange + " 与 " + mLoadTarget +
                    "（实际 " + mid + "）");
                return;
            }

            if (elapsed >= 0.8)
            {
                float final = ParseMbps(mCurText.text);
                ServerNodeInfoPanelPlayModeSmoke.Check(
                    Mathf.Abs(final - mLoadTarget) <= 0.05f,
                    "DataShowcur 应跳到目标负载 " + mLoadTarget + "（实际 " + final + "）");
                ServerNodeInfoPanelPlayModeSmoke.Success();
            }
        }

        /// <summary>在场景根中查找面板（含未激活对象）。</summary>
        private static ServerNodeInfoPanelController FindPanel()
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                ServerNodeInfoPanelController controller = root.GetComponentInChildren<ServerNodeInfoPanelController>(true);
                if (controller != null)
                {
                    return controller;
                }
            }

            return null;
        }

        /// <summary>读取面板上的私有序列化文本字段，用于校验场景绑定。</summary>
        private static TMP_Text ReadText(ServerNodeInfoPanelController controller, string fieldName)
        {
            FieldInfo field = typeof(ServerNodeInfoPanelController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new Exception("面板缺少序列化字段 " + fieldName);
            }

            return field.GetValue(controller) as TMP_Text;
        }

        /// <summary>解析“xx Mbps / /xx Mbps”中的数值。</summary>
        private static float ParseMbps(string text)
        {
            string cleaned = text.Replace("/", string.Empty).Replace("Mbps", string.Empty).Trim();
            if (float.TryParse(
                    cleaned,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value))
            {
                return value;
            }

            throw new Exception("无法解析吞吐文本：" + text);
        }

        /// <summary>从模型中选取一台带有限处理上限的服务器，避免无限上限分支干扰断言。</summary>
        private static bool TryGetServer(
            INetworkTopologyModel model,
            out NodeDescriptor node,
            out ServerNodeCapabilities capabilities)
        {
            node = default;
            capabilities = null;
            foreach (NodeDescriptor candidate in model.Nodes)
            {
                if (candidate.Role != NetworkNodeRole.Server)
                {
                    continue;
                }

                if (model.TryGetServerCapabilities(candidate.NodeId, out ServerNodeCapabilities candidateCaps) &&
                    candidateCaps != null &&
                    candidateCaps.DataProcessingPerSecond.Value > 0f)
                {
                    node = candidate;
                    capabilities = candidateCaps;
                    return true;
                }
            }

            return false;
        }
    }
}
