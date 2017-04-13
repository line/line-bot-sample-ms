using App.Linebot.Conversations.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Yamac.LineMessagingApi.AspNetCore.Middleware;
using Yamac.LineMessagingApi.Events;

namespace App.Linebot
{
    /// <summary>
    /// <see cref="ILineMessagingRequestHandler"/> の実装。
    /// LINE Messagin Service リクエストを <see cref="IConversationRouteService"/> でルートする処理を実装する。
    /// </summary>
    public class LineMessagingRequestHandler : ILineMessagingRequestHandler
    {
        private readonly IConversationRouteService _conversationRouteService;

        private readonly ILogger _logger;

        public LineMessagingRequestHandler(IConversationRouteService conversationRouteService, ILoggerFactory loggerFactory)
        {
            _conversationRouteService = conversationRouteService;
            _logger = loggerFactory.CreateLogger<LineMessagingRequestHandler>();
        }

        /// <summary>
        /// 受信した LINE Messagin Service リクエストの全てイベントを処理する。
        /// </summary>
        public async Task HandleRequestAsync(LineMessagingRequest request)
        {
            await Task.WhenAll(request.Events.Select(HandleEventAsync));
        }

        /// <summary>
        /// LINE Messagin Service イベントを処理する。
        /// <see cref="IConversationRouteService"/>
        /// </summary>
        public async Task HandleEventAsync(Event theEvent)
        {
            // Webhookのverifyは無視する
            if (theEvent.Source.SenderId == "Udeadbeefdeadbeefdeadbeefdeadbeef")
            {
                return;
            }

            try
            {
                // Reply Action をルートする
                // 本来なら Start, Reply が実行中の場合は処理を抑制 or キューイングする処理がないと誤動作するが、
                // 今回はハッカソンなので気にしない
                await _conversationRouteService.RouteReplyActionAsync(theEvent);
            }
            catch (Exception e)
            {
                // 例外はログ出力だけして無視する
                _logger.LogError($"{nameof(HandleEventAsync)}: Exception={e.Message}");
            }
        }
    }
}
