using System;

namespace WLNetwork.Matches.Methods
{
    public class MatchVoteArgs
    {
        public bool Vote { get; set; }
    }

    public class KillMatchArgs
    {
        public Guid Id { get; set; }
    }

    public class ResultMatchArgs
    {
        public Guid Id { get; set; }
        public EMatchResult Result { get; set; }
    }
}