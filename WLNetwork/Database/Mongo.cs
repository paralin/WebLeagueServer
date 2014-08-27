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
            Client = new MongoClient(Settings.Default.MongoURL+"/"+Settings.Default.MongoDB);
            Server = Client.GetServer();
            Database = Server.GetDatabase(Settings.Default.MongoDB);

            Users = Database.GetCollection("users");
            Servers = Database.GetCollection("servers");
        }
    }
}
