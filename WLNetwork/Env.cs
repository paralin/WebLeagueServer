using System;
using System.Reflection;
using log4net;
using WLNetwork.Properties;

namespace WLNetwork
{
    public static class Env
    {
        private static readonly ILog log =
           LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static Env()
        {
#if DEBUG
            MONGODB_URL = Settings.Default.DMongoDB + "/" + Settings.Default.DMongoDB;
#else
            MONGODB_URL = System.Environment.GetEnvironmentVariable("MONGODB_URL");
            if (MONGODB_URL == null)
            {
                log.Fatal("MONGODB_URL environment variable missing.");
                Environment.Exit(126);
            }

            var tid = System.Environment.GetEnvironmentVariable("TICKET_ID");
            if (!int.TryParse(tid, out TICKET_ID)){
                TICKET_ID = 0;
                log.Fatal("TICKET_ID environment variable missing.");
            }
#endif

#if DEBUG
            if (TICKET_ID == 0)
            {
                TICKET_ID = Settings.Default.LeagueTicketID;
            }
#endif
        }

        public static string MONGODB_URL;
        public static int TICKET_ID;
    }
}
