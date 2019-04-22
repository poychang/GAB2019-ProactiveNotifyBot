using Microsoft.Bot.Schema;
using System.Collections.Generic;

namespace ProactiveNotify.Models
{
    /// <summary>
    /// 通知清單，使用鍵值結構儲存通知清單資訊，並使用使用者名稱作為鍵值，因此使用者名稱必須為唯一值
    /// </summary>
    public class NotifyList : Dictionary<string, ConversationInfo>
    {
    }

    /// <summary>
    /// 對話框狀態的資訊
    /// </summary>
    public class ConversationInfo
    {
        /// <summary>
        /// 取得或設定用於傳送訊息的 Conversation Reference 對話框參考
        /// </summary>
        public ConversationReference ConversationRef { get; set; }

        /// <summary>
        /// 使用者名稱
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 發送訊息的計數器
        /// </summary>
        public int Count { get; set; }
    }
}
