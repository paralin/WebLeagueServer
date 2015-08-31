using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using log4net;
using WLNetwork.Clients;
using WLNetwork.Hubs;
using WLNetwork.Matches.Enums;

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

        static MatchesController()
        {
            Games.CollectionChanged += GamesOnCollectionChanged;
        }

        private static void GamesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.NewItems != null)
            {
                IEnumerable<MatchGame> newAvailable =
                    args.NewItems.OfType<MatchGame>().Where(m => m.Info.Status == MatchStatus.Players);
                var matchGames = newAvailable as MatchGame[] ?? newAvailable.ToArray();
                Hubs.Matches.HubContext.Clients.All.AvailableGameUpdate(matchGames.ToArray());
            }
            if (args.OldItems != null)
            {
                Hubs.Matches.HubContext.Clients.All.AvailableGameRemove(args.OldItems.OfType<MatchGame>().ToArray());
                Admin.HubContext.Clients.All.AvailableGameRemove(args.OldItems.OfType<MatchGame>().ToArray());
            }
        }
    }
}