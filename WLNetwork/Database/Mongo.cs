using MongoDB.Driver;
using WLCommon.Model;
using WLNetwork.Matches;
using WLNetwork.Model;
using WLNetwork.Properties;

namespace WLNetwork.Database
{
    public static class Mongo
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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
            Client = new MongoClient(Settings.Default.DMongoURL+"/"+Settings.Default.DMongoDB);
#else
            Client = new MongoClient(Settings.Default.MongoURL+"/"+Settings.Default.MongoDB);
#endif
            Server = Client.GetServer();
#if DEBUG
            Database = Server.GetDatabase(Settings.Default.DMongoDB);
#else
            Database = Server.GetDatabase(Settings.Default.MongoDB);
#endif

            Users = Database.GetCollection<User>("users");
            BotHosts = Database.GetCollection<BotHost>("botHosts");
            Bots = Database.GetCollection<Bot>("bots");
            Results = Database.GetCollection<MatchResult>("matchResults");
        }
    }
}
