using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Dota2.GC.Dota.Internal;
using log4net;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using WLNetwork.Chat;
using WLNetwork.Clients;
using WLNetwork.Database;
using WLNetwork.Leagues;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;
using WLNetwork.Properties;
using WLNetwork.Rating;
using WLNetwork.Utils;
using MatchType = WLNetwork.Matches.Enums.MatchType;

namespace WLNetwork.Hubs
{
    /// <summary>
    ///     Admin controller
    /// </summary>
    public class Admin : WebLeagueHub<Admin>
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public override async Task OnConnected()
        {
            await base.OnConnected();
            BrowserClient.HandleConnection(this.Context, BrowserClient.HubType.Admin);
        }

        public override async Task OnDisconnected(bool stopCalled)
        {
            await base.OnDisconnected(stopCalled);
            BrowserClient.HandleDisconnected(this.Context);
        }

        /// <summary>
        ///     Returns a snapshot of the game list.
        /// </summary>
        /// <returns></returns>
        public MatchGame[] GetGameList()
        {
            return MatchesController.Games.ToArray();
        }

        /// <summary>
        ///     Destroys a match with no result.
        /// </summary>
        public void KillMatch(Guid id)
        {
            var client = Client;
            if (client?.User == null || !client.User.authItems.Contains("admin")) return;

            MatchesController.Games.FirstOrDefault(m => m.Id == id)?.AdminDestroy();
        }

        /// <summary>
        ///     Manually result a match
        /// </summary>
        /// <param name="id">Match ID</param>
        /// <param name="result">New result</param>
        public void ResultMatch(Guid id, EMatchResult result)
        {
            var client = Client;
            if (client?.User == null || !client.User.authItems.Contains("admin")) return;
            if (result == EMatchResult.DontCount || result == EMatchResult.Unknown) return;
            var match = MatchesController.Games.FirstOrDefault(m => m.Id == id);
            if (match?.Setup == null ||
                match.Setup.Details.State != DOTA_GameState.DOTA_GAMERULES_STATE_GAME_IN_PROGRESS)
                return;
            match.ProcessMatchResult(result, true);
        }

        /// <summary>
        ///     Recalculate a match result
        /// </summary>
        /// <param name="args"></param>
        public void RecalculateMatch(long id)
        {
            var client = Client;
            if (client?.User == null || !client.User.authItems.Contains("admin")) return;
            var res = Mongo.Results.FindOneAs<MatchResult>(Query.EQ("_id", id));
            if (res == null)
            {
                log.Warn("Can't find match " + id + ", not re-calculating.");
                return;
            }

            log.Info("Recalculating result " + id + " on request of admin.");
            res.RecalculateResult();
        }

        /// <summary>
        ///     Changes match result
        /// </summary>
        /// <returns>Any errors</returns>
        public string ChangeResult(ulong id, EMatchResult result)
        {
            var client = Client;
            if (client?.User == null || !client.User.authItems.Contains("admin")) return "You are not an admin.";
            if (id == 0)
                return "Invalid request.";
            var res = Mongo.Results.FindAs<MatchResult>(Query<MatchResult>.EQ(m => m.Id, id)).FirstOrDefault();
            if (res == null)
                return "Unable to find that match result.";
            if (res.Result == result)
                return "The result is already set to that.";
            if (res.Result == EMatchResult.DontCount && res.MatchType == MatchType.OneVsOne)
                return "Cannot count this match.";
            if (res.MatchType == MatchType.OneVsOne)
            {
                res.Result = EMatchResult.DontCount;
                res.Save();
                return "Unable to change 1v1 results.";
            }

            var oldResult = res.Result;
            log.Debug("Request to change result of " + res.Id + " from " + res.Result.ToString("G") + " to " +
                      result.ToString("G"));
            if (!res.AdjustResult(result))
                return "Don't know how to convert " + res.Result.ToString("G") + " to " + result.ToString("G") + ".";
            res.Save();

            ChatChannel.SystemMessage(res.League, "Admin "+client.User.profile.name+" changed result of "+id+" from "+oldResult.ToString("G")+" to "+res.Result.ToString("G")+".");
            return null;
        }

        /// <summary>
        /// Submit a new match result that isn't in the system. Must be a public match (dotabuff viewable).
        /// </summary>
        /// <param name="id">match ID</param>
        /// <param name="leagueid">league ID</param>
        /// <returns>Error string or null.</returns>
        public async Task<string> SubmitMatch(ulong id, string leagueid)
        {
            // First check for any existing 
            var client = Client;
            if (client?.User == null || !client.User.authItems.Contains("admin")) return "You are not an admin.";
            log.Debug($"Admin {client.User.profile.name} attempted to submit match {id} to league {leagueid}.");
            if (string.IsNullOrWhiteSpace(leagueid)) return "Not a valid league ID.";
            if (id == 0) return "Not a valid match ID.";

            League league;
            if (!LeagueDB.Leagues.TryGetValue(leagueid, out league)) return $"Unable to find league {leagueid}!";

            MatchResult existing = Mongo.Results.FindOneAs<MatchResult>(Query<MatchResult>.EQ(m => m.Id, id));
            if (existing != null)
            {
                if (existing.Result == EMatchResult.DireVictory || existing.Result == EMatchResult.RadVictory)
                    return "That match has already been resulted.";
                else
                {
                    log.Debug($"Deleting unknown result {id} in favor of API check for result as per request by admin.");
                    Mongo.Results.Remove(Query<MatchResult>.EQ(m => m.Id, id));
                }
            }

            log.Debug($"Looking up match {id}...");
            bool radiant_win;
            DateTime completed = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            uint ticket_id = 0;
            JToken[] players;
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var res =
                        await
                            httpClient.GetStringAsync(
                                $"https://api.steampowered.com/IDOTA2Match_570/GetMatchDetails/v001/?key={Settings.Default.SteamAPI}&match_id={id}");
                    var pars = JObject.Parse(res);
                    var result = pars["result"];
                    if (result["error"] != null)
                    {
                        return result["error"].Value<string>();
                    }
                    radiant_win = result["radiant_win"].Value<bool>();
                    completed =
                        completed.AddSeconds(result["start_time"].Value<double>() +
                                             result["duration"].Value<int>());
                    if (result["leagueid"] != null)
                    {
                        ticket_id = result["leagueid"].Value<uint>();
                    }
                    players = result["players"].ToArray();
                }
            }
            catch (Exception ex)
            {
                log.Error("Error while trying to check API for "+id, ex);
                return "Unable to check API, errors occured.";
            }

            var matchResult = new MatchResult
            {
                Id = id,
                League = league.Id,
                MatchId = existing?.MatchId,
                Result = radiant_win ? EMatchResult.RadVictory : EMatchResult.DireVictory,
                MatchCounted = true,
                MatchCompleted = completed,
                MatchType = existing?.MatchType ?? MatchType.StartGame,
                LeagueSeason = existing?.LeagueSeason ?? league.CurrentSeason,
                LeagueSecondarySeasons = existing?.LeagueSecondarySeasons ?? league.SecondaryCurrentSeason.ToArray(),
                TicketId = ticket_id
            };

            List<MatchResultPlayer> resultPlayers = new List<MatchResultPlayer>(players.Length);
            // Try to find match players
            foreach (var plyr in players)
            {
                var accid = plyr["account_id"].Value<uint>();
                var hero = HeroCache.Heros[plyr["hero_id"].Value<uint>()];
                ulong steamid = accid.ToSteamID64();
                var user = Mongo.Users.FindOneAs<User>(Query<User>.EQ(m => m.steam.steamid, steamid + ""));
                var userstr = $"User {steamid} account ID {accid} hero {hero.fullName}";
                if (user == null)
                {
                    var msg = $"{userstr} not found in the system.";
                    log.Warn(msg);
                    return msg;
                }

                if (user.vouch?.leagues == null || !user.vouch.leagues.Contains(league.Id))
                {
                    var msg = $"{userstr} not in league {league.Name}!";
                    log.Warn(msg);
                    return msg;
                }

                var team = ((8 & (1 << plyr["player_slot"].Value<byte>() - 1)) == 0) ? MatchTeam.Dire : MatchTeam.Radiant;
                var player = new MatchPlayer(user, league.Id, league.CurrentSeason,
                    league.SecondaryCurrentSeason.ToArray())
                {
                    Team = team,
                    Hero = hero
                };

                resultPlayers.Add(new MatchResultPlayer(player));
            }
            matchResult.Players = resultPlayers.ToArray();

            RatingCalculator.CalculateRatingDelta(matchResult);
            matchResult.ApplyRating(league.SecondaryCurrentSeason.Concat(new[] {league.CurrentSeason}).ToArray());
            matchResult.Save();

            ChatChannel.SystemMessage(league.Id,
                $"Match {matchResult.Id} submitted by {client.User.profile.name}. Result was {(matchResult.Result == EMatchResult.DireVictory ? "dire victory." : "radiant victory.")}");

            return null;
        }
    }
}