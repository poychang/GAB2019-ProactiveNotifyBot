using Microsoft.Bot.Builder;

namespace ProactiveNotify.Models
{
    /// <summary>
    /// 用於管理 Notify 狀態的 <see cref="BotState"/>
    /// </summary>
    /// <remarks>
    /// <see cref="UserState"/> 和 <see cref="ConversationState"/> 這兩個 State 的最大差異
    /// 在於資料儲存的時效，而 NotifyState 是時效要與機器人相同，因此要獨立於這兩者
    /// </remarks>
    public class NotifyState : BotState
    {
        /// <summary>用於在回應上下文中暫存狀態資訊的密鑰</summary>
        private const string StorageKey = "ProactiveNotifyBot.NotifyState";

        /// <summary>建構式</summary>
        /// <param name="storage">儲存此狀態的儲存體</param>
        public NotifyState(IStorage storage) : base(storage, StorageKey)
        {
        }

        /// <summary>取得暫存狀態訊息的密鑰</summary>
        /// <param name="turnContext">回應上下文 <see cref="ITurnContext"/> 包含所有執行對話過程中所需的資訊</param>
        /// <returns>暫存狀態訊息的密鑰</returns>
        protected override string GetStorageKey(ITurnContext turnContext) => StorageKey;
    }
}
