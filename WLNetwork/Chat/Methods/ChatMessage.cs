using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Chat.Methods
{
    public class ChatMessage
    {
        public const string Msg = "onchatmessage";
        public ChatMember Member { get; set; }
        public string Text { get; set; }
    }
}
