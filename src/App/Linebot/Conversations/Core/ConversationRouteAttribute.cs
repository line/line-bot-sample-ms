using System;

namespace App.Linebot.Conversations.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ConversationRouteAttribute : Attribute
    {
        public ConversationRouteAttribute() { }

        public ConversationRouteAttribute(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string Path { get; set; }
    }
}
