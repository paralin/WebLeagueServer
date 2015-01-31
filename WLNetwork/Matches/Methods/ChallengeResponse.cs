using System;
using SteamKit2.Internal;

namespace WLNetwork.Matches.Methods
{
    public class ChallengeResponse
    {
        public bool accept { get; set; }
    }

    public class ClearSetupMatch
    {
        public Guid Id { get; set; }
    }
}