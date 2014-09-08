using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.Design;
using System.Dynamic;
using System.Linq;
using MongoDB.Driver.Linq;
using WLNetwork.Matches.Enums;
using WLNetwork.Matches.Methods;
using WLNetwork.Model;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Matches
{
    /// <summary>
    /// A instance of a match.
    /// </summary>
    public class MatchGame
    {
        private static readonly Controllers.Matches Matches = new Controllers.Matches();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Create a new game with options
        /// </summary>
        /// <param name="options"></param>
        public MatchGame(string owner, MatchCreateOptions options)
        {
            this.GameType = options.GameType;
            this.Info = new MatchGameInfo()
            {
                Name = options.Name,
                Public = true,
                Status = MatchStatus.Players,
                Mode = options.MatchType,
                Owner = owner
            };
            this.Players = new ObservableCollection<MatchPlayer>();
            this.Players.CollectionChanged += PlayersOnCollectionChanged;
            this.Id = Guid.NewGuid();
            this.SetupStatus = MatchSetupStatus.Queue;
            MatchesController.Games.Add(this);
            //note: Don't add to public games as it's not started yet
            log.Info("MATCH CREATE ["+this.Id+"] [" + options.Name + "] [" + options.GameType + "] [" + options.MatchType + "]");
        }

        /// <summary>
        /// Update with some options
        /// </summary>
        /// <param name="options"></param>
        public void Update(MatchCreateOptions options)
        {
            bool dirty = false;
            if (!string.IsNullOrEmpty(options.Name) && options.Name != Info.Name)
            {
                Info.Name = options.Name;
                dirty = true;
            }
            this.GameType = options.GameType;
            this.Info.Mode = options.MatchType;
        }

        private void PlayersOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if(args.NewItems != null)
                Matches.InvokeTo(m=>(Info.Status== MatchStatus.Players) ? m.User != null : m.Match==this, new MatchPlayerUpd(this.Id, args.NewItems.OfType<MatchPlayer>().ToArray()), MatchPlayerUpd.Msg);
            if(args.OldItems != null)
                Matches.InvokeTo(m => (Info.Status== MatchStatus.Players) ? m.User != null : m.Match==this, new MatchPlayerRm(this.Id, args.OldItems.OfType<MatchPlayer>().ToArray()), MatchPlayerRm.Msg);
        }

        public void Destroy()
        {
            MatchesController.Games.Remove(this);
            MatchesController.PublicGames.Remove(this.Info);
            foreach (var cont in Matches.Find(m => m.Match == this))
            {
                cont.Match = null;
            }
            log.Info("MATCH DESTROY [" + this.Id + "]");
        }

        public Guid Id { get; set; }

        /// <summary>
        /// Public info about the match.
        /// </summary>
        public MatchGameInfo Info { get; set; }

        /// <summary>
        /// Game type.
        /// </summary>
        public GameType GameType { get; set; }

        /// <summary>
        /// Players
        /// </summary>
        public ObservableCollection<MatchPlayer> Players { get; set; }

        /// <summary>
        /// Status of the bot assigned to setup the match
        /// </summary>
        public MatchSetupStatus SetupStatus { get; set; }
    }

    /// <summary>
    /// Match information.
    /// </summary>
    public class MatchGameInfo
    {
        public string Name { get; set; }
        public MatchType Mode { get; set; }
        public bool Public { get; set; }
        public MatchStatus Status { get; set; }
        public string Owner { get; set; }
    }
}
