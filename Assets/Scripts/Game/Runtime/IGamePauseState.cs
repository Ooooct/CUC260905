using QFramework;

namespace CUC260905.Game
{
    /// <summary>
    /// 暂停状态的共享可观察状态：IsPaused 为 true 表示模拟已冻结。
    /// 由 GamePauseController 写入，供输入/放置等世界交互层读取做暂停门控。
    /// </summary>
    public interface IGamePauseState : IModel
    {
        IBindableProperty<bool> IsPaused { get; }
    }

    /// <summary>暂停状态的默认实现。</summary>
    public sealed class GamePauseState : AbstractModel, IGamePauseState
    {
        public IBindableProperty<bool> IsPaused { get; }

        public GamePauseState()
        {
            IsPaused = new BindableProperty<bool>(false);
        }

        protected override void OnInit()
        {
        }
    }
}
