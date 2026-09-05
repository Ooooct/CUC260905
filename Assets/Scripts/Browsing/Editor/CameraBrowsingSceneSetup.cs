using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CUC260905.Browsing.EditorTools
{
    /// <summary>
    /// 一键把浏览系统接线进当前场景：
    /// 1) 确保 Main Camera（或任意 Camera）上挂 CameraBrowsingController 并赋好 Camera；
    /// 2) 按需求写入浏览参数：相机移动边界 (-100,-100)~(100,100)，缩放 0.25x~2x。
    /// </summary>
    public static class CameraBrowsingSceneSetup
    {
        [MenuItem("CUC260905/Browsing/Setup Demo Scene")]
        public static void SetupDemoScene()
        {
            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                Debug.LogError("浏览系统接线失败：场景没有 Camera。");
                return;
            }

            CameraBrowsingController controller = EnsureBrowsingController(camera);
            ApplyBrowsingSettings(controller);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(
                "浏览系统接线完成：\n" +
                "· CameraBrowsingController 已挂到 " + camera.name + "，mCamera 已赋值\n" +
                "· 相机移动边界 (-100,-100)~(100,100)，缩放 0.25x~2x\n" +
                "运行场景即可：左键拖拽空白区域平移（限制在 -100..100），滚轮以光标为锚缩放（0.25x~2x）。");
        }

        private static CameraBrowsingController EnsureBrowsingController(Camera camera)
        {
            CameraBrowsingController controller = camera.GetComponent<CameraBrowsingController>();
            if (controller == null)
            {
                controller = camera.gameObject.AddComponent<CameraBrowsingController>();
                Undo.RegisterCreatedObjectUndo(controller, "Create CameraBrowsingController");
            }

            return controller;
        }

        /// <summary>幂等写入需求参数，保证已存在的旧实例也能升级到新配置。</summary>
        private static void ApplyBrowsingSettings(CameraBrowsingController controller)
        {
            SerializedObject so = new SerializedObject(controller);

            SetObjectReference(so, "mCamera", controller.GetComponent<Camera>());
            SetBool(so, "mClampToBounds", true);
            SetVector4(so, "mBounds", new Vector4(-100.0f, 100.0f, -100.0f, 100.0f));
            SetFloat(so, "mMinZoomFactor", 0.25f);
            SetFloat(so, "mMaxZoomFactor", 2.0f);

            so.ApplyModifiedProperties();
        }

        private static void SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.boolValue = value;
            }
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.floatValue = value;
            }
        }

        private static void SetVector4(SerializedObject so, string field, Vector4 value)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.vector4Value = value;
            }
        }

        private static void SetObjectReference(SerializedObject so, string field, Object value)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
        }
    }
}
