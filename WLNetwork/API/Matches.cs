using System.Linq;
using Nancy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;

namespace WLNetwork.API
{
    public class Matches : NancyModule
    {
        public Matches()
        {
            Get["/api/matches"] = para => ListMatches();
        }

        private string ListMatches()
        {
            JArray arr = new JArray();
            foreach (var match in MatchesController.Games.Where(m => m.Info.Status == MatchStatus.Play))
            {
                JArray plyrs = new JArray();
                foreach (var plyr in match.Players.Where(m => m.Team == MatchTeam.Dire || m.Team == MatchTeam.Radiant))
                {
                    plyrs.Add(new
                    {
                        Id = plyr.SID,
                        Name = plyr.Name,
                        Rating = plyr.Rating,
                        Hero = plyr.Hero,
                        Team = plyr.Team
                    });
                }
                arr.Add(new
                {
                    Id = match.Id,
                    Players = plyrs,
                    State = match.Setup.Details.State,
                    StartTime = match.Setup.Details.GameStartTime,
                    SpectatorCount = match.Setup.Details.SpectatorCount
                });
            }
            return arr.ToString(Formatting.Indented);
        }
    }
}
