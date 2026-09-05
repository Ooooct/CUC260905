using CUC260905.Interaction;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CUC260905.Network
{
    /// <summary>
    /// 屏幕左下角总体负载 HUD。监听不可达事件，并把运行时状态渲染为红色 Slider 与百分比。
    /// 若场景未预先挂载该组件，启动器会自动将其添加到第一个非世界空间 Canvas。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GlobalLoadBarController : MonoBehaviour, IController, ICanSendEvent
    {
        [Header("总体负载规则")]
        [SerializeField, Range(0.0f, 1.0f)]
        [Tooltip("每个数据包无法到达目的地时增加的总体负载比例。")]
        private float mUnreachablePenalty = 0.05f;
        [SerializeField, Min(0.0f)]
        [Tooltip("总体负载每秒自动降低的比例，例如 0.02 代表每秒降低 2%。")]
        private float mDecreasePerSecond = 0.02f;

        [Header("HUD 引用（留空时运行时自动创建）")]
        [SerializeField] private Slider mLoadSlider;
        [SerializeField] private TMP_Text mPercentageText;

        private GlobalLoadState mLoadState;
        private IUnRegister mUnreachableRegistration;

        private void Awake()
        {
            mLoadState = new GlobalLoadState();
            EnsureView();
            RefreshView();
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            mUnreachableRegistration = this.RegisterEvent<PacketUnreachableEvent>(OnPacketUnreachable);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (mLoadState.Decay(Time.deltaTime, mDecreasePerSecond))
            {
                RefreshView();
            }
        }

        private void OnDestroy()
        {
            mUnreachableRegistration?.UnRegister();
            mUnreachableRegistration = null;
        }

        private void OnValidate()
        {
            mUnreachablePenalty = Mathf.Clamp01(mUnreachablePenalty);
            mDecreasePerSecond = Mathf.Max(0.0f, mDecreasePerSecond);
            if (!Application.isPlaying)
            {
                EnsureView();
                RefreshView();
            }
        }

        private void OnPacketUnreachable(PacketUnreachableEvent _)
        {
            bool reachedGameOver = mLoadState.AddUnreachablePenalty(mUnreachablePenalty);
            RefreshView();
            if (reachedGameOver)
            {
                this.SendEvent(new GameOverEvent());
            }
        }

        private void RefreshView()
        {
            float normalizedLoad = mLoadState == null ? 0.0f : mLoadState.NormalizedLoad;
            if (mLoadSlider != null)
            {
                mLoadSlider.SetValueWithoutNotify(normalizedLoad);
            }

            if (mPercentageText != null)
            {
                mPercentageText.text = $"{Mathf.RoundToInt(normalizedLoad * 100.0f)}%";
            }
        }

        private void EnsureView()
        {
            if (mLoadSlider != null && mPercentageText != null)
            {
                ConfigureSlider(mLoadSlider);
                return;
            }

            GameObject hudObject = new GameObject("GlobalLoadHUD", typeof(RectTransform));
            hudObject.transform.SetParent(transform, false);
            RectTransform hudTransform = hudObject.GetComponent<RectTransform>();
            hudTransform.anchorMin = Vector2.zero;
            hudTransform.anchorMax = Vector2.zero;
            hudTransform.pivot = Vector2.zero;
            hudTransform.anchoredPosition = new Vector2(32.0f, 32.0f);
            hudTransform.sizeDelta = new Vector2(250.0f, 56.0f);

            mLoadSlider = CreateSlider(hudTransform);
            mPercentageText = CreatePercentageText(hudTransform);
        }

        private static Slider CreateSlider(RectTransform parent)
        {
            GameObject sliderObject = new GameObject(
                "GlobalLoadSlider",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            RectTransform sliderTransform = sliderObject.GetComponent<RectTransform>();
            sliderTransform.anchorMin = Vector2.zero;
            sliderTransform.anchorMax = new Vector2(1.0f, 0.0f);
            sliderTransform.pivot = new Vector2(0.5f, 0.0f);
            sliderTransform.anchoredPosition = Vector2.zero;
            sliderTransform.sizeDelta = new Vector2(0.0f, 22.0f);

            Image background = sliderObject.GetComponent<Image>();
            background.color = new Color(0.16f, 0.025f, 0.025f, 0.95f);
            background.raycastTarget = false;

            GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaTransform = fillAreaObject.GetComponent<RectTransform>();
            fillAreaTransform.anchorMin = Vector2.zero;
            fillAreaTransform.anchorMax = Vector2.one;
            fillAreaTransform.offsetMin = new Vector2(2.0f, 2.0f);
            fillAreaTransform.offsetMax = new Vector2(-2.0f, -2.0f);

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            RectTransform fillTransform = fillObject.GetComponent<RectTransform>();
            fillTransform.anchorMin = Vector2.zero;
            fillTransform.anchorMax = new Vector2(1.0f, 1.0f);
            fillTransform.offsetMin = Vector2.zero;
            fillTransform.offsetMax = Vector2.zero;
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.92f, 0.05f, 0.05f, 1.0f);
            fillImage.raycastTarget = false;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fillTransform;
            ConfigureSlider(slider);
            return slider;
        }

        private static TMP_Text CreatePercentageText(RectTransform parent)
        {
            GameObject textObject = new GameObject(
                "GlobalLoadPercentage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform textTransform = textObject.GetComponent<RectTransform>();
            textTransform.anchorMin = new Vector2(0.0f, 1.0f);
            textTransform.anchorMax = new Vector2(1.0f, 1.0f);
            textTransform.pivot = new Vector2(0.0f, 1.0f);
            textTransform.anchoredPosition = new Vector2(0.0f, -2.0f);
            textTransform.sizeDelta = new Vector2(0.0f, 28.0f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = 22.0f;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureSlider(Slider slider)
        {
            slider.minValue = 0.0f;
            slider.maxValue = 1.0f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.interactable = false;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
