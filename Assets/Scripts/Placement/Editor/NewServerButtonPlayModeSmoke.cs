using CUC260905.Economy;
using CUC260905.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CUC260905.Placement.EditorTools
{
    /// <summary>
    /// “新建服务器”按钮经济接入的 PlayMode 冒烟检查：
    /// 打开 SampleScene 进入 PlayMode，验证
    ///   1) 初始余额 = 100，且余额 ≥ 30 时按钮可点；
    ///   2) 余额不足 30 时按钮禁用；
    ///   3) 服务器节点放置成功后余额扣除 30G；
    ///   4) 取消放置不扣费。
    ///
    /// 域重载安全：进入 PlayMode 时的域重载会清空静态订阅，因此用 EditorPrefs 标记
    /// “检查进行中”，由 [InitializeOnLoadMethod] 在每次域重载后重新挂载；检查逻辑由
    /// 场景内 Runner 组件在玩家循环（Update）中执行，不依赖 EditorApplication.update。
    ///
    /// 用法（批处理）：Unity -batchmode -projectPath ... -executeMethod CUC260905.Placement.EditorTools.NewServerButtonPlayModeSmoke.Run
    /// 用法（编辑器内）：菜单 CUC260905/Placement/New Server Economy Smoke。
    /// </summary>
    public static class NewServerButtonPlayModeSmoke
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ServerPrefabPath = "Prefabs/ServerNode";
        private const int ExpectedInitialBalance = 100;
        private const int ServerCost = 30;
        private const string PendingPrefKey = "CUC260905.NewServerButtonSmoke.Pending";

        /// <summary>编辑器菜单入口：批处理与 MCP 共用的同一套检查。</summary>
        [MenuItem("CUC260905/Placement/New Server Economy Smoke")]
        public static void RunFromMenu()
        {
            Run();
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[NewServerButtonSmoke] 已在播放状态，跳过。");
                Exit(1);
                return;
            }

            EditorPrefs.SetBool(PendingPrefKey, true);
            // 若当前场景就是 SampleScene（常见于编辑器内手动触发），不再重开，避免丢失未保存改动。
            if (!string.Equals(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                    ScenePath,
                    System.StringComparison.Ordinal))
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
            if (Object.FindFirstObjectByType<NewServerButtonSmokeRunner>() != null)
            {
                return;
            }

            GameObject runnerObject = new GameObject("NewServerButtonSmokeRunner");
            // 运行时对象：不进入场景存档，PlayMode 结束后自动销毁。
            runnerObject.hideFlags = HideFlags.HideAndDontSave;
            runnerObject.AddComponent<NewServerButtonSmokeRunner>();
        }

        internal static void RunChecks()
        {
            IEconomyModel economyModel = GameArchitecture.Interface.GetModel<IEconomyModel>();
            IEconomySystem economySystem = GameArchitecture.Interface.GetSystem<IEconomySystem>();
            IPlacementSystem placementSystem = GameArchitecture.Interface.GetSystem<IPlacementSystem>();
            GameObject serverPrefab = Resources.Load<GameObject>(ServerPrefabPath);
            if (economyModel == null || economySystem == null || placementSystem == null || serverPrefab == null)
            {
                throw new System.InvalidOperationException("依赖未就绪：经济/放置系统或服务器 prefab 缺失。");
            }

            NewServerButtonController controller = FindController();
            if (controller == null)
            {
                throw new System.InvalidOperationException("场景中未找到 NewServerButtonController。");
            }

            Button button = controller.GetComponent<Button>();
            if (button == null)
            {
                throw new System.InvalidOperationException("按钮物体上缺少 uGUI Button。");
            }

            // 1) 初始余额与按钮可用性。
            Check(economyModel.Balance.Value == ExpectedInitialBalance,
                $"初始余额应为 {ExpectedInitialBalance}，实际 {economyModel.Balance.Value}");
            Check(button.interactable, "初始余额 100 ≥ 30，按钮应可点");

            // 2) 余额不足 30 时按钮禁用；恢复后重新可用。
            int consumed = ExpectedInitialBalance - (ServerCost - 1); // 扣到 29G
            Check(economySystem.Consume(consumed), $"预扣 {consumed}G 到 29G 应成功");
            Check(!button.interactable, "余额 29 < 30，按钮应禁用");
            Check(economySystem.Add(consumed), "恢复余额到 100 应成功");
            Check(button.interactable, "余额恢复 100 ≥ 30，按钮应重新可点");

            // 3) 放置服务器成功后扣费 30G。
            int beforePlace = economyModel.Balance.Value;
            placementSystem.Begin(serverPrefab);
            placementSystem.TryPlace(new Vector3(2f, 0f, 0f));
            Check(economyModel.Balance.Value == beforePlace - ServerCost,
                $"放置服务器后应扣 {ServerCost}G（{beforePlace} → {beforePlace - ServerCost}），实际 {economyModel.Balance.Value}");
            Check(button.interactable, "放置后余额仍 ≥ 30，按钮应可点");

            // 4) 取消放置不扣费。
            int beforeCancel = economyModel.Balance.Value;
            placementSystem.Begin(serverPrefab);
            placementSystem.Cancel();
            Check(economyModel.Balance.Value == beforeCancel,
                "取消放置不应扣费，余额应保持 " + beforeCancel);
        }

        internal static void Check(bool condition, string message)
        {
            Debug.Log("[NewServerButtonSmoke] 检查：" + message + " → " + (condition ? "PASS" : "FAIL"));
            if (!condition)
            {
                throw new System.Exception("[NewServerButtonSmoke] " + message);
            }
        }

        internal static void Success()
        {
            Finish();
            Debug.Log("[NewServerButtonSmoke] PASS：新建服务器按钮经济接入验证通过。");
            Exit(0);
        }

        internal static void Fail(string message)
        {
            Finish();
            Debug.LogError("[NewServerButtonSmoke] FAIL：" + message);
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

        private static NewServerButtonController FindController()
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                NewServerButtonController controller = root.GetComponentInChildren<NewServerButtonController>(true);
                if (controller != null)
                {
                    return controller;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 冒烟检查的玩家循环驱动：随场景进入 PlayMode，在 Update 中推进帧计数并执行检查，
    /// 避免依赖 EditorApplication.update 在批处理播放模式下的行为。
    /// </summary>
    public sealed class NewServerButtonSmokeRunner : MonoBehaviour
    {
        private int mFrame;
        private bool mFinished;

        private void Update()
        {
            if (mFinished)
            {
                return;
            }

            mFrame++;
            // 跳过前几帧，确保场景各 Controller 的 Awake/Start 已完成。
            if (mFrame < 5)
            {
                return;
            }

            mFinished = true;
            try
            {
                NewServerButtonPlayModeSmoke.RunChecks();
                NewServerButtonPlayModeSmoke.Success();
            }
            catch (System.Exception e)
            {
                NewServerButtonPlayModeSmoke.Fail("冒烟检查异常：" + e.Message);
            }
        }
    }
}
