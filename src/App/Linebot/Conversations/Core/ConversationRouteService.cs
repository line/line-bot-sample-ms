using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Yamac.LineMessagingApi.Events;

namespace App.Linebot.Conversations.Core
{
    /// <summary>
    /// 会話のルートサービス <see cref="IConversationRouteService"/> の実装。
    /// </summary>
    public class ConversationRouteService : IConversationRouteService
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly MainDbContext _db;

        private readonly ILogger _logger;

        private readonly Dictionary<string, ConversationRouteInfo> _startRoutes = new Dictionary<string, ConversationRouteInfo>();

        private readonly Dictionary<string, ConversationRouteInfo> _replyRoutes = new Dictionary<string, ConversationRouteInfo>();

        private ConversationRouteInfo _globalRoute;

        public ConversationRouteService(IServiceProvider serviceProvider, MainDbContext db, ILoggerFactory loggerFactory)
        {
            _serviceProvider = serviceProvider;
            _db = db;
            _logger = loggerFactory.CreateLogger<ConversationRouteService>();

            RegisterConversationRoutes();
        }

        /// <summary>
        /// 会話のルートを登録。
        /// </summary>
        private void RegisterConversationRoutes()
        {
            _logger.LogDebug("RegisterConversationRoutes");
            Assembly.GetEntryAssembly().GetTypes()
                .Where(type => type.GetTypeInfo().IsDefined(typeof(ConversationAttribute), true) && type != typeof(ConversationBase))
                .ToList()
                .ForEach(RegisterConversationRoute);
        }

        /// <summary>
        /// 会話のルートの Type を登録。
        /// </summary>
        private void RegisterConversationRoute(Type type)
        {
            var routeAttr = type.GetTypeInfo().GetCustomAttribute(typeof(ConversationRouteAttribute)) as ConversationRouteAttribute;
            type.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(ConversationPathAttribute), false).Count() > 0)
                .ToList()
                .ForEach(m =>
                {
                    var path = routeAttr.Path.TrimEnd('/');
                    RegisterConversationRoute(path, type, routeAttr, m);
                });

            type.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(ConversationGlobalPathAttribute), false).Count() > 0)
                .ToList()
                .ForEach(m =>
                {
                    if (_globalRoute != null)
                    {
                        throw new ArgumentException($"Only one {nameof(ConversationGlobalPathAttribute)} can be attributed.");
                    }
                    _globalRoute = new ConversationRouteInfo
                    {
                        Path = null,
                        Type = type,
                        MethodInfo = m,
                    };
                });
        }

        /// <summary>
        /// 会話のルートの Path を登録。
        /// </summary>
        private void RegisterConversationRoute(string path, Type type, ConversationRouteAttribute routeAttr, MethodInfo methodInfo)
        {
            var pathAttr = methodInfo.GetCustomAttributes(typeof(ConversationPathAttribute), false).First() as ConversationPathAttribute;
            var subPath = pathAttr.Path ?? string.Empty;
            var fullPath = string.Join("/", path, subPath);

            if (pathAttr.Action == ConversationAction.Start)
            {
                _startRoutes[fullPath] = new ConversationRouteInfo
                {
                    Path = fullPath,
                    Type = type,
                    MethodInfo = methodInfo,
                };
            }
            else if (pathAttr.Action == ConversationAction.Reply)
            {
                _replyRoutes[fullPath] = new ConversationRouteInfo
                {
                    Path = fullPath,
                    Type = type,
                    MethodInfo = methodInfo,
                };
            }
            _logger.LogDebug($@"RegisterConversationRoute: Mapped {fullPath} => {type.Namespace}.{type.Name}.{methodInfo.Name}");
        }

        /// <summary>
        /// SenderId の会話を探す。
        /// </summary>
        private async Task<Conversation> FindConversationAsync(string senderId)
        {
            return await _db.Conversation.Where(x => x.SenderId == senderId).FirstOrDefaultAsync();
        }

        /// <summary>
        /// SenderId 会話を生成する。
        /// </summary>
        private async Task<Conversation> CreateOrUpdateConversationAsync(string senderId, string path, JObject data)
        {
            var conversation = await _db.Conversation.Where(x => x.SenderId == senderId).FirstOrDefaultAsync();
            if (conversation == null)
            {
                conversation = new Conversation
                {
                    SenderId = senderId,
                    Path = path,
                    Data = data?.ToString() ?? "{}",
                };

                _db.Add(conversation);
                await _db.SaveChangesAsync();
            }
            else
            {
                conversation.Path = path;
                conversation.Data = data?.ToString() ?? "{}";
                await _db.SaveChangesAsync();
            }

            return conversation;
        }

        /// <summary>
        /// 会話の開始をルートする。
        /// </summary>
        public async Task RouteStartActionAsync(string senderId, string path, JObject data)
        {
            await CreateOrUpdateConversationAsync(senderId, path, data);
            var conversation = await FindConversationAsync(senderId);
            data = JObject.Parse(conversation.Data);

            if (_startRoutes.ContainsKey(conversation.Path))
            {
                var routeInfo = _startRoutes[conversation.Path];
                var route = _serviceProvider.GetRequiredService(routeInfo.Type) as ConversationBase;

                _logger.LogDebug($@"RouteStartActionAsync: Invoke Start {conversation.Path} => {route}.{routeInfo.MethodInfo.Name}");
                try
                {
                    if (routeInfo.MethodInfo.ReturnType == typeof(Task))
                    {
                        await (routeInfo.MethodInfo.Invoke(route, new object[] { senderId, data }) as Task);
                    }
                    else
                    {
                        await Task.Factory.StartNew(() =>
                        {
                            routeInfo.MethodInfo.Invoke(route, new object[] { senderId, data });
                        });
                    }
                }
                catch (Exception e)
                {
                    // Ignore exception but logging.
                    _logger.LogError($"{nameof(RouteStartActionAsync)}: Exception={e.Message}");
                    await CreateOrUpdateConversationAsync(senderId, "/", null);
                }
            }
        }

        /// <summary>
        /// 会話の応答をルートする。
        /// </summary>
        public async Task RouteReplyActionAsync(Event theEvent)
        {
            var senderId = theEvent.Source.SenderId;
            var conversation = await FindConversationAsync(senderId);
            if (conversation == null)
            {
                conversation = await CreateOrUpdateConversationAsync(senderId, "/", null);
            }

            switch (theEvent.Type)
            {
                case EventType.Message:
                case EventType.Postback:
                    break;
                default:
                    if (_globalRoute != null)
                    {
                        var route = _serviceProvider.GetRequiredService(_globalRoute.Type) as ConversationBase;

                        _logger.LogDebug($@"{nameof(RouteReplyActionAsync)}: Invoke Reply Global => {route}.{_globalRoute.MethodInfo.Name}");
                        try
                        {
                            if (_globalRoute.MethodInfo.ReturnType == typeof(Task))
                            {
                                await (_globalRoute.MethodInfo.Invoke(route, new object[] { theEvent }) as Task);
                            }
                            else
                            {
                                await Task.Factory.StartNew(() =>
                                {
                                    _globalRoute.MethodInfo.Invoke(route, new object[] { theEvent });
                                });
                            }
                        }
                        catch (Exception e)
                        {
                            // Ignore exception but logging.
                            _logger.LogError($"{nameof(RouteReplyActionAsync)}: Exception={e.Message}");
                            await CreateOrUpdateConversationAsync(senderId, "/", null);
                        }
                    }
                    break;
            }

            if (_replyRoutes.ContainsKey(conversation.Path))
            {
                var routeInfo = _replyRoutes[conversation.Path];
                var route = _serviceProvider.GetRequiredService(routeInfo.Type) as ConversationBase;
                var data = JObject.Parse(conversation.Data ?? "{}");

                _logger.LogDebug($@"{nameof(RouteReplyActionAsync)}: Invoke Reply {conversation.Path} => {route}.{routeInfo.MethodInfo.Name}");
                try
                {
                    if (routeInfo.MethodInfo.ReturnType == typeof(Task))
                    {
                        await (routeInfo.MethodInfo.Invoke(route, new object[] { theEvent, data }) as Task);
                    }
                    else
                    {
                        await Task.Factory.StartNew(() =>
                        {
                            routeInfo.MethodInfo.Invoke(route, new object[] { theEvent, data });
                        });
                    }
                }
                catch (Exception e)
                {
                    // Ignore exception but logging.
                    _logger.LogError($"{nameof(RouteReplyActionAsync)}: Exception={e.Message}");
                    await CreateOrUpdateConversationAsync(senderId, "/", null);
                }
            }
        }

        /// <summary>
        /// 会話のルート情報。
        /// </summary>
        private class ConversationRouteInfo
        {
            public string Path { get; set; }

            public Type Type { get; set; }

            public MethodInfo MethodInfo { get; set; }
        }
    }
}
