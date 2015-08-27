using System.Reflection;
using log4net;
using MongoDB.Driver;
using WLNetwork.Chat.Methods;
using WLNetwork.Matches;
using WLNetwork.Model;
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

        public static MongoCollection<User> Users;
        public static MongoCollection<Bot> Bots;
        public static MongoCollection<MatchResult> Results;
        public static MongoCollection<ActiveMatch> ActiveMatches;
        public static MongoCollection<HeroInfo> Heros;
        public static MongoCollection<League> Leagues;
        public static MongoCollection<ChatMessage> ChatMessages;

        public static readonly object ExclusiveLock = new object();

        static Mongo()
        {
            if (Client != null)
            {
                log.Error("Tried to create a second instance of Mongo.");
                return;
            }
#if DEBUG
            Client = new MongoClient(Settings.Default.DMongoURL + "/" + Settings.Default.DMongoDB + "?safe=true;maxpoolsize=1000");
#else
            Client = new MongoClient(Env.MONGODB_URL + "?safe=true;maxpoolsize=600");
#endif
            Server = Client.GetServer();
#if DEBUG
            Database = Server.GetDatabase(Settings.Default.DMongoDB);
#else
            var uri = new Uri(Env.MONGODB_URL);
            Database = Server.GetDatabase(uri.AbsolutePath.Replace("/", ""));
#endif

            Users = Database.GetCollection<User>("users");
            Bots = Database.GetCollection<Bot>("bots");
            Results = Database.GetCollection<MatchResult>("matchResults");
            ActiveMatches = Database.GetCollection<ActiveMatch>("activeMatches");
            Heros = Database.GetCollection<HeroInfo>("heros");
            Leagues = Database.GetCollection<League>("leagues");
            ChatMessages = Database.GetCollection<ChatMessage>("chatMessages");
        }
    }
}
