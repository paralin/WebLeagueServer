using System;
using Newtonsoft.Json;

namespace WLNetwork.Matches
{
    public class MatchSetup
    {
        /// <summary>
        ///     Setup status for a match
        /// </summary>
        /// <param name="id"></param>
        public MatchSetup(Guid id, MatchSetupDetails details)
        {
            Id = id;
            ControllerGuid = Guid.Empty;
            Details = details;
        }

        /// <summary>
        ///     ID of the match
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        ///     Guid of the assigned controller
        /// </summary>
        [JsonIgnore]
        public Guid ControllerGuid { get; set; }

        /// <summary>
        ///     Details for the setup
        /// </summary>
        public MatchSetupDetails Details { get; set; }
    }
}