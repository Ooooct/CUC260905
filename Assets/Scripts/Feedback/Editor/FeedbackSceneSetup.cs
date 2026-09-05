using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CUC260905.Feedback.EditorTools
{
    /// <summary>
    /// 一键把反馈系统接进当前场景：
    /// 确保 Managers 下存在一个挂 <see cref="FeedbackPresenter"/> 的 Feedback 对象。
    /// </summary>
    public static class FeedbackSceneSetup
    {
        [MenuItem("CUC260905/Feedback/Setup Scene")]
        public static void SetupScene()
        {
            FeedbackPresenter existing = Object.FindObjectOfType<FeedbackPresenter>();
            if (existing != null)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("反馈系统已装配（FeedbackPresenter 已存在）。");
                return;
            }

            GameObject managers = GameObject.Find("Managers");
            if (managers == null)
            {
                managers = new GameObject("Managers");
                Undo.RegisterCreatedObjectUndo(managers, "Create Managers");
            }

            GameObject feedback = new GameObject("Feedback");
            Undo.RegisterCreatedObjectUndo(feedback, "Create Feedback");
            feedback.transform.SetParent(managers.transform, false);
            feedback.AddComponent<FeedbackPresenter>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("反馈系统装配完成：Managers/Feedback 已挂 FeedbackPresenter，" +
                      "运行后调用 IFeedbackSystem.ShowCircle 即可显示背景圆。");
        }
    }
}
