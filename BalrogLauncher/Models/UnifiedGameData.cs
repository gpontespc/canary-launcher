using System;
using System.Collections.Generic;

namespace BalrogLauncher.Models
{
    public class UnifiedGameData
    {
        public List<NewsItem> News { get; set; } = new();
        public List<CountdownEvent> Countdowns { get; set; } = new();
        public BoostedCreature? BoostedCreature { get; set; }
        public BoostedCreature? BoostedBoss { get; set; }
        public DateTime FetchTime { get; set; }
        public bool IsFromCache { get; set; }
    }
}
