﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLCommon.Model;
using WLNetwork.Bots;
using WLNetwork.Matches.Methods;
using WLNetwork.Model;
using WLNetwork.Utils;
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
            this.Info = new MatchGameInfo()
            {
                Name = options.Name,
                Public = true,
                Status = MatchStatus.Players,
                MatchType = options.MatchType,
                Owner = owner,
                GameMode = options.GameMode
            };
            this.Players = new ObservableCollection<MatchPlayer>();
            this.Players.CollectionChanged += PlayersOnCollectionChanged;
            this.Id = Guid.NewGuid();
            MatchesController.Games.Add(this);
            //note: Don't add to public games as it's not started yet
            log.Info("MATCH CREATE ["+this.Id+"] [" + options.Name + "] [" + options.GameMode + "] [" + options.MatchType + "]");
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
            this.Info.GameMode = options.GameMode;
            this.Info.MatchType = options.MatchType;
        }

        /// <summary>
        /// Transmit an update for a player.
        /// </summary>
        /// <param name="player"></param>
        public void PlayerUpdate(MatchPlayer player)
        {
            if (!Players.HasPlayer(player)) return;
            Matches.InvokeTo(m => (Info.Status == MatchStatus.Players) ? m.User != null : m.Match == this, new MatchPlayerUpd(this.Id, player), MatchPlayerUpd.Msg);
        }

        public void StartSetup()
        {
            if (Setup != null) return;
            Setup = new MatchSetup(Id, new MatchSetupDetails()
            {
                Id = Id,
                GameMode = this.Info.GameMode,
                Password = RandomPassword.CreateRandomPassword(9),
                Players = this.Players.ToArray()
            });
            BotDB.RegisterSetup(Setup);
        }

        private void PlayersOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if(args.NewItems != null)
                Matches.InvokeTo(m=>(Info.Status== MatchStatus.Players) ? m.User != null : m.Match==this, new MatchPlayerUpd(this.Id, args.NewItems.OfType<MatchPlayer>().ToArray()), MatchPlayerUpd.Msg);
            if(args.OldItems != null)
                Matches.InvokeTo(m => (Info.Status== MatchStatus.Players) ? m.User != null : m.Match==this, new MatchPlayerRm(this.Id, args.OldItems.OfType<MatchPlayer>().ToArray()), MatchPlayerRm.Msg);
        }

        /// <summary>
        /// Delete the game.
        /// </summary>
        public void Destroy()
        {
            foreach (var cont in Matches.Find(m => m.Match == this))
            {
                cont.Match = null;
            }
            if (Setup != null)
            {
                BotDB.SetupQueue.Remove(Setup);
                Setup.Details.Cleanup(true);
            }
            MatchesController.Games.Remove(this);
            MatchesController.PublicGames.Remove(this.Info);
            log.Info("MATCH DESTROY [" + this.Id + "]");
        }

        /// <summary>
        /// ID of the game.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Public info about the match.
        /// </summary>
        public MatchGameInfo Info { get; set; }

        /// <summary>
        /// Players
        /// </summary>
        public ObservableCollection<MatchPlayer> Players { get; set; }

        private MatchSetup _setup;

        public MatchSetup Setup
        {
            get { return _setup; }
            set
            {
                _setup = value;
                TransmitSetupUpdate();
            }
        }

        private void TransmitSetupUpdate()
        {
            if (_setup == null)
            {
                foreach (var cont in Matches.Find(m => m.Match == this))
                {
                    cont.Invoke("clearsetup");
                }
            }
            else
            {
                foreach (var cont in Matches.Find(m => m.Match == this))
                {
                    cont.Invoke(_setup, "setupsnapshot");
                }
            }
        }
    }

    /// <summary>
    /// Match information.
    /// </summary>
    public class MatchGameInfo
    {
        public string Name { get; set; }
        public MatchType MatchType { get; set; }
        public bool Public { get; set; }
        public MatchStatus Status { get; set; }
        public string Owner { get; set; }
        public GameMode GameMode { get; set; }
    }

    public static class MatchGameExt
    {
        /// <summary>
        /// How many players in a team?
        /// </summary>
        /// <param name="plyrs">Players</param>
        /// <param name="team">Team</param>
        /// <returns></returns>
        public static int TeamCount(this ObservableCollection<MatchPlayer> plyrs, MatchTeam team)
        {
            return plyrs.Count(m => m.Team == team);
        }

        /// <summary>
        /// Find the MatchPlayer for a user.
        /// </summary>
        /// <param name="plyrs"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static MatchPlayer ForUser(this ObservableCollection<MatchPlayer> plyrs, User user)
        {
            if (user == null) return null;
            return plyrs.FirstOrDefault(m => m.SID == user.steam.steamid);
        }

        /// <summary>
        /// Is this player in the team?
        /// </summary>
        /// <param name="players"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        public static bool HasPlayer(this ObservableCollection<MatchPlayer> players, MatchPlayer player)
        {
            return players.Contains(player);
        }
    }
}
