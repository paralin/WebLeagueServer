using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using log4net;
using WLNetwork.Model;

namespace WLNetwork.Database
{
    public static class HeroCache
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static Dictionary<uint, HeroInfo> Heros;

        static HeroCache()
        {
            Heros = new Dictionary<uint, HeroInfo>();

            log.Debug("Updating hero cache...");
            var heros = Mongo.Heros.FindAllAs<HeroInfo>().ToArray();
            foreach (var hero in heros) Heros[hero.Id] = hero;
            log.Debug("Imported " + Heros.Keys.Count + " heros to the system.");
        }
    }
}
