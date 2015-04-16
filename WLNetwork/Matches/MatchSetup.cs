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
            Details = details;
        }

        /// <summary>
        ///     ID of the match
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        ///     Details for the setup
        /// </summary>
        public MatchSetupDetails Details { get; set; }
    }
}