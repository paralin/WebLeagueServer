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
            TEAMSPEAK_URL = Settings.Default.TEAMSPEAK_URL;
            TEAMSPEAK_AUTH = Settings.Default.TEAMSPEAK_AUTH;
            TEAMSPEAK_PORT = Settings.Default.TEAMSPEAK_PORT;
#else
            MONGODB_URL = System.Environment.GetEnvironmentVariable("MONGODB_URL");
            if (MONGODB_URL == null)
            {
                log.Fatal("MONGODB_URL environment variable missing.");
                Environment.Exit(126);
            }

            TEAMSPEAK_URL = System.Environment.GetEnvironmentVariable("TEAMSPEAK_URL");
            if (TEAMSPEAK_URL == null)
            {
                ENABLE_TEAMSPEAK = false;
            }

            if (ENABLE_TEAMSPEAK)
            {
                TEAMSPEAK_AUTH = System.Environment.GetEnvironmentVariable("TEAMSPEAK_AUTH");
                if (TEAMSPEAK_AUTH == null)
                {
                    log.Fatal("TEAMSPEAK_AUTH environment variable missing.");
                    Environment.Exit(126);
                }

                var tsport = System.Environment.GetEnvironmentVariable("TEAMSPEAK_PORT");
                if (tsport != null)
                {
                    TEAMSPEAK_PORT = int.Parse(tsport);
                    log.Debug("Using teamspeak port "+TEAMSPEAK_PORT);
                }
            }
#endif
        }

        public static string MONGODB_URL;

        public static bool   ENABLE_TEAMSPEAK = true;
        public static string TEAMSPEAK_URL;
        public static string TEAMSPEAK_AUTH;

        public static int    TEAMSPEAK_PORT = 9987;
    }
}
