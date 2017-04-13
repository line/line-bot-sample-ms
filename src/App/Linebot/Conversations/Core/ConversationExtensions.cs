using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;
using System.Reflection;

namespace App.Linebot.Conversations.Core
{
    public static class ConversationExtensions
    {
        public static void AddConversation(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            AddConversationRoutes(services);

            services.AddSingleton<IConversationRouteService, ConversationRouteService>();
        }

        private static void AddConversationRoutes(IServiceCollection services)
        {
            Assembly.GetEntryAssembly().GetTypes()
                .Where(type => type.GetTypeInfo().IsDefined(typeof(ConversationAttribute), true) && type != typeof(ConversationBase))
                .ToList()
                .ForEach(type =>
                {
                    AddConversationRoute(services, type);
                });
        }


        private static void AddConversationRoute(IServiceCollection services, Type type)
        {
            services.TryAddTransient(type, type);
        }
    }
}
