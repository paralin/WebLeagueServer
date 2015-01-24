using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using log4net;
using WLCommon.Matches.Enums;
using WLNetwork.Matches.Methods;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Matches
{
    /// <summary>
    ///     Controls matches.
    /// </summary>
    public static class MatchesController
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Controllers.Matches Matches = new Controllers.Matches();

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
                Matches.InvokeTo(m => m.User != null, new PublicMatchUpd(newAvailable.ToArray()), PublicMatchUpd.Msg);
            }
            if (args.OldItems != null)
                Matches.InvokeTo(m => m.User != null, new PublicMatchRm(args.OldItems.OfType<MatchGameInfo>().ToArray()),
                    PublicMatchRm.Msg);
        }

        private static void GamesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.NewItems != null)
            {
                IEnumerable<MatchGame> newAvailable =
                    args.NewItems.OfType<MatchGame>().Where(m => m.Info.Status == MatchStatus.Players);
                Matches.InvokeTo(m => m.User != null, new AvailableGameUpd(newAvailable.ToArray()), AvailableGameUpd.Msg);
            }
            if (args.OldItems != null)
                Matches.InvokeTo(m => m.User != null, new AvailableGameRm(args.OldItems.OfType<MatchGame>().ToArray()),
                    AvailableGameRm.Msg);
        }
    }
}