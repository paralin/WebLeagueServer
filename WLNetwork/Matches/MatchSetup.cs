using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLCommon.Model;

namespace WLNetwork.Matches
{
    public class MatchSetup
    {
        /// <summary>
        /// ID of the match
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// Status of the bot assigned to setup the match
        /// </summary>
        public MatchSetupStatus SetupStatus { get; set; }

        /// <summary>
        /// Guid of the assigned controller
        /// </summary>
        [JsonIgnore]
        public Guid ControllerGuid { get; set; }

        /// <summary>
        /// Details for the setup
        /// </summary>
        public MatchSetupDetails Details { get; set; }

        /// <summary>
        /// Setup status for a match
        /// </summary>
        /// <param name="id"></param>
        public MatchSetup(Guid id, MatchSetupDetails details)
        {
            this.Id = id;
            ControllerGuid = Guid.Empty;
            this.Details = details;
        }
    }
}
