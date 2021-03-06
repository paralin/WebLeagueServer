﻿using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Timers;
using JWT;
using log4net;
using Microsoft.AspNet.SignalR.Hubs;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using WLNetwork.Challenge;
using WLNetwork.Chat;
using WLNetwork.Chat.Enums;
using WLNetwork.Database;
using WLNetwork.Hubs;
using WLNetwork.Leagues;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;
using WLNetwork.Properties;

namespace WLNetwork.Clients
{
    /// <summary>
    ///     Instance of a browser client.
    /// </summary>
    public class BrowserClient
    {
        /// <summary>
        ///     Hub type
        /// </summary>
        public enum HubType
        {
            Chat,
            Matches,
            Admin
        }

        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        ///     Dictionary of all clients.
        /// </summary>
        public static ConcurrentDictionary<string, BrowserClient> Clients =
            new ConcurrentDictionary<string, BrowserClient>();

        /// <summary>
        ///     Clients by Steam ID
        /// </summary>
        //todo: check usage and make sure Groups is used always
        public static ConcurrentDictionary<string, BrowserClient> ClientsBySteamID =
            new ConcurrentDictionary<string, BrowserClient>();

        /// <summary>
        ///     Timer until the challenge times out.
        /// </summary>
        public readonly Timer ChallengeTimer;

        private readonly bool userped = false;

        /// <summary>
        ///     Current channels list.
        /// </summary>
        public ObservableCollection<ChatChannel> Channels = new ObservableCollection<ChatChannel>();

        /// <summary>
        ///     Current ChatMember match.
        /// </summary>
        private ChatMember member;

        /// <summary>
        ///     Create a new browserclient
        /// </summary>
        /// <param name="user">User</param>
        /// <param name="ctx">Caller context</param>
        private BrowserClient(User user)
        {
            ChallengeTimer = new Timer(30000);
            ChallengeTimer.Elapsed += (sender, args) =>
            {
                if (Challenge != null)
                {
                    Challenge.Discard();
                    BrowserClient tcont;
                    if (ClientsBySteamID.TryGetValue(Challenge.ChallengerSID, out tcont))
                        tcont.ChallengeTimer?.Stop();
                }
                ChallengeTimer.Stop();
            };
            Channels.CollectionChanged += ChatChannelOnCollectionChanged;
            ChatMember memb;
            MemberDB.Members.TryGetValue(user.steam.steamid, out memb);
            UpdateUser(user, memb);
            if (member == null) return;
            member.State = UserState.ONLINE;
            member.StateDesc = "Online";
        }

        /// <summary>
        ///     All currently connected admin clients.
        /// </summary>
        public dynamic AdminClients => Admin.HubContext.Clients.Group(User.steam.steamid);

        /// <summary>
        ///     All currently connected chat clients.
        /// </summary>
        public dynamic ChatClients => Hubs.Chat.HubContext.Clients.Group(User.steam.steamid);

        /// <summary>
        ///     All currently connected match clients.
        /// </summary>
        public dynamic MatchClients => Hubs.Matches.HubContext.Clients.Group(User.steam.steamid);

        /// <summary>
        ///     Gets the local user.
        /// </summary>
        public User User { get; private set; }

        /// <summary>
        ///     The active match the player is in.
        /// </summary>
        public MatchGame Match
            => MatchesController.Games.FirstOrDefault(m => m.Players.Any(x => x.SID == User.steam.steamid));

        /// <summary>
        ///     The active challenge the player is in.
        /// </summary>
        public Challenge.Challenge Challenge
            =>
                ChallengeController.Challenges.Values.FirstOrDefault(
                    m => m.ChallengedSID == User.steam.steamid || m.ChallengerSID == User.steam.steamid);

        /// <summary>
        ///     Build a browserclient from a CallerContext
        /// </summary>
        /// <param name="ctx"></param>
        public static void HandleConnection(HubCallerContext ctx, HubType connType)
        {
            BrowserClient exist = null;
            if (!Clients.TryGetValue(ctx.ConnectionId, out exist))
            {
                log.Debug("CONNECTED [" + ctx.ConnectionId + "]");

                // Check the authentication
                try
                {
                    string token = ctx.QueryString["token"];
                    string jsonPayload = JsonWebToken.Decode(token, Settings.Default.AuthSecret);
                    var atoken = JObject.Parse(jsonPayload).ToObject<AuthToken>();
                    try
                    {
                        var nuser =
                            Mongo.Users.FindOneAs<User>(Query.And(Query.EQ("_id", atoken._id),
                                Query.EQ("steam.steamid", atoken.steamid)));
                        if (nuser != null)
                        {
                            if (nuser.vouch != null)
                            {
                                log.Debug("AUTHED [" + ctx.ConnectionId + "] => [" + nuser.steam.steamid + "]");
                                if (!ClientsBySteamID.TryGetValue(nuser.steam.steamid, out exist))
                                    exist = ClientsBySteamID[nuser.steam.steamid] = new BrowserClient(nuser);
                            }
                            else
                            {
                                log.Warn("Unvouched user tried to connect.");
                                return;
                            }
                        }
                        else
                        {
                            log.Warn("Authentication token valid but no user for " + jsonPayload);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn("Issue authenticating decrypted token " + atoken._id, ex);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Unable to authenticate user", ex);
                    return;
                }
            }

            var user = exist.User;

            // Add our new connection ID 
            Clients[ctx.ConnectionId] = exist;

            // Register any groups here
            switch (connType)
            {
                case HubType.Admin:
                    Admin.HubContext.Groups.Add(ctx.ConnectionId, user.steam.steamid);
                    break;
                case HubType.Chat:
                    Hubs.Chat.HubContext.Groups.Add(ctx.ConnectionId, user.steam.steamid);

                    // Transmit the members snapshot
                    Hubs.Chat.HubContext.Clients.Client(ctx.ConnectionId)
                        .GlobalMemberSnapshot(MemberDB.Members.Values.ToArray());

                    var hubctx = Hubs.Chat.HubContext;
                    foreach (var chan in exist.Channels)
                    {
                        hubctx.Groups.Add(ctx.ConnectionId, chan.Id.ToString());
                        hubctx.Clients.Client(ctx.ConnectionId).ChannelUpdate(chan);
                        League leaguel;
                        if (LeagueDB.Leagues.TryGetValue(chan.Name, out leaguel) &&
                            leaguel.MotdMessages != null)
                            foreach (var msg in leaguel.MotdMessages)
                                Hubs.Chat.HubContext.Clients.Client(ctx.ConnectionId)
                                    .OnChatMessage(chan.Id.ToString(), "system", "MOTD: " + msg, true, DateTime.UtcNow,
                                        chan.Name);
                    }

                    break;
                case HubType.Matches:
                    Hubs.Matches.HubContext.Groups.Add(ctx.ConnectionId, user.steam.steamid);
                    var match = exist.Match ??
                                MatchesController.Games.FirstOrDefault(
                                    m => m.Players.Any(x => x.SID == user.steam.steamid));
                    if (match != null)
                        Hubs.Matches.HubContext.Groups.Add(ctx.ConnectionId, match.Id.ToString());
                    Hubs.Matches.HubContext.Clients.Client(ctx.ConnectionId)
                        .AvailableGameUpdate(MatchesController.Games.ToArray());
                    var chal = exist.Challenge ?? ChallengeController.Challenges.Values.FirstOrDefault(
                        m =>
                            m.ChallengerSID == user.steam.steamid ||
                            m.ChallengedSID == user.steam.steamid);
                    if (chal != null)
                        Hubs.Matches.HubContext.Groups.Add(ctx.ConnectionId, chal.Id.ToString());
                    Hubs.Matches.HubContext.Clients.Client(ctx.ConnectionId).ChallengeSnapshot(chal);
                    break;
            }
        }

        /// <summary>
        ///     Called when the client disconnects.
        /// </summary>
        /// <param name="ctx"></param>
        public static void HandleDisconnected(HubCallerContext ctx)
        {
            BrowserClient cli;
            if (Clients.TryRemove(ctx.ConnectionId, out cli))
                log.Debug("DISCONNECTED [" + ctx.ConnectionId + "]");
            if (cli != null && cli.User != null && !Clients.Values.Contains(cli))
            {
                ClientsBySteamID.TryRemove(cli.User.steam.steamid, out cli);
                cli.Destroy();
            }
        }

        /// <summary>
        ///     Called when a chat channel is added/removed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChatChannelOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (var chan in e.NewItems.OfType<ChatChannel>().ToArray())
                    Hubs.Chat.HubContext.Clients.Group(User.steam.steamid).ChannelUpdate(chan);
            if (e.OldItems != null)
                foreach (var chan in e.OldItems.OfType<ChatChannel>().ToArray())
                    Hubs.Chat.HubContext.Clients.Group(User.steam.steamid).ChannelRemove(chan.Id);
        }

        /// <summary>
        ///     Recheck chats
        /// </summary>
        private void RecheckChats()
        {
            if (member?.Leagues == null || Channels == null) return;
            var memberleagues = member.Leagues;
            var chanarr = Channels.ToArray();
            var channames = chanarr.Select(m => m.Name);
            var leagueChans = chanarr.Where(m => m != null && m.ChannelType == ChannelType.League);
            lock (Channels)
            {
                foreach (var league in leagueChans.Where(league => !memberleagues.Contains(league.Name)))
                {
                    lock (league.Members)
                        league.Members.Remove(User.steam.steamid);
                    Channels.Remove(league);
                }
                foreach (var league in memberleagues.Where(league => !channames.Contains(league)))
                {
                    ChatChannel chan = ChatChannel.JoinOrCreate(league, member, ChannelType.League);
                    if (chan != null)
                        Channels.Add(chan);
                    League leaguel;
                    if (LeagueDB.Leagues.TryGetValue(league, out leaguel) && leaguel.MotdMessages != null)
                        foreach (var msg in leaguel.MotdMessages.ToArray())
                            ChatChannel.SystemMessage(league, "MOTD: " + msg, User.steam.steamid);
                }
            }
        }

        /// <summary>
        ///     Watch changes on the member.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="propertyChangedEventArgs"></param>
        private void MemberPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            RecheckChats();
        }

        /// <summary>
        ///     Try to leave the current match.
        /// </summary>
        public string LeaveMatch()
        {
            if (User == null) return "You are not logged in.";
            var match = Match;
            var me = match?.Players.FirstOrDefault(m => m.SID == User.steam.steamid);
            if (me == null) return "You are not in any game.";

            bool isOwner = Match.Info.Owner == User.steam.steamid || me.IsCaptain;
            if (me.Team < MatchTeam.Spectate &&
                ((Match.Info.Status > MatchStatus.Lobby) || (Match.Info.Status > MatchStatus.Players && !isOwner)))
                return "You cannot leave games in progress.";
            if (isOwner)
                Match.Destroy();
            else
                Match.Players.Remove(me);

            return null;
        }

        /// <summary>
        ///     Force set the user.
        /// </summary>
        /// <param name="user">user object</param>
        /// <param name="memb">new member</param>
        public void UpdateUser(User user, ChatMember memb)
        {
            User = user;

            ChatMember oldMember = member;
            if (memb != null && oldMember != memb)
            {
                if (oldMember != null) oldMember.PropertyChanged -= MemberPropertyChanged;
                memb.PropertyChanged += MemberPropertyChanged;
            }
            member = memb;
            RecheckChats();
        }

        /// <summary>
        ///     Deconstruct
        /// </summary>
        private void Destroy()
        {
            LeaveMatch();
            if (Challenge != null && !userped)
            {
                BrowserClient tcont;
                if (ClientsBySteamID.TryGetValue(Challenge.ChallengedSID, out tcont))
                    tcont.ChallengeTimer.Stop();
                Challenge.Discard();
            }
            Channels.CollectionChanged -= ChatChannelOnCollectionChanged;
            foreach (var channel in Channels.ToArray())
            {
                lock (channel.Members)
                {
                    channel.Members.Remove(User.steam.steamid);
                }
            }
            Channels.Clear();
            Channels = null;
            ChallengeTimer?.Stop();
            ChallengeTimer?.Close();
            if (member != null)
            {
                member.StateDesc = "Offline";
                member.State = UserState.OFFLINE;
            }
            member = null;
            User = null;
        }
    }
}