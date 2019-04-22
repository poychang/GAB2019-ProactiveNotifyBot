using Microsoft.Bot.Builder;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using ProactiveNotify.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProactiveNotify
{
    /// <summary>每次和使用者的互動都會使用此類別實體，並執行 OnTurnAsync 方法</summary>
    /// <remarks>此實體的生命週期為 Transient，每個 Request 會建立一個新的實體</remarks>
    public class ProactiveNotifyBot : IBot
    {
        private readonly string _botAppId;
        private readonly NotifyState _notifyState;
        private readonly IStatePropertyAccessor<NotifyList> _notifyListAccessor;

        private const string HelpText =
            "輸入 'reg <username>' 或 '註冊 <username>' 將指定名稱註冊至 Notify 清單中\r\n" +
            "輸入 'show' 顯示所有已註冊的人員清單\r\n" +
            "輸入 'send <username>' 發送訊息給指定的人員\r\n" +
            "輸入 'help' 顯示命令清單";

        public ProactiveNotifyBot(NotifyState notifyState, EndpointService endpointService)
        {
            // Bot App ID 為必要資訊，用於取得已存在的對話參考，並繼續完成對話
            // 更多資訊請參考 <see cref="BotAdapter.ContinueConversationAsync"/>
            _botAppId = string.IsNullOrWhiteSpace(endpointService.AppId) ? "1" : endpointService.AppId;
            _notifyState = notifyState ?? throw new ArgumentNullException(nameof(notifyState));
            _notifyListAccessor = _notifyState.CreateProperty<NotifyList>(nameof(NotifyList));
        }

        /// <summary>每組回應對話框都會執行此方法</summary>
        /// <param name="turnContext">回應對話框的上下文</param>
        /// <param name="cancellationToken">註銷令牌</param>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // 取得使用者輸入的文字訊息
                var text = turnContext.Activity.Text.Trim().ToLowerInvariant();
                switch (text)
                {
                    case var _ when text.StartsWith("reg "):
                    case var _ when text.StartsWith("註冊 "):
                        await RegisterAsync(turnContext, text);
                        break;
                    case "show":
                        await ShowRegisterAsync(turnContext);
                        break;
                    case var _ when text.StartsWith("send "):
                        await SendMessageAsync(turnContext, text);
                        break;
                    case var _ when text.StartsWith("info"):
                        await turnContext.SendActivityAsync(JsonConvert.SerializeObject(turnContext.Activity, Formatting.Indented));
                        break;
                    case var _ when text.StartsWith("help"):
                    default:
                        await turnContext.SendActivityAsync(HelpText);
                        break;
                }
            }
            else
            {
                await OnSystemActivityAsync(turnContext);
            }
        }

        /// <summary>處理非傳送 Message 類型的活動</summary>
        /// <param name="turnContext">對話框上下文 <see cref="ITurnContext"/></param>
        /// <returns></returns>
        private async Task OnSystemActivityAsync(ITurnContext turnContext)
        {
            // 當對話機器人收到 NotifyEvent 自訂的活動類型時
            if (turnContext.Activity.Type is ActivityTypes.Event)
            {
                var notifyList = await _notifyListAccessor.GetAsync(turnContext, () => new NotifyList());
                var activity = turnContext.Activity.AsEventActivity();
                var notifyEvent = JsonConvert.DeserializeObject<NotifyEventModel>(activity.Value.ToString());

                if (activity.Name == "notifyEvent" && notifyList.ContainsKey(notifyEvent.Username))
                {
                    var text = $"send {notifyEvent.Username} {notifyEvent.Message}";
                    await SendMessageAsync(turnContext, text);
                }
            }
            // 當對話機器人收到 ConversationUpdate 活動類型時
            else if (turnContext.Activity.Type is ActivityTypes.ConversationUpdate)
            {
                if (turnContext.Activity.MembersAdded != null)
                {
                    await SendWelcomeMessageAsync(turnContext);
                }
            }
        }

        /// <summary>發送歡迎訊息給使用者</summary>
        /// <param name="turnContext">對話框上下文 <see cref="ITurnContext"/></param>
        /// <returns>></returns>
        private static async Task SendWelcomeMessageAsync(ITurnContext turnContext)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync($"{member.Name} 您好，歡迎使用主動式通知機器人\r\n{HelpText}");
                }
            }
        }

        /// <summary>將使用者名稱註冊至 Notify 清單中</summary>
        /// <param name="turnContext">對話框上下文 <see cref="ITurnContext"/></param>
        /// <param name="text">接收到的對話文字</param>
        /// <returns></returns>
        private async Task RegisterAsync(ITurnContext turnContext, string text)
        {
            var notifyList = await _notifyListAccessor.GetAsync(turnContext, () => new NotifyList());
            var username = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).First();

            await AddToNotifyListAsync(turnContext, username);
            await _notifyListAccessor.SetAsync(turnContext, notifyList);
            await _notifyState.SaveChangesAsync(turnContext);
            await turnContext.SendActivityAsync($"已將 '{username}' 註冊至通知清單");
        }

        /// <summary>顯示所有已註冊至 Notify 清單的使用者</summary>
        /// <param name="turnContext">對話框上下文 <see cref="ITurnContext"/></param>
        /// <returns></returns>
        private async Task ShowRegisterAsync(ITurnContext turnContext)
        {
            var notifyList = await _notifyListAccessor.GetAsync(turnContext, () => new NotifyList());
            if (notifyList.Any())
            {
                await turnContext.SendActivityAsync(
                    "| Username | Conversation ID |\r\n" +
                    "| -------- | --------------- |\r\n" +
                    string.Join("\r\n", notifyList.Values.Select(p => $"| {p.Username} | {p.ConversationRef.Conversation.Id.Split('|')[0]} |"))
                );
            }
            else
            {
                await turnContext.SendActivityAsync("Notify 清單沒有資料");
            }
        }

        /// <summary>發送訊息給指定的人員</summary>
        /// <param name="turnContext">對話框上下文 <see cref="ITurnContext"/></param>
        /// <param name="text">接收到的對話文字</param>
        /// <returns></returns>
        private async Task SendMessageAsync(ITurnContext turnContext, string text)
        {
            var notifyList = await _notifyListAccessor.GetAsync(turnContext, () => new NotifyList());
            var textParts = text?.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

            if (textParts?.Length == 3)
            {
                var textCommand = textParts[0];
                var textUsername = textParts[1];
                var textMessage = textParts[2];

                if (textCommand.ToLower().Equals("send") && !string.IsNullOrEmpty(textUsername))
                {
                    if (!notifyList.TryGetValue(textUsername, out ConversationInfo conversationInfo))
                    {
                        await turnContext.SendActivityAsync($"Notify 清單沒有 {conversationInfo.Username} 這位使用者的資料");
                    }
                    else
                    {
                        await SendingMessageAsync(turnContext.Adapter, conversationInfo, textMessage);
                    }
                }
            }
        }

        /// <summary>
        /// 將使用者加入通知清單中
        /// </summary>
        /// <param name="turnContext">對話框上下文 <see cref="ITurnContext"/></param>
        /// <param name="username">使用者名稱</param>
        private async Task AddToNotifyListAsync(ITurnContext turnContext, string username)
        {
            var notifyList = await _notifyListAccessor.GetAsync(turnContext, () => new NotifyList());
            // 檢查 NotifyList 的 Key 是否唯一
            if (notifyList.ContainsKey(username))
            {
                notifyList[username].ConversationRef = turnContext.Activity.GetConversationReference();
            }
            else
            {
                notifyList[username] = new ConversationInfo
                {
                    ConversationRef = turnContext.Activity.GetConversationReference(),
                    Username = username
                };
            }
        }

        /// <summary>
        /// 發送訊息給特定的使用者
        /// </summary>
        /// <param name="adapter">對話機器人橋接器，<see cref="BotAdapter"/></param>
        /// <param name="conversationInfo"><see cref="ConversationInfo"></param>
        /// <param name="message">要傳送的訊息</param>
        /// <param name="cancellationToken">註銷令牌</param>
        /// <returns></returns>
        private async Task SendingMessageAsync(BotAdapter adapter,
                                               ConversationInfo conversationInfo,
                                               string message,
                                               CancellationToken cancellationToken = default)
        {
            await adapter.ContinueConversationAsync(_botAppId,
                                                    conversationInfo.ConversationRef,
                                                    CreateCallback(conversationInfo, message),
                                                    cancellationToken);
        }

        /// <summary>
        /// 主動發送訊息到指定使用者的對話框
        /// </summary>
        /// <param name="conversationInfo"><see cref="ConversationInfo"></param>
        /// <param name="message">要傳送的訊息</param>
        /// <returns></returns>
        private BotCallbackHandler CreateCallback(ConversationInfo conversationInfo, string message)
        {
            return async (turnContext, token) =>
            {
                // 從 BotState 中取得通知清單
                var notifyList = await _notifyListAccessor.GetAsync(turnContext, () => new NotifyList());
                // 增加發送訊息的計數器
                notifyList[conversationInfo.Username].Count++;
                // 更新 NotifyList 的屬性值
                await _notifyListAccessor.SetAsync(turnContext, notifyList);
                // 儲存至 NotifyState
                await _notifyState.SaveChangesAsync(turnContext);
                // 主動發送訊息給指定使用者的對話框
                await turnContext.SendActivityAsync(message);
            };
        }
    }
}
