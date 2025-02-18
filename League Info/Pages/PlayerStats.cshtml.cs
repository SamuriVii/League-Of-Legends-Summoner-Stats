using LeagueInfo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeagueInfo.Pages
{
    public class PlayerStatsModel : PageModel
    {
        private readonly RiotApiService _riotApiService;

        public PlayerStatsModel(RiotApiService riotApiService)
        {
            _riotApiService = riotApiService;
        }

        [BindProperty(SupportsGet = true)]
        public string GameName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string TagLine { get; set; }

        public string Puuid { get; set; }
        public int SummonerLevel { get; set; }
        public List<RankedStatsDto> RankedStats { get; set; }
        public List<ChampionMasteryWithNameDto> ChampionMastery { get; set; }
        public PlayerAchievementsDto Achievements { get; set; }
        public List<MatchDetailsDto> MatchHistory { get; set; }
        public Dictionary<string, double> LaneDistribution { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(GameName) || string.IsNullOrWhiteSpace(TagLine))
            {
                ModelState.AddModelError(string.Empty, "Summoner Name i Tag są wymagane.");
                return Page();
            }

            //Pobieranie danych konta
            var accountData = await _riotApiService.GetAccountDataAsync(GameName, TagLine);
            if (accountData == null)
            {
                ModelState.AddModelError(string.Empty, "Nie znaleziono gracza.");
                return Page();
            }

            Puuid = accountData.Puuid;

            //Pobieranie SummonerLvL
            var summonerData = await _riotApiService.GetSummonerDataByPuuidAsync(Puuid);
            if (summonerData != null)
            {
                SummonerLevel = summonerData.SummonerLevel;
            }

            //Pobieranie statystyk rankingowych
            RankedStats = await _riotApiService.GetRankedStatsAsync(summonerData?.Id);

            //Pobieranie champion mastery
            var masteryData = await _riotApiService.GetChampionMasteryWithNamesAsync(Puuid);
            ChampionMastery = masteryData?.OrderByDescending(c => c.ChampionPoints).Take(5).ToList();

            //Pobieranie ostatnich osiągnięcia gracza
            Achievements = await _riotApiService.GetPlayerAchievementsAsync(Puuid, 20);

            //Pobieranie historii meczów + liczenie linii
            var matchIds = await _riotApiService.GetMatchIdsAsync(Puuid, 20);
            if (matchIds != null)
            {
                MatchHistory = new List<MatchDetailsDto>();
                var laneCount = new Dictionary<string, int> 
                { 
                    { "TOP", 0 }, { "JUNGLE", 0 }, { "MIDDLE", 0 }, 
                    { "ADC", 0 }, { "SUPPORT", 0 }
                };

                foreach (var matchId in matchIds)
                {
                    var matchDetails = await _riotApiService.GetMatchDetailsAsync(matchId);
                    if (matchDetails != null)
                    {
                        MatchHistory.Add(matchDetails);

                        //Wyliczanie danych i przypisywanie do linii
                        var player = matchDetails.Info.Participants.FirstOrDefault(p => p.Puuid == Puuid);

                        if (player != null && matchDetails.Info.GameMode == "CLASSIC")
                        {
                            if (player.Lane == "BOTTOM")
                            {
                                if (player.Assists > player.Kills && (player.TotalMinionsKilled / (matchDetails.Info.GameDuration / 60.0)) < 5) laneCount["SUPPORT"]++;
                                else laneCount["ADC"]++;    
                            }
                            else if (laneCount.ContainsKey(player.Lane))
                            {
                                laneCount[player.Lane]++;
                            }
                        }
                    }
                }

                //Obliczanie procentów dla każdej linii
                int totalGames = laneCount.Values.Sum();
                LaneDistribution = laneCount
                    .Where(l => l.Value > 0)
                    .ToDictionary(k => k.Key, v => Math.Round((double)v.Value / totalGames * 100, 2));
            }
            return Page();
        }

    }
}
