using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using log4net;
using WLNetwork.Chat;
using WLNetwork.Clients;

namespace WLNetwork.Hubs
{
    /// <summary>
    ///     Chat controller.
    /// </summary>
    public class Chat : WebLeagueHub<Chat>
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
        /// Complete member list snapshot
        /// </summary>
        public ChatMember[] MemberListSnapshot()
        {
            return MemberDB.Members.Values.ToArray();
        }

        /// <summary>
        /// Sends a message to a channel.
        /// </summary>
        /// <param name="channel">Channel</param>
        /// <param name="text">Message text</param>
        public void SendMessage(string channel, string text)
        {
            var cli = Client;
            if (cli?.User == null || string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(text)) {
              log.WarnFormat("Ignored chat message {0}", text);
              return;
            }
            ChatChannel chan = Client.Channels.FirstOrDefault(m => m.Id.ToString() == channel);
            if (chan == null) return;
            log.DebugFormat("[{0}] {1}: \"{2}\"", chan.Name, Client.User.profile.name, text);
            chan.TransmitMessage(Client.User.steam.steamid, text);
        }

        /* Join or create a chat not here - not interactive right now. */
    }
}
