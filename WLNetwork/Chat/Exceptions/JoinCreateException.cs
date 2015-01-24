using System;

namespace WLNetwork.Chat.Exceptions
{
    /// <summary>
    ///     Thrown when a chat can't be joined or created
    /// </summary>
    public class JoinCreateException : Exception
    {
        public JoinCreateException(string message) : base(message)
        {
        }
    }
}