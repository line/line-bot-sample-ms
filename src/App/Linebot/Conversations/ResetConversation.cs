using App.Linebot.Conversations.Core;
using App.Linebot.Persons;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yamac.LineMessagingApi.Events;
using Messages = Yamac.LineMessagingApi.Messages;
using Templates = Yamac.LineMessagingApi.Messages.Templates;

namespace App.Linebot.Conversations
{
    /// <summary>
    /// 会話:忘れて の実装。
    /// </summary>
    [ConversationRoute("/reset")]
    public class ResetConversation : ConversationBase
    {
        private LinebotOptions _options;

        private ILineMessagingService _lineMessagingService;

        private IPersonService _personService;

        private readonly ILogger _logger;

        public ResetConversation(
            IConversationRouteService conversationRouteService,
            IOptions<LinebotOptions> options,
            ILineMessagingService lineMessagingService,
            IPersonService personService,
            ILoggerFactory loggerFactory) : base(conversationRouteService)
        {
            _options = options.Value;
            _lineMessagingService = lineMessagingService;
            _personService = personService;
            _logger = loggerFactory.CreateLogger<RootConversation>();
        }

        /// <summary>
        /// 会話:忘れての確認
        /// </summary>
        [ConversationPath("confirm", ConversationAction.Start)]
        public async Task ConfirmStart(string senderId, JObject data)
        {
            var replyToken = data["ReplyToken"].ToString();

            // 忘れるかやめるかの質問を発言
            var templateMessage = new Messages.TemplateMessage
            {
                AltText = @"せっかく覚えたけど全部忘れるよ？",
                Template = new Templates.ConfirmTemplate
                {
                    Text = @"せっかく覚えたけど全部忘れるよ？",
                    Actions = new List<Templates.Action>
                        {
                            new Templates.MessageAction
                            {
                                Label = @"忘れて！",
                                Text = @"忘れて！",
                            },
                            new Templates.MessageAction
                            {
                                Label = @"やめて！",
                                Text = @"やめて！",
                            },
                        }
                }
            };
            await _lineMessagingService.ReplyMessageAsync(replyToken, templateMessage);
        }

        /// <summary>
        /// 会話:忘れての確認
        /// </summary>
        [ConversationPath("confirm", ConversationAction.Reply)]
        public async Task ConfirmReply(Event theEvent, JObject data)
        {
            var senderId = theEvent.Source.SenderId;

            if (theEvent.Type != EventType.Message && (theEvent as MessageEvent).Message.Type != MessageType.Text)
            {
                // テキスト以外なら 会話:ルート へ
                await StartConversationAsync(senderId, "/");
                return;
            }

            var text = ((theEvent as MessageEvent).Message as TextMessage).Text;
            var replyToken = (theEvent as MessageEvent).ReplyToken;

            if (text.StartsWith(@"忘れて"))
            {
                // 忘れて ならすべて忘れる
                await ForgetAllAsync(senderId);
                await _lineMessagingService.ReplyTextMessageAsync(replyToken, @"全部忘れた！");
                await StartConversationAsync(senderId, "/");
            }
            else
            {
                // それ以外なら 会話:ルート へ
                await StartConversationAsync(senderId, "/");
            }
        }

        /// <summary>
        /// 全て忘れる。
        /// </summary>
        private async Task ForgetAllAsync(string senderId)
        {
            await _personService.ForgetAllAsync(senderId);
        }
    }
}
