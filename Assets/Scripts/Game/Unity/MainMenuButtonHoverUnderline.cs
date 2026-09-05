using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CUC260905.Game
{
    /// <summary>为主菜单按钮提供文字下划线悬浮反馈。</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuButtonHoverUnderline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TMP_Text mLabel;
        private FontStyles mDefaultFontStyle;

        private void Awake()
        {
            mLabel = GetComponentInChildren<TMP_Text>(true);

            if (mLabel != null)
            {
                mDefaultFontStyle = mLabel.fontStyle;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (mLabel != null)
            {
                mLabel.fontStyle = mDefaultFontStyle | FontStyles.Underline;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            RestoreLabelStyle();
        }

        private void OnDisable()
        {
            RestoreLabelStyle();
        }

        private void RestoreLabelStyle()
        {
            if (mLabel != null)
            {
                mLabel.fontStyle = mDefaultFontStyle;
            }
        }
    }
}
