using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CUC260905.Placement.EditorTools
{
    /// <summary>
    /// 一键把放置系统接线进当前场景：
    /// 1) 确保 PlacementView（挂 PlacementPreviewView）；
    /// 2) 确保 Canvas 下有一个 Button 并挂 PlacementButton、onClick 绑到 OnButtonClick；
    /// 3) 生成一个白色方块演示 prefab 并赋给按钮。
    /// 运行后请按需调整 InputController 的 mPlacementZ 与按钮的 mPrefab。
    /// </summary>
    public static class PlacementSceneSetup
    {
        private const string DemoDir = "Assets/Placement/Demo";
        private const string DemoPrefabPath = DemoDir + "/PlacementDemo.prefab";

        [MenuItem("CUC260905/Placement/Setup Demo Scene")]
        public static void SetupDemoScene()
        {
            GameObject placementView = EnsurePlacementView();
            Button button = EnsureButton();
            if (button == null)
            {
                Debug.LogError("放置系统接线失败：场景需要 Canvas 与 EventSystem（至少一个 Button）。");
                return;
            }

            PlacementButton placementButton = EnsurePlacementButton(button);
            GameObject prefab = EnsureDemoPrefab();
            AssignPrefab(placementButton, prefab);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("放置系统接线完成：PlacementView 预览 / Button→PlacementButton / 演示 prefab 已生成。" +
                      "请确认 InputController.mPlacementZ，并把按钮 mPrefab 换成正式 prefab。");
        }

        private static GameObject EnsurePlacementView()
        {
            PlacementPreviewView view = Object.FindObjectOfType<PlacementPreviewView>();
            if (view != null)
            {
                return view.gameObject;
            }

            GameObject go = new GameObject("PlacementView");
            Undo.RegisterCreatedObjectUndo(go, "Create PlacementView");
            go.AddComponent<PlacementPreviewView>();
            return go;
        }

        private static Button EnsureButton()
        {
            Button button = Object.FindObjectOfType<Button>();
            if (button != null)
            {
                return button;
            }

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            GameObject go = new GameObject("PlacementButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(go, "Create PlacementButton");
            go.transform.SetParent(canvas.transform, false);
            return go.GetComponent<Button>();
        }

        private static PlacementButton EnsurePlacementButton(Button button)
        {
            PlacementButton placementButton = button.GetComponent<PlacementButton>();
            if (placementButton == null)
            {
                placementButton = button.gameObject.AddComponent<PlacementButton>();
            }

            // 幂等接线：先移除旧监听再添加，避免重复注册。
            button.onClick.RemoveListener(placementButton.OnButtonClick);
            button.onClick.AddListener(placementButton.OnButtonClick);
            return placementButton;
        }

        private static GameObject EnsureDemoPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DemoPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Placement"))
            {
                AssetDatabase.CreateFolder("Assets", "Placement");
            }

            if (!AssetDatabase.IsValidFolder(DemoDir))
            {
                AssetDatabase.CreateFolder("Assets/Placement", "Demo");
            }

            GameObject go = new GameObject("PlacementDemo");
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            Sprite sprite = FindSprite();
            if (sprite != null)
            {
                renderer.sprite = sprite;
            }

            renderer.color = new Color(1.0f, 0.6f, 0.2f, 1.0f);
            PrefabUtility.SaveAsPrefabAsset(go, DemoPrefabPath);
            Object.DestroyImmediate(go);

            return AssetDatabase.LoadAssetAtPath<GameObject>(DemoPrefabPath);
        }

        private static Sprite FindSprite()
        {
            // 优先使用 Unity 内置白色方块；找不到再回退到工程内任意 Sprite。
            Sprite builtin = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (builtin != null)
            {
                return builtin;
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        private static void AssignPrefab(PlacementButton placementButton, GameObject prefab)
        {
            if (placementButton == null || prefab == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(placementButton);
            SerializedProperty prop = so.FindProperty("mPrefab");
            if (prop != null)
            {
                prop.objectReferenceValue = prefab;
                so.ApplyModifiedProperties();
            }
        }
    }
}
