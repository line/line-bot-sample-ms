using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace App.Linebot.Conversations.Core
{
    [Conversation]
    public class ConversationBase
    {
        private readonly IConversationRouteService _conversationRouteService;

        public ConversationBase(IConversationRouteService conversationRouteService)
        {
            _conversationRouteService = conversationRouteService;
        }

        /// <summary>
        /// 会話を開始する。
        /// </summary>
        public async Task StartConversationAsync(string senderId, string path, JObject data = null)
        {
            await _conversationRouteService.RouteStartActionAsync(senderId, path, data);
        }
    }
}
