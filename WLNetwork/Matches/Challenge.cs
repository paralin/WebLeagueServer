using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WLCommon.Matches.Enums;

namespace WLNetwork.Matches
{
    public class Challenge
    {
        /// <summary>
        /// Challenger name
        /// </summary>
        public string ChallengerName { get; set; }
        
        /// <summary>
        /// The steam id of the person challenging
        /// </summary>
        public string ChallengerSID { get; set; }

        /// <summary>
        /// The challenged person
        /// </summary>
        public string ChallengedSID { get; set; }

        /// <summary>
        /// Name of the challenged
        /// </summary>
        public string ChallengedName { get; set; }

        /// <summary>
        /// Game mode
        /// </summary>
        public GameMode GameMode { get; set; }
    }
}
