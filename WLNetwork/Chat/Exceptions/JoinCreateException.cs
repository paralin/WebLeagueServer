using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Chat.Exceptions
{
    /// <summary>
    /// Thrown when a chat can't be joined or created
    /// </summary>
    public class JoinCreateException : Exception
    {
        public JoinCreateException(string message) : base(message)
        {
        }
    }
}
