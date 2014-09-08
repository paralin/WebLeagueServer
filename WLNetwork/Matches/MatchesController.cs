using System.Collections.ObjectModel;
using System.Dynamic;

namespace WLNetwork.Matches
{
    /// <summary>
    /// Controls matches.
    /// </summary>
    public static class MatchesController
    {
        public static ObservableCollection<MatchGame> Games = new ObservableCollection<MatchGame>();

        /// <summary>
        /// Updates the live game list.
        /// </summary>
        public static ObservableCollection<MatchGameInfo> PublicGames = new ObservableCollection<MatchGameInfo>(); 
    }
}