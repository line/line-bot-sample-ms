using System;

namespace App.Linebot.Conversations.Core
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ConversationPathAttribute : Attribute
    {
        public ConversationPathAttribute() { }

        public ConversationPathAttribute(string path, ConversationAction action)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Action = action;
        }

        public string Path { get; set; }

        public ConversationAction Action { get; set; }
    }

    public enum ConversationAction
    {
        Start,
        Reply,
    }
}
