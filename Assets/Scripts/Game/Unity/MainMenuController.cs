using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CUC260905.Game
{
    /// <summary>主菜单入口：开始新游戏前确保时间流速恢复为正常值。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private Button mStartButton;

        private void Awake()
        {
            mStartButton = GetComponent<Button>();
            mStartButton.onClick.AddListener(StartGame);
        }

        /// <summary>供“开始”按钮调用，进入主游戏场景。</summary>
        public void StartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("SampleScene");
        }

        private void OnDestroy()
        {
            if (mStartButton != null)
            {
                mStartButton.onClick.RemoveListener(StartGame);
            }
        }
    }
}
