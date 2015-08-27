using System;
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
using WLNetwork.Chat;
using WLNetwork.Chat.Enums;
using WLNetwork.Database;
using WLNetwork.Leagues;
using WLNetwork.Matches;
using WLNetwork.Model;
using WLNetwork.Properties;

namespace WLNetwork.Clients
{
    /// <summary>
    /// Instance of a browser client.
    /// </summary>
    public class BrowserClient
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Dictionary of all clients.
        /// </summary>
        public static ConcurrentDictionary<string, BrowserClient> Clients = new ConcurrentDictionary<string, BrowserClient>();

        /// <summary>
        /// Clients by Steam ID
        /// </summary>
        public static ConcurrentDictionary<string, BrowserClient> ClientsBySteamID = new ConcurrentDictionary<string, BrowserClient>();

        /// <summary>
        /// Current channels list.
        /// </summary>
        public ObservableCollection<ChatChannel> Channels = new ObservableCollection<ChatChannel>();

        /// <summary>
        /// Current ChatMember match.
        /// </summary>
        private ChatMember member;

        private string ConnectionID;
        private User _user;
        private bool userped = false;

        /// <summary>
        /// All currently connected chat clients.
        /// </summary>
        public ConcurrentDictionary<string, dynamic> ChatClients = new ConcurrentDictionary<string, dynamic>();

        /// <summary>
        /// All currently connected match clients.
        /// </summary>
        public ConcurrentDictionary<string, dynamic> MatchClients = new ConcurrentDictionary<string, dynamic>();

        /// <summary>
        /// All currently connected admin clients.
        /// </summary>
        public ConcurrentDictionary<string, dynamic> AdminClients = new ConcurrentDictionary<string, dynamic>();

        /// <summary>
        /// Client type.
        /// </summary>
        public enum ClientType
        {
            CHAT,
            MATCH,
            ADMIN
        }

        /// <summary>
        /// Gets the local user.
        /// </summary>
        public User User => _user;

        /// <summary>
        ///     The active match the player is in.
        /// </summary>
        public MatchGame Match
        {
            get { return activeMatch; }
            internal set
            {
                foreach (var client in MatchClients.Values) client.MatchSnapshot(value);
                activeMatch = value;
            }
        }

        /// <summary>
        ///     The active match the player is in.
        /// </summary>
        public MatchResult Result
        {
            get { return activeResult; }
            internal set
            {
                foreach (var client in MatchClients.Values) client.ResultSnapshot(value);
                activeResult = value;
            }
        }

        /// <summary>
        ///     The active challenge the player is in.
        /// </summary>
        public Challenge Challenge
        {
            get { return activeChallenge; }
            internal set
            {
                if (value != null)
                    foreach (var client in MatchClients.Values) client.ChallengeSnapshot(value);
                else
                    foreach (var client in MatchClients.Values) client.ClearChallenge();
                activeChallenge = value;
            }
        }

        /// <summary>
        /// Timer until the challenge times out.
        /// </summary>
        public readonly Timer ChallengeTimer;

        private Challenge activeChallenge;
        private MatchGame activeMatch;
        private MatchResult activeResult;

        /// <summary>
        /// Build a browserclient from a CallerContext
        /// </summary>
        /// <param name="ctx"></param>
        public static void HandleConnection(HubCallerContext ctx, dynamic client, ClientType clientType)
        {
            if (Clients.ContainsKey(ctx.ConnectionId)) return;
            log.Debug("CONNECTED [" + ctx.ConnectionId + "]");

            // Check the authentication
            try
            {
                string token = ctx.QueryString["token"];
                string jsonPayload = JsonWebToken.Decode(token, Settings.Default.AuthSecret);
                var atoken = JObject.Parse(jsonPayload).ToObject<AuthToken>();
                try
                {
                    var user =
                        Mongo.Users.FindOneAs<User>(Query.And(Query.EQ("_id", atoken._id),
                            Query.EQ("steam.steamid", atoken.steamid)));
                    if (user != null)
                    {
                        if (user.vouch != null)
                        {
                            log.Debug("AUTHED [" + ctx.ConnectionId + "] => [" + user.steam.steamid + "]");

                            // Check for any existing client to use
                            BrowserClient exist = null;
                            if (!ClientsBySteamID.TryGetValue(user.steam.steamid, out exist))
                                exist = ClientsBySteamID[user.steam.steamid] = new BrowserClient(user);

                            // Add our new connection ID 
                            Clients[ctx.ConnectionId] = exist;

                            // Add the client to the dict.
                            switch (clientType)
                            {
                                case ClientType.ADMIN:
                                    exist.AdminClients[ctx.ConnectionId] = client;
                                    break;
                                case ClientType.MATCH:
                                    exist.MatchClients[ctx.ConnectionId] = client;
                                    break;
                                case ClientType.CHAT:
                                    exist.ChatClients[ctx.ConnectionId] = client;
                                    break;
                            }

                            // Resend everything.
                            exist.SendFullSnapshot();
                            return;
                        }
                        log.Warn("Unvouched user tried to connect.");
                    }
                    else
                    {
                        log.Warn("Authentication token valid but no user for " + jsonPayload);
                    }
                }
                catch (Exception ex)
                {
                    log.Warn("Issue authenticating decrypted token " + atoken._id, ex);
                }
            }
            catch (Exception ex)
            {
                log.Error("Unable to authenticate user", ex);
            }
        }

        /// <summary>
        /// Called when the client disconnects.
        /// </summary>
        /// <param name="ctx"></param>
        public static void HandleDisconnected(HubCallerContext ctx, ClientType hubType)
        {
            log.Debug("DISCONNECTED [" + ctx.ConnectionId + "]");
            BrowserClient cli;
            if (!Clients.TryRemove(ctx.ConnectionId, out cli)) return;
            ConcurrentDictionary<string, dynamic> clientDictionary = null;
            switch (hubType)
            {
                case ClientType.ADMIN:
                    clientDictionary = cli.AdminClients;
                    break;
                case ClientType.CHAT:
                    clientDictionary = cli.ChatClients;
                    break;
                case ClientType.MATCH:
                    clientDictionary = cli.MatchClients;
                    break;
            }
            dynamic outp;
            clientDictionary?.TryRemove(ctx.ConnectionId, out outp);
        }

        /// <summary>
        /// Create a new browserclient
        /// </summary>
        /// <param name="user">User</param>
        /// <param name="ctx">Caller context</param>
        private BrowserClient(User user)
        {
            ChallengeTimer = new Timer(2000);
            ChallengeTimer.Elapsed += (sender, args) =>
            {
                if (Challenge != null)
                {
                    BrowserClient tcont;
                    if (ClientsBySteamID.TryGetValue(Challenge.ChallengerSID, out tcont))
                    {
                        tcont.Challenge = null;
                        tcont.ChallengeTimer?.Stop();
                    }
                    Challenge = null;
                }
                ChallengeTimer.Stop();
            };
            _user = user;
            Channels.CollectionChanged += ChatChannelOnCollectionChanged;
            ReloadUser();
            if (User != null)
            {
                //See if we're in any matches already
                MatchGame game = MatchesController.Games.FirstOrDefault(m => m.Players.Any(x => x.SID == User.steam.steamid));
                if (game != null)
                    Match = game;
            }
            if (member == null) return;
            member.State = UserState.ONLINE;
            member.StateDesc = "Online";
        }

        /// <summary>
        /// Re-transmits everything.
        /// </summary>
        public void SendFullSnapshot()
        {
            Match = Match;
            Result = Result;
            Challenge = Challenge;
            foreach (var client in ChatClients.Values)
                client.ChannelUpdate(Channels.ToArray());
        }

        /// <summary>
        /// Called when a chat channel is added/removed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChatChannelOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var client in ChatClients.Values)
            {
                if (e.NewItems != null)
                    client.ChannelUpdate(e.NewItems.OfType<ChatChannel>().ToArray());
                if (e.OldItems != null)
                    client.ChannelRemove(e.OldItems.OfType<ChatChannel>().ToArray().Select(m => m.Id));
            }
        }

        /// <summary>
        /// Lookup the ChatMember.
        /// </summary>
        public void ReloadUser()
        {
            if (User == null) return;
            ChatMember oldMember = member;

            ChatMember memb = null;
            if (MemberDB.Members.TryGetValue(User.steam.steamid, out memb) && memb != null)
            {
                member = memb;
            }
            else
            {
                MemberDB.UpdateDB();
                if (!MemberDB.Members.TryGetValue(User.steam.steamid, out memb) || memb == null)
                {
                    log.Warn("Unable to find ChatMember for user " + User.profile.name + "!");
                }
            }
            if (memb != null && oldMember != memb)
            {
                if (oldMember != null) oldMember.PropertyChanged -= MemberPropertyChanged;
                memb.PropertyChanged += MemberPropertyChanged;
            }
            RecheckChats();
        }

        /// <summary>
        /// Recheck chats
        /// </summary>
        private void RecheckChats()
        {
            if (member?.Leagues == null) return;
            var leagueChans = Channels.Where(m => m.ChannelType == ChannelType.League);
            foreach (var league in leagueChans.ToArray().Where(league => !member.Leagues.Contains(league.Name)))
            {
                lock (league.Members)
                    league.Members.Remove(User.steam.steamid);
                lock (Channels)
                    Channels.Remove(league);
            }
            foreach (var league in member.Leagues.Where(league => Channels.All(m => m.Name != league)))
            {
                lock (Channels)
                {
                    ChatChannel chan = ChatChannel.JoinOrCreate(league, member, ChannelType.League);
                    if (chan != null)
                        Channels.Add(chan);
                    League leaguel;
                    if (LeagueDB.Leagues.TryGetValue(league, out leaguel) && leaguel.MotdMessages != null)
                        foreach (var msg in leaguel.MotdMessages)
                            ChatChannel.SystemMessage(league, "MOTD: " + msg, User.steam.steamid);
                }
            }
        }

        /// <summary>
        /// Watch changes on the member.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="propertyChangedEventArgs"></param>
        private void MemberPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            RecheckChats();
        }

        /// <summary>
        /// Try to leave the current match.
        /// </summary>
        public void LeaveMatch()
        {

        }

        /// <summary>
        /// Force set the user.
        /// </summary>
        /// <param name="user">user object</param>
        public void UpdateUser(User user)
        {
            _user = user;
        }

        /// <summary>
        /// Deconstruct
        /// </summary>
        ~BrowserClient()
        {
            LeaveMatch();
            if (Challenge != null && !userped)
            {
                BrowserClient tcont;
                if (ClientsBySteamID.TryGetValue(Challenge.ChallengedSID, out tcont))
                {
                    tcont.Challenge = null;
                    tcont.ChallengeTimer.Stop();
                }
                Challenge = null;
            }
            Channels.CollectionChanged -= ChatChannelOnCollectionChanged;
            foreach (var channel in Channels.ToArray())
                channel.Members.Remove(User.steam.steamid);
            Channels.Clear();
            Channels = null;
            ConnectionID = null;
            ChallengeTimer?.Stop();
            ChallengeTimer?.Close();
            BrowserClient tmpcli;
            if (User != null)
                ClientsBySteamID.TryRemove(User.steam.steamid, out tmpcli);
            if (member != null)
            {
                member.StateDesc = "Offline";
                member.State = UserState.OFFLINE;
            }
            member = null;
            _user = null;
        }
    }
}