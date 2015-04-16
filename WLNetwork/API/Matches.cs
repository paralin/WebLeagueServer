using System;
using System.Linq;
using System.Reflection;
using log4net;
using Nancy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;

namespace WLNetwork.API
{
    public class Matches : NancyModule
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public Matches()
        {
            Get["/api/matches"] = para => ListMatches();
        }

        private string ListMatches()
        {
            try
            {
                JArray arr = new JArray();
                foreach (var match in MatchesController.Games.Where(m => m.Setup != null && m.Setup.Details != null))
                {
                    JArray plyrs = new JArray();
                    foreach (
                        var plyr in match.Players.Where(m => m.Team == MatchTeam.Dire || m.Team == MatchTeam.Radiant))
                    {
                        plyrs.Add(JObject.FromObject(new
                        {
                            Id = plyr.SID,
                            Name = plyr.Name,
                            Rating = plyr.Rating,
                            Hero = plyr.Hero,
                            Team = plyr.Team
                        }));
                    }
                    arr.Add(JObject.FromObject(new
                    {
                        Id = match.Id,
                        Players = plyrs,
                        MatchStatus = match.Info.Status,
                        State = match.Setup.Details.State,
                        StartTime = match.Setup.Details.GameStartTime,
                        SpectatorCount = match.Setup.Details.SpectatorCount,
                        MatchId = match.Setup.Details.MatchId
                    }));
                }
                return arr.ToString();
            }
            catch (Exception ex)
            {
                log.Error("Error generating matches list!", ex);
                return "[]";
            }
        }
    }
}
