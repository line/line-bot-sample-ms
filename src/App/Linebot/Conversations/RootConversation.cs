using App.Linebot.Conversations.Core;
using App.Linebot.Persons;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Yamac.LineMessagingApi.Events;

namespace App.Linebot.Conversations
{
    /// <summary>
    /// 会話のルートの実装。
    /// </summary>
    [ConversationRoute("/")]
    public class RootConversation : ConversationBase
    {
        private LinebotOptions _options;

        private ILineMessagingService _lineMessagingService;

        private IPersonService _personService;

        private readonly ILogger _logger;

        public RootConversation(
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
        /// 会話:ルート
        /// </summary>
        [ConversationPath(Action = ConversationAction.Reply)]
        public async Task Root(Event theEvent, JObject data)
        {
            // MessageEvent 以外は無視
            if (theEvent.Type != EventType.Message)
            {
                return;
            }

            var messageEvent = theEvent as MessageEvent;
            var senderId = theEvent.Source.SenderId;
            var replyToken = messageEvent.ReplyToken;

            switch (messageEvent.Message.Type)
            {
                case MessageType.Text:
                    var explained = bool.Parse((data["Explained"]?.ToString() ?? "false").ToString());
                    await HandleTextMessageAsync(senderId, replyToken, messageEvent.Message as TextMessage, explained);
                    break;
                case MessageType.Image:
                    await HandleImageMessageAsync(senderId, replyToken, messageEvent.Message as ImageMessage, rememberMode: false);
                    break;
            }
        }

        /// <summary>
        /// 会話:覚えて
        /// </summary>
        [ConversationPath("remember", ConversationAction.Start)]
        public async Task RememberStart(string senderId, JObject data)
        {
            var replyToken = data["ReplyToken"].ToString();
            await _lineMessagingService.ReplyTextMessageAsync(replyToken, @"顔を覚えるから顔が写った画像を送って！");
        }

        /// <summary>
        /// 会話:覚えて
        /// </summary>
        [ConversationPath("remember", ConversationAction.Reply)]
        public async Task RememberReply(Event theEvent, JObject data)
        {
            // MessageEvent 以外は無視
            if (theEvent.Type != EventType.Message)
            {
                return;
            }

            var messageEvent = theEvent as MessageEvent;
            var senderId = messageEvent.Source.SenderId;
            var replyToken = messageEvent.ReplyToken;

            switch (messageEvent.Message.Type)
            {
                case MessageType.Image:
                    // 画像の場合は RememberMode で画像を処理
                    await HandleImageMessageAsync(senderId, replyToken, messageEvent.Message as ImageMessage, rememberMode: true);
                    break;
                default:
                    // それ以外の場合は 会話:ルート に移動
                    await _lineMessagingService.ReplyTextMessageAsync(replyToken, @"何？ よくわからないから覚えないよ。");
                    await StartConversationAsync(senderId, "/");
                    break;
            }
        }

        /// <summary>
        /// テキストの処理。
        /// </summary>
        private async Task HandleTextMessageAsync(string senderId, string replyToken, TextMessage textMessage, bool explained)
        {
            var text = textMessage.Text;

            if (text.Contains(@"覚えて"))
            {
                // 会話:覚えて に移動
                var data = new JObject
                {
                    ["ReplyToken"] = replyToken
                };
                await StartConversationAsync(senderId, "/remember", data);
            }
            else if (text == @"リセット" || text == @"忘れて")
            {
                // 会話:忘れて に移動
                var data = new JObject
                {
                    ["ReplyToken"] = replyToken
                };
                await StartConversationAsync(senderId, "/reset/confirm", data);
            }
            else
            {
                // 未説明の場合のみ(会話:ルート での連続したテキストは一回のみ処理)
                if (!explained)
                {
                    // 使い方を発言
                    await _lineMessagingService.ReplyTextMessageAsync(replyToken,
                        @"みんなの顔を覚えたいから顔が写った画像を送って！ もし間違ってたら「覚えて」って言ってね！ 最初からやり直したかったら「忘れて」って言ってね！");

                    var data = new JObject
                    {
                        ["Explained"] = true
                    };
                    await StartConversationAsync(senderId, "/", data);
                }
            }
        }

        /// <summary>
        /// 画像の処理。
        /// </summary>
        private async Task HandleImageMessageAsync(string senderId, string replyToken, ImageMessage imageMessage, bool rememberMode = false)
        {
            // 会話:顔の検出 へ移動
            var data = new JObject
            {
                ["ReplyToken"] = replyToken,
                ["MessageId"] = imageMessage.Id,
                ["RememberMode"] = rememberMode
            };
            await StartConversationAsync(senderId, "/image/detect", data);
        }
    }
}
