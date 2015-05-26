using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using KellermanSoftware.CompareNetObjects;
using WLNetwork.Annotations;
using WLNetwork.Chat.Enums;
using WLNetwork.Model;

namespace WLNetwork.Chat
{
    /// <summary>
    ///    Global user state 
    /// </summary>
    public class ChatMember : INotifyPropertyChanged
    {
        private bool _disablePropertyChanged = false;

        /// <summary>
        ///     Create a chat member.
        /// </summary>
        /// <param name="user">user</param>
        public ChatMember(User user)
        {
            _disablePropertyChanged = true;

            UpdateFromUser(user);

            // On default
            State = UserState.OFFLINE;
            StateDesc = "Offline";

            _disablePropertyChanged = false;
        }

        /// <summary>
        /// Update from a user and return if changed
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public void UpdateFromUser(User user)
        {
            CompareLogic logic = new CompareLogic();

            ID = user.steam.steamid;
            SteamID = user.steam.steamid;
            UID = user.Id;
            Name = user.profile.name;
            Avatar = user.steam.avatarfull;

            if(Leagues == null || !logic.Compare(Leagues, user.leagues).AreEqual)
                Leagues = user.leagues;
            if (!logic.Compare(LeagueProfiles, user.profile.leagues).AreEqual)
                LeagueProfiles = user.profile.leagues;

            if (user.authItems.Contains("admin"))
                MemberType = ChatMemberType.Admin;
            else if (user.authItems.Contains("vouch"))
                MemberType = ChatMemberType.Moderator;
            else if (user.authItems.Contains("spectateOnly"))
                MemberType = ChatMemberType.Spectator;
            else
                MemberType = ChatMemberType.Normal;
        }

        private string _id;
        private string _steamId;
        private string _uid;
        private string _name;
        private string _avatar;
        private uint _rating;
        private uint _winStreak;
        private UserState _state;
        private string _stateDesc;
        private ChatMemberType _memberType;
        private string[] _leagues;
        private Dictionary<string, LeagueProfile> _leagueProfiles;

        /// <summary>
        /// ID, basically steam id
        /// </summary>
        public string ID
        {
            get { return _id; }
            set
            {
                if (value == _id) return;
                _id = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Steam ID
        /// </summary>
        public string SteamID
        {
            get { return _steamId; }
            set
            {
                if (value == _steamId) return;
                _steamId = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     User ID
        /// </summary>
        public string UID
        {
            get { return _uid; }
            set
            {
                if (value == _uid) return;
                _uid = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Name of the member.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set
            {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Avatar image URL.
        /// </summary>
        public string Avatar
        {
            get { return _avatar; }
            set
            {
                if (value == _avatar) return;
                _avatar = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Current user state
        /// </summary>
        public UserState State
        {
            get { return _state; }
            set
            {
                if (value == _state) return;
                _state = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Current state description
        /// </summary>
        public string StateDesc
        {
            get { return _stateDesc; }
            set
            {
                if (value == _stateDesc) return;
                _stateDesc = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Member type for visibility in the player list
        /// </summary>
        public ChatMemberType MemberType
        {
            get { return _memberType; }
            set
            {
                if (value == _memberType) return;
                _memberType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Leagues the user is in
        /// </summary>
        public string[] Leagues
        {
            get { return _leagues; }
            set
            {
                if (Equals(value, _leagues)) return;
                _leagues = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// League profiles
        /// </summary>
        public Dictionary<string, LeagueProfile> LeagueProfiles
        {
            get { return _leagueProfiles; }
            set
            {
                if (Equals(value, _leagueProfiles)) return;
                _leagueProfiles = value;
                OnPropertyChanged();
            }
        }

        public enum ChatMemberType : int
        {
            Spectator = -1,
            Normal = 0,
            Moderator = 1,
            Admin = 2
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (_disablePropertyChanged) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}