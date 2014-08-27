using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Model
{
    public class AuthTokenString
    {
        public string token { get; set; }
    }
    public class AuthToken
    {
        public string _id { get; set; }
        public string steamid { get; set; }
    }
}
