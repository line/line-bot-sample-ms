using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Yamac.LineMessagingApi.Events;

namespace App.Linebot.Conversations.Core
{
    /// <summary>
    /// 会話のルートサービス。
    /// </summary>
    public interface IConversationRouteService
    {
        /// <summary>
        /// 会話の開始。
        /// </summary>
        Task RouteStartActionAsync(string senderId, string path, JObject data = null);

        /// <summary>
        /// 会話の応答。
        /// </summary>
        Task RouteReplyActionAsync(Event theEvent);
    }
}
