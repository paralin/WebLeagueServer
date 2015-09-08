using System.Reflection;
using log4net;
using WLNetwork.Properties;

namespace WLNetwork
{
    public static class Env
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static string MONGODB_URL;

        static Env()
        {
#if DEBUG
            MONGODB_URL = Settings.Default.DMongoDB + "/" + Settings.Default.DMongoDB;
#else
            MONGODB_URL = Environment.GetEnvironmentVariable("MONGODB_URL");
            if (MONGODB_URL == null)
            {
                log.Fatal("MONGODB_URL environment variable missing.");
                Console.WriteLine("MONGODB_URL environment variable missing.");
                Environment.Exit(126);
            }
#endif
        }
    }
}