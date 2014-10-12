using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Matches.Methods
{
    /// <summary>
    /// Add or update some public matches.
    /// </summary>
    public class PublicMatchUpd
    {
        public const string Msg = "publicmatchupd";

        /// <summary>
        /// Matches to add/update
        /// </summary>
        public MatchGameInfo[] matches { get; set; }

        /// <summary>
        /// Add/update some channels.
        /// </summary>
        /// <param name="members"></param>
        public PublicMatchUpd(params MatchGameInfo[] matches)
        {
            this.matches = matches;
        }
    }

    public class PublicMatchRm
    {
        public const string Msg = "publicmatchrm";

        /// <summary>
        /// IDS of the matches
        /// </summary>
        public string[] ids { get; set; }

        /// <summary>
        /// Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public PublicMatchRm(params MatchGameInfo[] matches)
        {
            this.ids = new string[matches.Length];
            int i = 0;
            foreach (var match in matches)
            {
                this.ids[i] = match.Id.ToString();
                i++;
            }
        }
    }
}
