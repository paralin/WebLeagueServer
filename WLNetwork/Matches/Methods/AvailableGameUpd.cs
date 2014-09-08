using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Matches.Methods
{
    /// <summary>
    /// Add or update some available games
    /// </summary>
    public class AvailableGameUpd
    {
        public const string Msg = "availablegameupd";

        /// <summary>
        /// Players to add/update
        /// </summary>
        public MatchGame[] matches { get; set; }

        /// <summary>
        /// Add/update some channels.
        /// </summary>
        /// <param name="members"></param>
        public AvailableGameUpd(params MatchGame[] matches)
        {
            this.matches = matches;
        }
    }

    public class AvailableGameRm
    {
        public const string Msg = "availablegamerm";

        /// <summary>
        /// IDS of the matches
        /// </summary>
        public string[] ids { get; set; }

        /// <summary>
        /// Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public AvailableGameRm(params MatchGame[] matches)
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
