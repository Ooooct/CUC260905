using UnityEngine;

namespace CUC260905.Browsing
{
    /// <summary>
    /// 摄像机浏览系统的纯数据参数（不引用 DOTween，保证运行时逻辑可脱离表现层做单元测试）。
    ///
    /// Zoom 语义与 Unity Camera 一致：正交相机取 orthographicSize，透视相机取 fieldOfView，
    /// 数值越小表示镜头越放大（越靠近场景）。
    /// </summary>
    public readonly struct CameraBrowsingConfig
    {
        /// <summary>缩放允许范围：[min, max]。正交相机即 orthographicSize 的范围。</summary>
        public readonly Vector2 ZoomRange;

        /// <summary>每个滚轮刻度对缩放值的倍率；大于 1。</summary>
        public readonly float ZoomStep;

        /// <summary>是否以光标所在世界点作为缩放锚点（true）还是屏幕中心（false）。</summary>
        public readonly bool ZoomToCursor;

        /// <summary>释放拖拽后惯性滑动的初始速度上限（世界单位/秒）。</summary>
        public readonly float MaxPanSpeed;

        /// <summary>是否在松手后启用惯性滑动。</summary>
        public readonly bool InertiaEnabled;

        /// <summary>惯性滑动持续时间（秒），同时决定速度衰减时间常数。</summary>
        public readonly float InertiaDuration;

        /// <summary>是否只允许在空白区域（未命中可交互对象）发起平移。</summary>
        public readonly bool PanOnEmptyArea;

        /// <summary>是否把焦点限制在世界范围内。</summary>
        public readonly bool ClampToBounds;

        /// <summary>世界焦点范围：x = minX, y = maxX, z = minY, w = maxY。仅 ClampToBounds 为 true 时生效。</summary>
        public readonly Vector4 Bounds;

        /// <summary>常用默认参数。</summary>
        public static CameraBrowsingConfig Default
        {
            get
            {
                return new CameraBrowsingConfig(
                    zoomRange: new Vector2(1.0f, 40.0f),
                    zoomStep: 1.12f,
                    zoomToCursor: true,
                    maxPanSpeed: 60.0f,
                    inertiaEnabled: true,
                    inertiaDuration: 0.45f,
                    panOnEmptyArea: true,
                    clampToBounds: false,
                    bounds: new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
            }
        }

        /// <summary>
        /// 由"放大倍率"推导 orthoSize/fov 范围。
        /// 倍率以 baseZoom 为 1x 基准：越放大（倍率越大）orthoSize/fov 越小。
        /// 例如 base=5、minFactor=0.25、maxFactor=2 → 范围 (5/2, 5/0.25) = (2.5, 20)。
        /// </summary>
        public static Vector2 ZoomRangeFromFactors(float baseZoom, float minFactor, float maxFactor)
        {
            float safeBase = Mathf.Max(0.0001f, baseZoom);
            float safeMin = Mathf.Clamp(minFactor, 0.001f, 1.0f);
            float safeMax = Mathf.Max(maxFactor, safeMin);
            return new Vector2(safeBase / safeMax, safeBase / safeMin);
        }

        public CameraBrowsingConfig(
            Vector2 zoomRange,
            float zoomStep,
            bool zoomToCursor,
            float maxPanSpeed,
            bool inertiaEnabled,
            float inertiaDuration,
            bool panOnEmptyArea,
            bool clampToBounds,
            Vector4 bounds)
        {
            ZoomRange = new Vector2(Mathf.Min(zoomRange.x, zoomRange.y), Mathf.Max(zoomRange.x, zoomRange.y));
            ZoomStep = Mathf.Max(1.01f, zoomStep);
            ZoomToCursor = zoomToCursor;
            MaxPanSpeed = Mathf.Max(0.0f, maxPanSpeed);
            InertiaEnabled = inertiaEnabled;
            InertiaDuration = Mathf.Max(0.05f, inertiaDuration);
            PanOnEmptyArea = panOnEmptyArea;
            ClampToBounds = clampToBounds;
            Bounds = bounds;
        }

        /// <summary>把焦点限制到配置范围；未启用时原样返回。</summary>
        public Vector3 ClampFocal(Vector3 focal)
        {
            if (!ClampToBounds)
            {
                return focal;
            }

            float minX = Mathf.Min(Bounds.x, Bounds.y);
            float maxX = Mathf.Max(Bounds.x, Bounds.y);
            float minY = Mathf.Min(Bounds.z, Bounds.w);
            float maxY = Mathf.Max(Bounds.z, Bounds.w);

            focal.x = Mathf.Clamp(focal.x, minX, maxX);
            focal.y = Mathf.Clamp(focal.y, minY, maxY);
            return focal;
        }
    }
}
