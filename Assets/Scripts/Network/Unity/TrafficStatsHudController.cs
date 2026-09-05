using System;
using System.Globalization;
using CUC260905.Interaction;
using QFramework;
using TMPro;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 场景左上角"总传输量" HUD（表现层，仅保留文本）。
    /// 订阅统计模型的累计字节量（Mb），显示值以指数阻尼逐帧追赶目标：
    /// 每次数值追加后平滑"跳动"到新目标，而不是生硬跳变。
    /// 性能：单个 Update 驱动，不按数据包开协程；仅在显示值实际变化时重建文本，
    /// 静止时不产生字符串分配，动画期间重建频率也受一位小数粒度限制。
    /// 编辑模式由 TrafficStatsHudEditorSetup 自动挂载、Play 模式由 TrafficStatsHudBootstrap
    /// 自动挂载；也可直接在 Canvas 上手动挂载并指定文本引用。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TrafficStatsHudController : MonoBehaviour, IController
    {
        [Header("显示")]
        [SerializeField]
        [Tooltip("显示累计传输量的 TextMeshPro 文本对象（留空时自动创建）。")]
        private TMP_Text mValueText;

        [SerializeField]
        [Tooltip("文本格式，{0} 为累计值占位符，例如 {0:0.0} Mb。")]
        private string mDisplayFormat = "{0:0.0} Mb";

        [SerializeField, Min(1f)]
        [Tooltip("自动创建文本的字号（像素）。")]
        private float mFontSize = 22f;

        [Header("动画")]
        [SerializeField, Range(1f, 30f)]
        [Tooltip("显示值追赶目标的指数阻尼速率（/秒）；越大到位越快。")]
        private float mSnapPerSecond = 5f;

        [Header("布局（自动创建时生效）")]
        [SerializeField, Min(0f)]
        [Tooltip("距离屏幕左上角的水平边距（像素）。")]
        private float mMarginLeft = 16f;
        [SerializeField, Min(0f)]
        [Tooltip("距离屏幕左上角的垂直边距（像素）。")]
        private float mMarginTop = 16f;
        [SerializeField, Min(0f)]
        [Tooltip("自动创建视图的宽度（像素）。")]
        private float mViewWidth = 340f;
        [SerializeField, Min(0f)]
        [Tooltip("自动创建视图的高度（像素）。")]
        private float mViewHeight = 48f;

        private IPacketTrafficStatsModel mModel;
        private double mDisplayedMegabits;
        private double mLastRenderedRounded = double.MinValue;
        private bool mViewDirty = true;
        private string mFormatPrefix;
        private string mFormatSuffix;

        private void Awake()
        {
            EnsureView();
        }

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配。
        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            mModel = this.GetModel<IPacketTrafficStatsModel>();
            if (mModel == null)
            {
                Debug.LogWarning($"[{name}] 未找到 IPacketTrafficStatsModel，跳过传输量订阅。", this);
                return;
            }

            mDisplayedMegabits = mModel.TotalMegabits.Value;
            mViewDirty = true;

            // RegisterWithInitValue：先以当前值标记重绘，之后每次变化都标记；
            // 句柄随物体销毁自动取消注册，避免泄漏。
            mModel.TotalMegabits.RegisterWithInitValue(OnTotalMegabitsChanged)
                .UnRegisterWhenGameObjectDestroyed(this);
        }

        private void Update()
        {
            if (!Application.isPlaying || mModel == null || mValueText == null)
            {
                return;
            }

            double target = mModel.TotalMegabits.Value;
            if (mDisplayedMegabits < target)
            {
                // 指数阻尼：剩余差距按固定速率衰减，数值追加后平滑"跳动"到位。
                double remaining = target - mDisplayedMegabits;
                double step = remaining * (1d - Math.Exp(-mSnapPerSecond * Time.deltaTime));
                mDisplayedMegabits += step;
                if (target - mDisplayedMegabits < 0.05d)
                {
                    mDisplayedMegabits = target;
                }
            }
            else if (mDisplayedMegabits > target)
            {
                mDisplayedMegabits = target;
            }

            RefreshText();
        }

        private void OnTotalMegabitsChanged(double _)
        {
            // 目标值由 Update 直接从模型读取；这里仅标记需要重绘。
            mViewDirty = true;
        }

        private void OnValidate()
        {
            mSnapPerSecond = Mathf.Max(1f, mSnapPerSecond);
            mFontSize = Mathf.Max(1f, mFontSize);
            mMarginLeft = Mathf.Max(0f, mMarginLeft);
            mMarginTop = Mathf.Max(0f, mMarginTop);
            mViewWidth = Mathf.Max(0f, mViewWidth);
            mViewHeight = Mathf.Max(0f, mViewHeight);
            if (!Application.isPlaying)
            {
                // 编辑模式预览：确保视图存在并显示当前值（架构未装配时按 0 处理）。
                EnsureView();
                mDisplayedMegabits = mModel == null ? 0d : mModel.TotalMegabits.Value;
                RefreshText();
            }
        }

        private void RefreshText()
        {
            if (mValueText == null)
            {
                return;
            }

            // 以一位小数（0.05 为半格）为粒度判断显示值是否变化：
            // 动画期间最多每秒约十余次重建文本，静止时完全不重建，避免逐帧字符串分配。
            double rounded = Math.Round(mDisplayedMegabits, 1, MidpointRounding.AwayFromZero);
            if (!mViewDirty && rounded == mLastRenderedRounded)
            {
                return;
            }

            mViewDirty = false;
            mLastRenderedRounded = rounded;

            if (mFormatPrefix == null)
            {
                SplitFormat();
            }

            string valueText = rounded.ToString("0.0", CultureInfo.InvariantCulture);
            mValueText.text = mFormatPrefix + valueText + mFormatSuffix;
        }

        private void SplitFormat()
        {
            string format = string.IsNullOrEmpty(mDisplayFormat) ? "{0}" : mDisplayFormat;
            int placeholderStart = format.IndexOf("{0", StringComparison.Ordinal);
            if (placeholderStart >= 0)
            {
                int placeholderEnd = format.IndexOf('}', placeholderStart);
                if (placeholderEnd > placeholderStart)
                {
                    mFormatPrefix = format.Substring(0, placeholderStart);
                    mFormatSuffix = format.Substring(placeholderEnd + 1);
                    return;
                }
            }

            mFormatPrefix = format;
            mFormatSuffix = string.Empty;
        }

        private void EnsureView()
        {
            if (mValueText != null)
            {
                return;
            }

            // 只创建文本本身，不添加背景，保持左上角 HUD 轻量。
            GameObject textObject = new GameObject(
                "TrafficStatsValue",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            RectTransform textTransform = textObject.GetComponent<RectTransform>();
            textTransform.anchorMin = new Vector2(0f, 1f);
            textTransform.anchorMax = new Vector2(0f, 1f);
            textTransform.pivot = new Vector2(0f, 1f);
            textTransform.anchoredPosition = new Vector2(mMarginLeft, -mMarginTop);
            textTransform.sizeDelta = new Vector2(mViewWidth, mViewHeight);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.TopLeft;
            text.fontSize = mFontSize;
            text.color = Color.white;
            text.raycastTarget = false;
            mValueText = text;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
