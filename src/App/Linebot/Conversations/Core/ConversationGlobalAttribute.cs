using System;

namespace App.Linebot.Conversations.Core
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ConversationGlobalPathAttribute : Attribute
    {
        public ConversationGlobalPathAttribute() { }
    }
}
