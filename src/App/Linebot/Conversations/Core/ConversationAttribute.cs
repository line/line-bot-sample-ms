using System;

namespace App.Linebot.Conversations.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ConversationAttribute : Attribute
    {
    }
}
