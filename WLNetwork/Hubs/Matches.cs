using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using log4net;
using WLNetwork.Chat;
using WLNetwork.Clients;
using WLNetwork.Leagues;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Matches.Methods;
using WLNetwork.Model;

namespace WLNetwork.Hubs
{
    /// <summary>
    ///     Games controller
    /// </summary>
    public class Matches : WebLeagueHub<Matches>
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public override Task OnConnected()
        {
            BrowserClient.HandleConnection(this.Context, this.Clients.Caller, BrowserClient.ClientType.CHAT);
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            BrowserClient.HandleDisconnected(this.Context, BrowserClient.ClientType.CHAT);
            return base.OnDisconnected(stopCalled);
        }

        /// <summary>
        ///     Returns a snapshot of the public game list.
        /// </summary>
        /// <returns></returns>
        public MatchGameInfo[] GetPublicGameList()
        {
            return MatchesController.PublicGames.ToArray();
        }

        /// <summary>
        ///     Returns a snapshot of games that the player could join.
        /// </summary>
        /// <returns></returns>
        public MatchGame[] GetAvailableGameList()
        {
            //todo: limit to non-in-progress matches
            return MatchesController.Games.ToArray();
        }

        /// <summary>
        ///     Create a new match.
        /// </summary>
        /// <param name="options">Match options</param>
        /// <returns>Error else null</returns>
        public string CreateMatch(MatchCreateOptions options)
        {
            BrowserClient client = Client;
            if (client?.User == null) return "You are not logged in.";
            LeaveMatch();
            if (options == null) return "You didn't give any options for the match.";
            if (client.Match != null) return "You are already in a match you cannot leave.";
            if (!client.User.authItems.Contains("startGames")) return "You cannot start games.";
            if (client.User.authItems.Contains("spectateOnly")) return "You are limited to spectating only.";
            if (client.User.authItems.Contains("challengeOnly")) return "You are limited to joining challenge pools only. You cannot create challenges/startgames.";
            if (!client.User.vouch.leagues.Contains(options.League)) return $"You are not in the league '{options.League}'!";
            League league;
            if (!LeagueDB.Leagues.TryGetValue(options.League, out league)) return "Can't find league " + options.League + "!";
            if (league.Archived || !league.IsActive) return "The league '" + league.Name + "' is currently inactive, you cannot create matches.";
            var start = league.Seasons[(int)league.CurrentSeason].Start;
            if (start > DateTime.UtcNow) return $"The league '{league.Name}' has not started yet.";
            if (league.RequireTeamspeak && !client.User.tsonline) return "Please join Teamspeak before joining games.";
            options.MatchType = MatchType.StartGame;
            options.League = league.Id;
            options.LeagueSeason = league.CurrentSeason;
            options.LeagueTicket = league.Seasons[(int) league.CurrentSeason].Ticket;
            options.LeagueRegion = league.Region;
            options.SecondaryLeagueSeason = league.SecondaryCurrentSeason.ToArray();
            var match = new MatchGame(client.User.steam.steamid, options);
            client.Match = match;
            match.Players.Add(new MatchPlayer(client.User, league.Id, league.CurrentSeason, league.SecondaryCurrentSeason.ToArray()) { IsCaptain = true });
            ChatChannel.SystemMessage(league.Id, client.User.profile.name + " created a new match.");
            return null;
        }

        /// <summary>
        ///     Starts the queue to find a bot for the match
        /// </summary>
        /// <returns></returns>
        public string StartMatch()
        {
            BrowserClient client = Client;
            if (client?.User == null) return "You are not logged in.";
            if (client.Match == null) return "You are not currently in a match.";
            if (client.User == null) return "You are not logged in for some reason.";
            if (client.User.authItems.Contains("spectateOnly")) return "You cannot start matches, you can spectate only.";
            var me = client.Match.Players.FirstOrDefault(m => m.SID == client.User.steam.steamid);
            if (me == null || !me.IsCaptain) return "You are not the host of this game.";
            if (client.Match.Setup != null || client.Match.Info.Status == MatchStatus.Teams)
            {
                return client.Match.Info.Status == MatchStatus.Lobby ? FinalizeMatch() : "The match is already being set up.";
            }
            if (client.Match.Info.MatchType == MatchType.Captains)
            {
                if (client.Match.Players.Count(m => m.Team != MatchTeam.Spectate) < 10) return "You need at least 10 players to start the challenge.";
                client.Match.StartPicks();
            }
            else if (client.Match.Info.MatchType == MatchType.StartGame)
            {
#if !DEBUG
                if (client.Match.Players.Count(m=> m.Team != MatchTeam.Spectate) < 10 && !client.User.authItems.Contains("admin"))
                    return "Non admins must have 10 players for start games.";
#endif
                client.Match.StartSetup();
            }
            return null;
        }

        /// <summary>
        ///     Pick a player in captains
        /// </summary>
        /// <param name="player"></param>
        public void PickPlayer(string steamid)
        {
            BrowserClient client = Client;
            if (client?.User == null) return;
            if (client.Match == null || client.Match.Info.MatchType != MatchType.Captains) return;
            MatchPlayer me = client.Match.Players.FirstOrDefault(m => m.IsCaptain && m.SID == client.User.steam.steamid);
            if (me == null) return;
            client.Match.PickPlayer(steamid, me.Team);
        }

        /// <summary>
        ///      Kick a player from a startgame
        /// </summary>
        /// <param name="player"></param>
        public void KickPlayer(string steamid)
        {
            BrowserClient client = Client;
            if (client?.User == null) return;
            if (client.Match == null || client.User == null || client.Match.Info.MatchType != MatchType.StartGame || client.Match.Info.Owner != client.User.steam.steamid || steamid == client.User.steam.steamid || client.Match.Info.Status > MatchStatus.Players) return;
            client.Match.KickPlayer(steamid);
        }

        /// <summary>
        ///     Dismiss a result.
        /// </summary>
        public void DismissResult()
        {
            BrowserClient client = Client;
            if (client?.User == null) return;
            client.Result = null;
        }

        /// <summary>
        ///     Starts the game in-game
        /// </summary>
        /// <returns></returns>
        public string FinalizeMatch()
        {
            BrowserClient client = Client;
            if (client?.User == null) return "You are not logged in.";
            if (client.Match == null) return "You are not currently in a match.";
            if (client.User == null) return "You are not logged in for some reason.";
            var me = client.Match.Players.FirstOrDefault(m => m.SID == client.User.steam.steamid);
            if (me == null || !me.IsCaptain) return "You are not the host of this game.";
            if (client.Match.Setup == null || client.Match.Setup.Details.Status != MatchSetupStatus.Wait ||
                client.Match.Players.Any(m => !m.Ready && m.Team < MatchTeam.Spectate))
                return "The match cannot be started yet.";
            client.Match.StartMatch();
            return null;
        }

        /// <summary>
        ///     Respond to a challenge
        /// </summary>
        /// <param name="resp"></param>
        public void ChallengeResponse(bool accept)
        {
            BrowserClient client = Client;
            if (client?.User == null) return;
            if (client.Challenge == null || client.Challenge.ChallengedSID != client.User.steam.steamid) return;
            client.ChallengeTimer.Stop();
            BrowserClient other;
            if (!BrowserClient.ClientsBySteamID.TryGetValue(client.Challenge.ChallengerSID, out other)) return;
            Challenge chal = client.Challenge;
            client.Challenge = null;
            if (other == null) return;
            other.Challenge = null;
            ChatChannel.SystemMessage(chal.League, client.User.profile.name + (accept ? " accepted the challenge." : " declined the challenge."));
            if (!accept) return;

            League league = null;
            if (!LeagueDB.Leagues.TryGetValue(chal.League, out league))
            {
                log.ErrorFormat("Unable to find league {0} for challenge created by {1}.", chal.League, chal.ChallengerName);
                return;
            }

            if (!league.IsActive || league.Archived)
            {
                log.ErrorFormat("League {0} isn't active / is archived.", league.Name);
                return;
            }

            //Create the match
            var match = new MatchGame(client.User.steam.steamid, new MatchCreateOptions
            {
                GameMode = chal.GameMode,
                MatchType = chal.MatchType,
                OpponentSID = other.User.steam.steamid,
                League = chal.League,
                LeagueSeason = league.CurrentSeason,
                LeagueTicket = league.Seasons[(int)league.CurrentSeason].Ticket,
                LeagueRegion = league.Region,
                SecondaryLeagueSeason = league.SecondaryCurrentSeason.ToArray()
            });
            client.Match = match;
            other.Match = match;
            match.Players.AddRange(new[]
            {
                new MatchPlayer(other.User, league.Id, league.CurrentSeason, league.SecondaryCurrentSeason.ToArray()) {IsCaptain = true, Team = MatchTeam.Dire},
                new MatchPlayer(client.User, league.Id, league.CurrentSeason, league.SecondaryCurrentSeason.ToArray()) {IsCaptain = true, Team = MatchTeam.Radiant}
            });
            if (chal.MatchType != MatchType.OneVsOne) return;
            log.Debug("Launching 1v1 match automatically!");
            match.StartSetup();
        }

        /// <summary>
        /// Delete a sent challenge.
        /// </summary>
        public void CancelChallenge()
        {
            BrowserClient client = Client;
            if (client?.User == null) return;

            var tchallenge = client.Challenge;

            client.Challenge = null;
            client.ChallengeTimer.Stop();

            if (tchallenge == null || client.User == null) return;

            var tsid = tchallenge.ChallengerSID == client.User.steam.steamid ? tchallenge.ChallengedSID : tchallenge.ChallengerSID;
            BrowserClient other;
            if (BrowserClient.ClientsBySteamID.TryGetValue(tsid, out other))
            {
                other.ChallengeTimer.Stop();
                other.Challenge = null;
            }

            client.Challenge = null;

            ChatChannel.SystemMessage(tchallenge.League, client.User.profile.name + " canceled his challenge.");
        }

        /// <summary>
        ///     Join an existing match.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public string JoinMatch(Guid id, bool spec)
        {
            BrowserClient client = Client;
            if (client?.User == null) return "You are not logged in.";
            if (client.Match != null && id == client.Match.Id) return "You are already in that match.";
            if (client.User.authItems.Contains("spectateOnly") && !spec) return "You cannot join matches, you can spectate only.";
            //LeaveMatch();
            if (client.Match != null) return "You are already in a match, leave that one first.";
            MatchGame match = MatchesController.Games.FirstOrDefault(m => m.Id == id && m.Info.Public);
            if (match == null) return "That match can't be found.";
            if (client.User != null && client.User.authItems.Contains("challengeOnly") && match.Info.MatchType != MatchType.Captains) return "You are limited to joining challenge pools only.";
            if (!client.User.vouch.leagues.Contains(match.Info.League)) return $"You are not in the league '{match.Info.League}'!";
            if (match.Info.Status != MatchStatus.Players && !spec && match.Info.Status != MatchStatus.Teams)
                return "Can't join a match that has started.";
            if (match.Info.Status > MatchStatus.Lobby && spec)
                return "Can't spectate a match already past the lobby stage.";
            if (!spec && match.Players.Count(m => m.Team != MatchTeam.Spectate) >= 10 && match.Info.MatchType != MatchType.Captains)
                return "That match is full.";
            if (match.PlayerForbidden(client.User.steam.steamid) && !spec)
                return "Can't join as you've been kicked from that startgame.";
            var league = LeagueDB.Leagues[match.Info.League];
            if (league != null && league.RequireTeamspeak && !client.User.tsonline && !spec) return "Please join Teamspeak before joining games.";
            client.Match = match;
            match.Players.Add(new MatchPlayer(client.User, match.Info.League, match.Info.LeagueSeason, match.Info.SecondaryLeagueSeason) { Team = spec ? MatchTeam.Spectate : MatchTeam.Unassigned });
            return null;
        }

        /// <summary>
        ///     Leave an existing match.
        ///     <returns>Error else null</returns>
        /// </summary>
        public string LeaveMatch()
        {
            BrowserClient client = Client;
            if (client?.User == null) return "You are not logged in.";
            if (client.Match == null) return "You are not currently in a match.";
            if (client.User == null) return "You are not signed in and thus cannot be in a match.";
            MatchPlayer me = client.Match.Players.FirstOrDefault(m => m.SID == client.User.steam.steamid);

            if (me == null)
            {
                client.Match = null;
                return null;
            }

            bool isOwner = client.Match.Info.Owner == client.User.steam.steamid || me.IsCaptain;
            if (me.Team < MatchTeam.Spectate && ((client.Match.Info.Status > MatchStatus.Lobby && isOwner) || (client.Match.Info.Status > MatchStatus.Players && !isOwner))) return "You cannot leave matches in progress.";
            if (isOwner)
                client.Match.Destroy();
            else
            {
                MatchPlayer plyr = client.Match.Players.FirstOrDefault(m => m.SID == client.User.steam.steamid);
                if (plyr != null) client.Match.Players.Remove(plyr);
                client.Match = null;
            }
            return null;
        }

        /// <summary>
        ///     Create a challenge
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public string StartChallenge(Challenge target)
        {
            BrowserClient client = Client;
            //todo: Allow challenges without a league
            if (client?.User == null) return "You are not logged in.";
            if (target == null) return "You need some info start a challenge.";
            if (client.Match != null) return "You are already in a match.";
            if (client.Challenge != null) return "Waiting for a challenge response already...";
            League league = null;
            if (!LeagueDB.Leagues.TryGetValue(target.League, out league) || league == null) return $"League {target.League} cannot be found.";
            if (!client.User.authItems.Contains("startGames")) return "You cannot start games.";
            if (client.User.authItems.Contains("spectateOnly")) return "You are spectator and cannot play matches.";
            if (client.User.authItems.Contains("challengeOnly")) return "You are limited to joining challenge pools only.";
            if (!client.User.vouch.leagues.Contains(target.League)) return "You are not in the league '" + target.League + "!";
            if (target.MatchType == 0) target.MatchType = MatchType.Captains;
            if (league.RequireTeamspeak && !client.User.tsonline && target.MatchType != MatchType.OneVsOne) return "Please join Teamspeak before joining games.";
            target.GameMode = target.MatchType == MatchType.OneVsOne ? GameMode.SOLOMID : GameMode.CM;
            target.ChallengerSID = client.User.steam.steamid;
            target.ChallengerName = client.User.profile.name;
            if (target.ChallengedSID == null) return "You didn't specify a person to challenge.";
            if (target.ChallengedSID == client.User.steam.steamid) return "You cannot challenge yourself!";
            BrowserClient tcont;
            if (!BrowserClient.ClientsBySteamID.TryGetValue(target.ChallengedSID, out tcont)) return "That player is no longer online.";
            if (tcont.Match != null) return "That player is already in a match.";
            if (tcont.Challenge != null) return "That player is already waiting for a challenge.";
            if (tcont.User.authItems.Contains("spectateOnly")) return "That player is a spectator and cannot play matches.";
            if (!tcont.User.vouch.leagues.Contains(target.League)) return "That player is not in the league '" + target.League + "!";
            if (league.RequireTeamspeak && !tcont.User.tsonline && target.MatchType != MatchType.OneVsOne) return "Please ask your target to join Teamspeak first.";
            var start = league.Seasons[(int)league.CurrentSeason].Start;
            if (start > DateTime.UtcNow && target.MatchType != MatchType.OneVsOne) return $"The league '{league.Name}' hasn't started yet.";
            target.ChallengedName = tcont.User.profile.name;
            target.ChallengedSID = tcont.User.steam.steamid;
            tcont.Challenge = target;
            tcont.ChallengeTimer.Start();
            client.Challenge = target;
            ChatChannel.SystemMessage(target.League, $"{client.User.profile.name} challenged {tcont.User.profile.name} to a {(target.MatchType == MatchType.OneVsOne ? "1v1" : "captains")} match!");
            return null;
        }
    }
}
