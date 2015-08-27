using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using log4net;
using WLNetwork.Clients;
using WLNetwork.Hubs;
using WLNetwork.Matches.Enums;
using WLNetwork.Matches.Methods;

namespace WLNetwork.Matches
{
    /// <summary>
    ///     Controls matches.
    /// </summary>
    public static class MatchesController
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        ///     All games in the system.
        /// </summary>
        public static ObservableCollection<MatchGame> Games = new ObservableCollection<MatchGame>();

        /// <summary>
        ///     Updates the live game list.
        /// </summary>
        public static ObservableCollection<MatchGameInfo> PublicGames = new ObservableCollection<MatchGameInfo>();

        static MatchesController()
        {
            Games.CollectionChanged += GamesOnCollectionChanged;
            PublicGames.CollectionChanged += PublicGamesOnCollectionChanged;
        }

        private static void PublicGamesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.NewItems != null)
            {
                IEnumerable<MatchGameInfo> newAvailable = args.NewItems.OfType<MatchGameInfo>();
                Hubs.Matches.HubContext.Clients.All.PublicMatchUpdate(newAvailable.ToArray());
            }
            if (args.OldItems != null)
                Hubs.Matches.HubContext.Clients.All.PublicMatchRemove(args.OldItems.OfType<MatchGameInfo>().ToArray());
        }

        private static void GamesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.NewItems != null)
            {
                IEnumerable<MatchGame> newAvailable =
                    args.NewItems.OfType<MatchGame>().Where(m => m.Info.Status == MatchStatus.Players);
                var matchGames = newAvailable as MatchGame[] ?? newAvailable.ToArray();
                Hubs.Matches.HubContext.Clients.All.AvailableGameUpdate(matchGames.ToArray());
                foreach (var cli in BrowserClient.Clients.Values.Where(m=>m.User != null && m.User.authItems.Contains("admin")).SelectMany(client => client.MatchClients.Values))
                    cli.AvailableGameUpdate(matchGames.ToArray());
            }
            if (args.OldItems != null)
            {
                Hubs.Matches.HubContext.Clients.All.AvailableGameRemove(args.OldItems.OfType<MatchGame>().ToArray());
                Hubs.Admin.HubContext.Clients.All.AvailableGameRemove(args.OldItems.OfType<MatchGame>().ToArray());
            }
        }
    }
}