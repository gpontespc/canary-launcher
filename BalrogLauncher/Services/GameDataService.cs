using BalrogLauncher.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BalrogLauncher.Services
{
    public class GameDataService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        //TODO: insert base API URL
        private const string BASE_URL = "https://example.com";
        private const string UNIFIED_API_URL = "https://example.com/api";
        private static DateTime lastFetchTime = DateTime.MinValue;
        private static UnifiedGameData? cachedData = null;
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(5);

        static GameDataService()
        {
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "BalrogLauncher/1.0");
        }

        public async Task<UnifiedGameData> FetchAllDataAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && DateTime.Now - lastFetchTime < CACHE_DURATION && cachedData != null)
            {
                cachedData.IsFromCache = true;
                return cachedData;
            }

            try
            {
                var unifiedData = await FetchUnifiedDataAsync();
                cachedData = unifiedData;
                lastFetchTime = DateTime.Now;
                cachedData.IsFromCache = false;
                return cachedData;
            }
            catch
            {
                if (cachedData != null)
                {
                    cachedData.IsFromCache = true;
                    return cachedData;
                }
                return new UnifiedGameData();
            }
        }

        private async Task<UnifiedGameData> FetchUnifiedDataAsync()
        {
            var mainPageTask = httpClient.GetStringAsync(UNIFIED_API_URL);
            var newsArchiveTask = httpClient.GetStringAsync($"{BASE_URL}/news");

            await Task.WhenAll(mainPageTask, newsArchiveTask);

            var mainPageHtml = await mainPageTask;
            var newsArchiveHtml = await newsArchiveTask;

            var unifiedData = new UnifiedGameData
            {
                FetchTime = DateTime.Now
            };

            var (creature, boss) = ExtractBoostedCreaturesFromHtml(mainPageHtml);
            unifiedData.BoostedCreature = creature;
            unifiedData.BoostedBoss = boss;
            unifiedData.Countdowns = ExtractCountdownsFromHtml(mainPageHtml);
            unifiedData.News = await ExtractNewsFromHtml(newsArchiveHtml, 2);

            return unifiedData;
        }

        private static (BoostedCreature creature, BoostedCreature boss) ExtractBoostedCreaturesFromHtml(string html)
        {
            var creature = ExtractBoostedCreature(html);
            var boss = ExtractBoostedBoss(html);
            return (creature, boss);
        }

        private static BoostedCreature ExtractBoostedCreature(string html)
        {
            try
            {
                var creatureMatch = Regex.Match(html, @"boosted-creature:(.*?)<", RegexOptions.IgnoreCase);
                if (creatureMatch.Success)
                {
                    return new BoostedCreature { Name = creatureMatch.Groups[1].Value.Trim(), Type = "Creature" };
                }
            }
            catch { }
            return new BoostedCreature { Name = "Loading...", Type = "Creature" };
        }

        private static BoostedCreature ExtractBoostedBoss(string html)
        {
            try
            {
                var bossMatch = Regex.Match(html, @"boosted-boss:(.*?)<", RegexOptions.IgnoreCase);
                if (bossMatch.Success)
                {
                    return new BoostedCreature { Name = bossMatch.Groups[1].Value.Trim(), Type = "Boss" };
                }
            }
            catch { }
            return new BoostedCreature { Name = "Loading...", Type = "Boss" };
        }

        private static List<CountdownEvent> ExtractCountdownsFromHtml(string html)
        {
            var result = new List<CountdownEvent>();
            try
            {
                var eventsMatch = Regex.Match(html, @"events:(\[.*?\])", RegexOptions.Singleline);
                if (eventsMatch.Success)
                {
                    var events = JsonConvert.DeserializeObject<List<dynamic>>(eventsMatch.Groups[1].Value);
                    foreach (var ev in events)
                    {
                        string name = ev.name;
                        long timestamp = (long)ev.timestamp;
                        var endTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
                        result.Add(new CountdownEvent { Name = name, EndTime = endTime, TimestampMs = timestamp });
                    }
                }
            }
            catch { }
            return result;
        }

        private static async Task<List<NewsItem>> ExtractNewsFromHtml(string archiveHtml, int maxNewsItems)
        {
            var news = new List<NewsItem>();
            try
            {
                var matches = Regex.Matches(archiveHtml, @"<article>(.*?)</article>", RegexOptions.Singleline);
                int count = 0;
                foreach (Match match in matches)
                {
                    if (count++ >= maxNewsItems) break;
                    var titleMatch = Regex.Match(match.Value, @"<h2>(.*?)</h2>");
                    var dateMatch = Regex.Match(match.Value, @"<time>(.*?)</time>");
                    news.Add(new NewsItem
                    {
                        Title = titleMatch.Groups[1].Value.Trim(),
                        Date = dateMatch.Groups[1].Value.Trim(),
                        Content = Regex.Replace(match.Value, "<.*?>", string.Empty).Trim()
                    });
                }
            }
            catch { }
            return news;
        }
    }
}
