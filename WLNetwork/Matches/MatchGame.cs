using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLCommon.Model;
using WLNetwork.Bots;
using WLNetwork.Controllers;
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
        private static readonly Controllers.DotaBot Bot = new Controllers.DotaBot();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Create a new game with options
        /// </summary>
        /// <param name="options"></param>
        public MatchGame(string owner, MatchCreateOptions options)
        {
            this.Id = Guid.NewGuid();
            this.Info = new MatchGameInfo()
            {
                Id = Id,
                Name = options.Name,
                Public = true,
                Status = MatchStatus.Players,
                MatchType = options.MatchType,
                Owner = owner,
                GameMode = options.GameMode
            };
            this.Players = new ObservableCollection<MatchPlayer>();
            this.Players.CollectionChanged += PlayersOnCollectionChanged;
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
        public void PlayerUpdate()
        {
            RebalanceTeams();
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
            this.Info.Status = MatchStatus.Lobby;
            this.Info = this.Info;
            BotDB.RegisterSetup(Setup);
        }

        private void PlayersOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            RebalanceTeams();
        }

        private bool _balancing = false;

        /// <summary>
        /// Balance teams
        /// </summary>
        public void RebalanceTeams()
        {
            if (_balancing) return;
            _balancing = true;
            //Simple algorithm to later be replaced
            int direCount = 0;
            int radiCount = 0;
            foreach (var plyr in Players)
            {
                if (plyr.Team == MatchTeam.Spectate) continue;
                if (direCount < radiCount && direCount < 5)
                {
                    plyr.Team = MatchTeam.Dire;
                    direCount++;
                }
                else
                {
                    plyr.Team = MatchTeam.Radiant;
                    radiCount++;
                }
            }
            Players = Players;
            _balancing = false;
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

        private MatchGameInfo _info;

        /// <summary>
        /// Public info about the match.
        /// </summary>
        public MatchGameInfo Info
        {
            get { return _info; }
            set
            {
                _info = value;
                TransmitInfoUpdate();
            }
        }

        private ObservableCollection<MatchPlayer> _players; 

        /// <summary>
        /// Players
        /// </summary>
        public ObservableCollection<MatchPlayer> Players {
            get { return _players; }
            set
            {
                _players = value;
                Matches.InvokeTo(m=>m.User != null && ((this.Info.Public&&this.Info.Status == MatchStatus.Players)||(m.Match == this)), new MatchPlayersSnapshot(this), MatchPlayersSnapshot.Msg);
            } 
        }

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

        private void TransmitInfoUpdate()
        {
            if(_info != null)
            {
                foreach (var cont in Matches.Find())//Matches.Find(m => m.Match == this))
                {
                    cont.Invoke(_info, "infosnapshot");
                }
            }
        }

        /// <summary>
        /// Start the match in-game
        /// </summary>
        public void Finalize()
        {
            var controller = Bot.Find(m => m.PersistentId == Setup.ControllerGuid).FirstOrDefault();
            if (controller != null)
            {
                controller.Finalize(this);
            }
            Destroy();
        }
    }

    /// <summary>
    /// Match information.
    /// </summary>
    public class MatchGameInfo
    {
        public Guid Id { get; set; }
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
