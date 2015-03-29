using System;
using System.Reflection;
using log4net;
using MongoDB.Driver;
using SteamKit2.GC.Internal;
using WLCommon.Model;
using WLNetwork.Matches;
using WLNetwork.Properties;

namespace WLNetwork.Database
{
    public static class Mongo
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static MongoClient Client = null;
        public static MongoServer Server;
        public static MongoDatabase Database;

        public static MongoCollection Users;
        public static MongoCollection BotHosts;
        public static MongoCollection Bots;
        public static MongoCollection Results;

        static Mongo()
        {
            if (Client != null)
            {
                log.Error("Tried to create a second instance of Mongo.");
                return;
            }
#if DEBUG
            Client = new MongoClient(Settings.Default.DMongoURL + "/" + Settings.Default.DMongoDB);
#else
            var mongoUrl = System.Environment.GetEnvironmentVariable("MONGODB_URL");
            if (mongoUrl == null)
            {
                log.Fatal("MONGODB_URL environment variable missing.");
                Environment.Exit(126);
                return;
            }

            Client = new MongoClient(mongoUrl);
#endif
            Server = Client.GetServer();
#if DEBUG
            Database = Server.GetDatabase(Settings.Default.DMongoDB);
#else
            var uri = new Uri(mongoUrl);
            Database = Server.GetDatabase(uri.AbsolutePath.Replace("/", ""));
#endif

            Users = Database.GetCollection<User>("users");
            BotHosts = Database.GetCollection<BotHost>("botHosts");
            Bots = Database.GetCollection<Bot>("bots");
            Results = Database.GetCollection<MatchResult>("matchResults");
        }
    }
}