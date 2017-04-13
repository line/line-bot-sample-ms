using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Yamac.LineMessagingApi.AspNetCore.Middleware;
using Yamac.LineMessagingApi.Client;
using Messages = Yamac.LineMessagingApi.Messages;

namespace App.Linebot
{
    public class LineMessagingService : ILineMessagingService
    {
        private readonly ILineMessagingApi _api;

        private readonly ILogger _logger;

        public LineMessagingService(IOptions<LineMessagingMiddlewareOptions> options, ILoggerFactory loggerFactory)
        {
            _api = new LineMessagingApi(options.Value.ChannelAccessToken);
            _logger = loggerFactory.CreateLogger<LineMessagingRequestHandler>();
        }

        public async Task<Stream> GetMessageContentAsync(string messageId)
        {
            return await _api.GetMessageContentAsync(messageId);
        }

        public async Task ReplyMessageAsync(string replyToken, Messages.Message message)
        {
            var replyMessage = new Messages.ReplyMessage(replyToken, message);
            await _api.ReplyMessageAsync(replyMessage);
        }

        public async Task ReplyTextMessageAsync(string replyToken, string text)
        {
            var replyMessage = new Messages.ReplyMessage(
                replyToken,
                new Messages.TextMessage
                {
                    Text = text,
                });
            await _api.ReplyMessageAsync(replyMessage);
        }

        public async Task PushMessageAsync(string to, Messages.Message message)
        {
            var pushMessage = new Messages.PushMessage(to, message);
            await _api.PushMessageAsync(pushMessage);
        }

        public async Task PushTextMessageAsync(string to, string text)
        {
            var pushMessage = new Messages.PushMessage(
                to,
                new Messages.TextMessage
                {
                    Text = text,
                });
            await _api.PushMessageAsync(pushMessage);
        }

        public async Task MulticastAsync(List<string> to, Messages.Message message)
        {
            var multicastMessage = new Messages.Multicast(to, message);
            await _api.MulticastAsync(multicastMessage);
        }

        public async Task MulticastTextAsync(List<string> to, string text)
        {
            var multicastMessage = new Messages.Multicast(
                to,
                new Messages.TextMessage
                {
                    Text = text,
                });
            await _api.MulticastAsync(multicastMessage);
        }
    }
}
