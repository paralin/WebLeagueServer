using System;
using System.Collections.Concurrent;
using System.Reflection;
using log4net;

namespace WLNetwork.Challenge
{
    /// <summary>
    ///     Lists all challenges.
    /// </summary>
    public class ChallengeController
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        ///     All challenges in the system.
        /// </summary>
        public static ConcurrentDictionary<Guid, Challenge> Challenges = new ConcurrentDictionary<Guid, Challenge>();
    }
}