namespace WLNetwork.Bots.Data
{
    public class PlayerReadyArgs
    {
        public Player[] Players { get; set; }

        public class Player
        {
            public string SteamID { get; set; }
            public bool IsReady { get; set; }
            public bool WrongTeam { get; set; }
        }
    }
}