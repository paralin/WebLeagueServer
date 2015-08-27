using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using MongoDB.Driver.Linq;
using WLNetwork.Clients;
using WLNetwork.Model;

namespace WLNetwork.Hubs
{
    public class WebLeagueHub<T> : Hub
        where T : IHub
    {
        public BrowserClient Client
        {
            get
            {
                BrowserClient cli;
                return BrowserClient.Clients.TryGetValue(this.Context.ConnectionId, out cli) ? cli : null;
            }
        }

        /// <summary>
        /// Hub context.
        /// </summary>
        public static IHubContext HubContext => GlobalHost.ConnectionManager.GetHubContext<T>();
    }
}
