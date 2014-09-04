using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Model
{
    public class Message
    {
        /// <summary>
        /// Channel ID
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Chat message
        /// </summary>
        public string Text { get; set; }
          
        public bool Validate()
        {
            return Channel != null && Text != null;
        }
    }

    public class JoinCreateRequest
    {
        public string Name { get; set; }
    }

    public class LeaveRequest
    {
        public string Id { get; set; }
    }
}
