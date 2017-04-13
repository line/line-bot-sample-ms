using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Messages = Yamac.LineMessagingApi.Messages;

namespace App.Linebot
{
    public interface ILineMessagingService
    {
        Task<Stream> GetMessageContentAsync(string messageId);

        Task ReplyMessageAsync(string replyToken, Messages.Message message);

        Task ReplyTextMessageAsync(string replyToken, string text);

        Task PushMessageAsync(string to, Messages.Message message);

        Task PushTextMessageAsync(string to, string text);

        Task MulticastAsync(List<string> to, Messages.Message message);

        Task MulticastTextAsync(List<string> to, string text);
    }
}
