using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dota2.GC.Dota.Internal;
using log4net;
using MongoDB.Driver.Builders;
using WLNetwork.Database;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;
using WLNetwork.Rating;

namespace WLNetwork.Matches
{
    /// <summary>
    ///     A player in a match
    /// </summary>
    public class MatchPlayer
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MatchPlayer(User user = null, string leagueid = null, uint leagueseason = 0,
            uint[] additionalSeasons = null)
        {
            if (additionalSeasons == null) additionalSeasons = new uint[0];
            if (user != null)
            {
                SID = user.steam.steamid;
                Name = user.profile.name;
                Avatar = user.steam.avatarfull;
                Team = MatchTeam.Unassigned;
                if (leagueid != null)
                {
                    if (!user.vouch.leagues.Contains(leagueid))
                    {
                        log.ErrorFormat("MatchPlayer created with a user {0} not in the league {1}.", user.profile.name,
                            leagueid);
                    }
                    else
                    {
                        LeagueProfile tprof = null;
                        if (user.profile.leagues == null)
                            user.profile.leagues = new Dictionary<string, LeagueProfile>();
                        foreach (var season in additionalSeasons.Concat(new[] {leagueseason}))
                        {
                            LeagueProfile prof = null;
                            if (!user.profile.leagues.TryGetValue(leagueid + ":" + season, out prof) ||
                                prof == null)
                            {
                                prof = new LeagueProfile();
                                prof.rating = RatingCalculator.BaseMmr;
                                user.profile.leagues[leagueid + ":" + season] = prof;
                                Mongo.Users.Update(Query<User>.EQ(m => m.Id, user.Id),
                                    Update<User>.Set(m => m.profile.leagues, user.profile.leagues));
                            }
                            if (season == leagueseason || tprof == null) tprof = prof;
                        }
                        Rating = (uint) tprof.rating;
                        WinStreak = (uint) tprof.winStreak;
                    }
                }
            }
        }

        /// <summary>
        ///     SteamID
        /// </summary>
        public string SID { get; set; }

        /// <summary>
        ///     Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Avatar
        /// </summary>
        public string Avatar { get; set; }

        /// <summary>
        ///     Team
        /// </summary>
        public MatchTeam Team { get; set; }

        /// <summary>
        ///     Is ready in the match?
        /// </summary>
        public bool Ready { get; set; }

        /// <summary>
        ///     Is a captain?
        /// </summary>
        public bool IsCaptain { get; set; }

        /// <summary>
        ///     Is this person a leaver?
        /// </summary>
        public bool IsLeaver { get; set; }

        /// <summary>
        ///     Reason they left
        /// </summary>
        public DOTALeaverStatus_t LeaverReason { get; set; }

        /// <summary>
        ///     Rating at the start of the match
        /// </summary>
        public uint Rating { get; set; }

        /// <summary>
        ///     Win streak before
        /// </summary>
        public uint WinStreak { get; set; }

        /// <summary>
        ///     Hero ID
        /// </summary>
        public HeroInfo Hero { get; set; }
    }
}