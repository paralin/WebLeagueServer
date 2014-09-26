using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLCommon
{
    public static class Utils
    {
        public static string ToSteamID64(this int accountid)
        {
            return (accountid + 76561197960265728) + "";
        }
    }
}
