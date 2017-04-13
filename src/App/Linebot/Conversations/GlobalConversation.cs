using App.Linebot.Conversations.Core;
using App.Linebot.Persons;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Yamac.LineMessagingApi.Events;

namespace App.Linebot.Conversations
{
    /// <summary>
    /// 全体イベント(Message, Postback 以外のイベント)の実装。
    /// </summary>
    public class GlobalConversation : ConversationBase
    {
        private LinebotOptions _options;

        private ILineMessagingService _lineMessagingService;

        private IPersonService _personService;

        private readonly ILogger _logger;

        public GlobalConversation(
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
        /// 全体イベントの実装。
        /// </summary>
        [ConversationGlobalPath]
        public async Task Global(Event theEvent)
        {
            var senderId = theEvent.Source.SenderId;

            switch (theEvent.Type)
            {
                case EventType.Leave:
                case EventType.Unfollow:
                    // グループ退会・ルーム退室なら忘れる
                    await _personService.ForgetAllAsync(senderId);
                    await StartConversationAsync(senderId, "/");
                    break;
            }
        }
    }
}
