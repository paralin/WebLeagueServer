using MongoDB.Driver;
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
        public static MongoCollection Servers;

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

            Users = Database.GetCollection("users");
            Servers = Database.GetCollection("servers");
        }
    }
}
