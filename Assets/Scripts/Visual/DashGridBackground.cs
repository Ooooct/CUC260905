using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CUC260905.Visual
{
    /// <summary>
    /// 在相机可见范围内生成短虚线网格背景；网格线固定对齐世界坐标，每条相邻线间距为 0.5 Unity 单位。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DashGridBackground : MonoBehaviour
    {
        private const string SpriteShaderName = "Sprites/Default";

        [Header("网格")]
        [Tooltip("相邻水平/垂直网格线的世界间距。")]
        [SerializeField, Min(0.01f)] private float mLineSpacing = 0.5f;
        [Tooltip("每段虚线的长度。")]
        [SerializeField, Min(0.01f)] private float mDashLength = 0.18f;
        [Tooltip("同一网格线相邻虚线之间的空隙。")]
        [SerializeField, Min(0.0f)] private float mDashGap = 0.32f;
        [Tooltip("背景覆盖宽度；会自动向上对齐到网格间距。")]
        [SerializeField, Min(1.0f)] private float mCoverageWidth = 42.0f;
        [Tooltip("背景覆盖高度；会自动向上对齐到网格间距。")]
        [SerializeField, Min(1.0f)] private float mCoverageHeight = 28.0f;
        [SerializeField] private float mDepth = 5.0f;

        [Header("样式")]
        [SerializeField] private Color mLineColor = new Color(0.22f, 0.71f, 0.80f, 0.45f);
        [SerializeField] private int mSortingOrder = -100;
        [SerializeField] private Camera mCamera;

        private MeshFilter mMeshFilter;
        private MeshRenderer mMeshRenderer;
        private Mesh mMesh;
        private Material mMaterial;

        private void Awake()
        {
            EnsureResources();
            Rebuild();
        }

        private void OnEnable()
        {
            EnsureResources();
            Rebuild();
            UpdatePosition();
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureResources();
            Rebuild();
            UpdatePosition();
        }

        private void OnDestroy()
        {
            if (mMaterial != null)
            {
                DestroyImmediate(mMaterial);
            }

            if (mMesh != null)
            {
                DestroyImmediate(mMesh);
            }
        }

        private void EnsureResources()
        {
            if (mMeshFilter == null)
            {
                mMeshFilter = GetComponent<MeshFilter>();
            }

            if (mMeshRenderer == null)
            {
                mMeshRenderer = GetComponent<MeshRenderer>();
            }

            if (mMesh == null)
            {
                mMesh = new Mesh
                {
                    name = "Dash Grid Background Mesh",
                    indexFormat = IndexFormat.UInt32,
                    hideFlags = HideFlags.DontSave
                };
                mMeshFilter.sharedMesh = mMesh;
            }

            if (mMaterial == null)
            {
                Shader shader = Shader.Find(SpriteShaderName);
                if (shader == null)
                {
                    Debug.LogError("DashGridBackground 未找到 Sprites/Default Shader。", this);
                    enabled = false;
                    return;
                }

                mMaterial = new Material(shader)
                {
                    name = "Dash Grid Background Material",
                    hideFlags = HideFlags.DontSave
                };
                mMeshRenderer.sharedMaterial = mMaterial;
            }

            mMaterial.color = mLineColor;
            mMeshRenderer.sortingOrder = mSortingOrder;
        }

        private void Rebuild()
        {
            if (mMesh == null)
            {
                return;
            }

            float spacing = Mathf.Max(0.01f, mLineSpacing);
            float dashLength = Mathf.Min(Mathf.Max(0.01f, mDashLength), spacing);
            float dashPeriod = dashLength + Mathf.Max(0.0f, mDashGap);
            int halfWidthSteps = Mathf.CeilToInt(mCoverageWidth / (2.0f * spacing));
            int halfHeightSteps = Mathf.CeilToInt(mCoverageHeight / (2.0f * spacing));
            float halfWidth = halfWidthSteps * spacing;
            float halfHeight = halfHeightSteps * spacing;
            int horizontalLineCount = halfHeightSteps * 2 + 1;
            int verticalLineCount = halfWidthSteps * 2 + 1;
            int dashesPerHorizontalLine = Mathf.CeilToInt(halfWidth * 2.0f / dashPeriod);
            int dashesPerVerticalLine = Mathf.CeilToInt(halfHeight * 2.0f / dashPeriod);
            int dashCount = horizontalLineCount * dashesPerHorizontalLine +
                            verticalLineCount * dashesPerVerticalLine;

            List<Vector3> vertices = new List<Vector3>(dashCount * 4);
            List<int> triangles = new List<int>(dashCount * 6);

            for (int yIndex = -halfHeightSteps; yIndex <= halfHeightSteps; yIndex++)
            {
                float y = yIndex * spacing;
                for (int dashIndex = 0; dashIndex < dashesPerHorizontalLine; dashIndex++)
                {
                    float x = -halfWidth + dashIndex * dashPeriod;
                    float right = Mathf.Min(x + dashLength, halfWidth);
                    AddQuad(vertices, triangles, x, y - spacing * 0.035f, right, y + spacing * 0.035f);
                }
            }

            for (int xIndex = -halfWidthSteps; xIndex <= halfWidthSteps; xIndex++)
            {
                float x = xIndex * spacing;
                for (int dashIndex = 0; dashIndex < dashesPerVerticalLine; dashIndex++)
                {
                    float y = -halfHeight + (dashIndex + 0.5f) * dashPeriod;
                    float top = Mathf.Min(y + dashLength, halfHeight);
                    AddQuad(vertices, triangles, x - spacing * 0.035f, y, x + spacing * 0.035f, top);
                }
            }

            mMesh.Clear();
            mMesh.SetVertices(vertices);
            mMesh.SetTriangles(triangles, 0);
            mMesh.RecalculateBounds();
        }

        private void UpdatePosition()
        {
            if (mCamera == null)
            {
                mCamera = Camera.main;
            }

            if (mCamera == null)
            {
                return;
            }

            float spacing = Mathf.Max(0.01f, mLineSpacing);
            Vector3 cameraPosition = mCamera.transform.position;
            float alignedX = Mathf.Round(cameraPosition.x / spacing) * spacing;
            float alignedY = Mathf.Round(cameraPosition.y / spacing) * spacing;
            transform.position = new Vector3(alignedX, alignedY, mDepth);
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<int> triangles,
            float left,
            float bottom,
            float right,
            float top)
        {
            int startIndex = vertices.Count;
            vertices.Add(new Vector3(left, bottom, 0.0f));
            vertices.Add(new Vector3(right, bottom, 0.0f));
            vertices.Add(new Vector3(right, top, 0.0f));
            vertices.Add(new Vector3(left, top, 0.0f));

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }
    }
}
