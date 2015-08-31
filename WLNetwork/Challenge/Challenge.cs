using System;
using System.Linq;
using WLNetwork.Clients;
using WLNetwork.Matches.Enums;

namespace WLNetwork.Challenge
{
    public class Challenge
    {
        /// <summary>
        /// Challenge ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        ///     Challenger name
        /// </summary>
        public string ChallengerName { get; set; }

        /// <summary>
        ///     The steam id of the person challenging
        /// </summary>
        public string ChallengerSID { get; set; }

        /// <summary>
        ///     The challenged person
        /// </summary>
        public string ChallengedSID { get; set; }

        /// <summary>
        ///     Name of the challenged
        /// </summary>
        public string ChallengedName { get; set; }

        /// <summary>
        ///     League ID
        /// </summary>
        public string League { get; set; }

        /// <summary>
        ///     Game mode
        /// </summary>
        public GameMode GameMode { get; set; }

        /// <summary>
        ///     You can also do 1v1 challenge
        /// </summary>
        public MatchType MatchType { get; set; }

        /// <summary>
        /// Create a new challenge
        /// </summary>
        public Challenge()
        {
            Id = Guid.NewGuid();
            ChallengeController.Challenges[Id] = this;
        }

        /// <summary>
        /// Throw away the challenge.
        /// </summary>
        public void Discard()
        {
            Challenge thechallenge;
            ChallengeController.Challenges.TryRemove(Id, out thechallenge);
            Hubs.Matches.HubContext.Clients.Group(Id.ToString()).ClearChallenge();
            foreach (var cli in BrowserClient.Clients.Where(m => m.Value.User != null && (m.Value.User.steam.steamid == ChallengerSID || m.Value.User.steam.steamid == ChallengedSID)))
                Hubs.Matches.HubContext.Groups.Remove(cli.Key, Id.ToString());
        }
    }
}