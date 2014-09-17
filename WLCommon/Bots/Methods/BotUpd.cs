using WLCommon.Model;

namespace WLCommon.Bots.Methods
{
    /// <summary>
    /// Add or update some bots.
    /// </summary>
    public class BotUpd
    {
        /// <summary>
        /// Bots to add/update
        /// </summary>
        public Bot[] bots { get; set; }

        /// <summary>
        /// Add/update some bots.
        /// </summary>
        /// <param name="members"></param>
        public BotUpd(params Bot[] bots)
        {
            this.bots = bots;
        }
    }

    /// <summary>
    /// Remove some bots.
    /// </summary>
    public class BotRm
    {
        /// <summary>
        /// IDS of the bots.
        /// </summary>
        public string[] ids { get; set; }

        /// <summary>
        /// Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public BotRm(params Bot[] bots)
        {
            this.ids = new string[bots.Length];
            int i = 0;
            foreach (var bot in bots)
            {
                this.ids[i] = bot.Id;
                i++;
            }
        }
    }
}
