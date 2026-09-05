using System.Collections.Generic;
using QFramework;

namespace CUC260905.Message
{
    /// <summary>
    /// 游戏内提示消息的唯一写入口。
    /// targetId 与 MessageTerminalController 的终端标识一致时，消息才会显示在该终端。
    /// </summary>
    public interface IMessageSystem : ISystem
    {
        /// <summary>发布一条消息；目标标识或文本为空白时返回 false。</summary>
        bool Publish(string targetId, string text);

        /// <summary>返回指定终端当前保存的历史快照，调用方不可影响系统内历史。</summary>
        IReadOnlyList<SystemMessage> GetHistory(string targetId);
    }
}
