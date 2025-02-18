using LeagueInfo.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LeagueInfo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlayerController : ControllerBase
    {
        private readonly RiotApiService _riotApiService;

        public PlayerController(RiotApiService riotApiService)
        {
            _riotApiService = riotApiService;
        }

        //Endpoint do wszystkich danych gracza
        [HttpGet("all-stats/{gameName}/{tagLine}")]
        public async Task<IActionResult> GetAllPlayerStats(string gameName, string tagLine, int matchCount = 10)
        {
            if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(tagLine))
            {
                return BadRequest("Nick gracza i tag są wymagane.");
            }

            //Pobieranie wszystkich danych gracza
            var accountData = await _riotApiService.GetAccountDataAsync(gameName, tagLine);
            if (accountData == null)
            {
                return NotFound($"Nie znaleziono gracza: {gameName}#{tagLine}");
            }

            var summonerData = await _riotApiService.GetSummonerDataByPuuidAsync(accountData.Puuid);
            if (summonerData == null)
            {
                return NotFound($"Nie udało się znaleźć danych Summoner dla puuid: {accountData.Puuid}");
            }

            var rankedStats = await _riotApiService.GetRankedStatsAsync(summonerData.Id);

            var championMastery = await _riotApiService.GetChampionMasteryWithNamesAsync(accountData.Puuid);
            var top5Mastery = championMastery?
                .OrderByDescending(c => c.ChampionPoints)
                .Take(5)
                .ToList();

            var achievements = await _riotApiService.GetPlayerAchievementsAsync(accountData.Puuid, matchCount);

            var matchIds = await _riotApiService.GetMatchIdsAsync(accountData.Puuid, matchCount);
            List<MatchDetailsDto>? matchDetails = null;

            if (matchIds != null && matchIds.Count > 0)
            {
                matchDetails = new List<MatchDetailsDto>();
                foreach (var matchId in matchIds)
                {
                    var matchDetail = await _riotApiService.GetMatchDetailsAsync(matchId);
                    if (matchDetail != null)
                    {
                        matchDetails.Add(matchDetail);
                    }
                }
            }

            //Zbiorcza odpowiedź
            var response = new
            {
                Account = new
                {
                    accountData.GameName,
                    accountData.TagLine,
                    accountData.Puuid
                },
                RankedStats = rankedStats,
                ChampionMastery = top5Mastery,
                Achievements = achievements,
                MatchHistory = matchDetails
            };
            return Ok(response);
        }
    }
}
