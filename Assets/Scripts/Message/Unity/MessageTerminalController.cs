using System.Collections.Generic;
using System.Text;
using CUC260905.Interaction;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CUC260905.Message
{
    /// <summary>
    /// 终端 UI 的表现层：显示一个目标标识的全部历史，并在新消息到来时滚至最新内容。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MessageTerminalController : MonoBehaviour, IController
    {
        [Header("终端")]
        [SerializeField]
        [Tooltip("与 IMessageSystem.Publish 的 targetId 对应，区分不同终端。")]
        private string mTargetId = "MainTerminal";

        [Header("显示")]
        [SerializeField]
        [Tooltip("承载全部历史消息的 TMP 文本，应位于 ScrollRect 的 Content。")]
        private TMP_Text mHistoryText;

        [SerializeField]
        [Tooltip("当前终端使用的滚动容器。")]
        private ScrollRect mScrollRect;

        [SerializeField]
        [Min(0f)]
        [Tooltip("写入消息后 Content 底部预留的空白高度。")]
        private float mBottomPadding = 12f;

        private readonly StringBuilder mTextBuilder = new StringBuilder();
        private IMessageSystem mMessageSystem;
        private IUnRegister mMessageRegistration;

        private void Start()
        {
            if (mHistoryText == null || mScrollRect == null)
            {
                Debug.LogError($"[{name}] 需要指定 History Text 与 Scroll Rect。", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(mTargetId))
            {
                Debug.LogError($"[{name}] 终端标识不能为空。", this);
                return;
            }

            mMessageSystem = this.GetSystem<IMessageSystem>();
            mMessageRegistration = this.RegisterEvent<SystemMessagePublishedEvent>(OnMessagePublished);
            RefreshHistory();
        }

        private void OnDestroy()
        {
            mMessageRegistration?.UnRegister();
        }

        private void OnMessagePublished(SystemMessagePublishedEvent publishedEvent)
        {
            SystemMessage message = publishedEvent.Message;
            if (!string.Equals(message.TargetId, mTargetId, System.StringComparison.Ordinal))
            {
                return;
            }

            if (mTextBuilder.Length > 0)
            {
                mTextBuilder.AppendLine();
            }

            mTextBuilder.Append(message.Text);
            ApplyTextAndScrollToBottom();
        }

        private void RefreshHistory()
        {
            IReadOnlyList<SystemMessage> history = mMessageSystem.GetHistory(mTargetId);
            mTextBuilder.Clear();
            for (int index = 0; index < history.Count; index++)
            {
                if (index > 0)
                {
                    mTextBuilder.AppendLine();
                }

                mTextBuilder.Append(history[index].Text);
            }

            ApplyTextAndScrollToBottom();
        }

        private void ApplyTextAndScrollToBottom()
        {
            mHistoryText.text = mTextBuilder.ToString();
            Canvas.ForceUpdateCanvases();

            RectTransform content = mHistoryText.rectTransform;
            float viewportHeight = mScrollRect.viewport == null ? 0f : mScrollRect.viewport.rect.height;
            float contentHeight = Mathf.Max(viewportHeight, mHistoryText.preferredHeight + mBottomPadding);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

            Canvas.ForceUpdateCanvases();
            mScrollRect.verticalNormalizedPosition = 0f;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
