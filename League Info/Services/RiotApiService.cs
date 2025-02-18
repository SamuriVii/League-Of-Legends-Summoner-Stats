using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LeagueInfo.Services
{
    public class RiotApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private Dictionary<int, (string Name, string IconUrl)> _championMapping;

        public RiotApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["RiotApiKey"];
        }


        //METODY WYCIĄGANIA DANYCH Z API:

        //Pobieranie danych Riot ID (Name#Tag)
        public async Task<AccountDto?> GetAccountDataAsync(string gameName, string tagLine)
        {
            var url = $"https://europe.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{gameName}/{tagLine}?api_key={_apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AccountDto>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        //Pobranie danych summonera
        public async Task<SummonerLevelDto?> GetSummonerDataByPuuidAsync(string puuid)
        {
            var url = $"https://eun1.api.riotgames.com/lol/summoner/v4/summoners/by-puuid/{puuid}?api_key={_apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SummonerLevelDto>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        //Pobieranie Champion Mastery z nazwami bohaterów
        public async Task<List<ChampionMasteryWithNameDto>?> GetChampionMasteryWithNamesAsync(string puuid)
        {
            var masteryUrl = $"https://eun1.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-puuid/{puuid}?api_key={_apiKey}";
            var masteryResponse = await _httpClient.GetAsync(masteryUrl);

            if (!masteryResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var masteryJson = await masteryResponse.Content.ReadAsStringAsync();
            var masteryList = JsonSerializer.Deserialize<List<ChampionMasteryWithNameDto>>(masteryJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (_championMapping == null || _championMapping.Count == 0)
            {
                await LoadChampionDataAsync();
            }

            var masteryWithNames = new List<ChampionMasteryWithNameDto>();
            foreach (var mastery in masteryList)
            {
                var mapping = _championMapping.GetValueOrDefault(mastery.ChampionId, ("Unknown", null));
                masteryWithNames.Add(new ChampionMasteryWithNameDto
                {
                    ChampionId = mastery.ChampionId,
                    ChampionName = mapping.Name,
                    ChampionLevel = mastery.ChampionLevel,
                    ChampionPoints = mastery.ChampionPoints,
                    IconUrl = mapping.IconUrl
                });
            }

            return masteryWithNames;
        }

        //Załadowanie danych o bohaterach z Data Dragon
        private async Task LoadChampionDataAsync()
        {
            //Pobieranie najnowszej wersji Data Dragon
            var versionUrl = "https://ddragon.leagueoflegends.com/api/versions.json";
            var versionResponse = await _httpClient.GetAsync(versionUrl);

            if (!versionResponse.IsSuccessStatusCode)
            {
                throw new Exception("Nie udało się pobrać najnowszej wersji Data Dragon.");
            }

            var versionContent = await versionResponse.Content.ReadAsStringAsync();
            var versions = JsonSerializer.Deserialize<List<string>>(versionContent);
            var latestVersion = versions?.FirstOrDefault() ?? "14.24.1";

            //Pobieranie danych bohaterów z najnowszej wersji
            var dataDragonUrl = $"https://ddragon.leagueoflegends.com/cdn/{latestVersion}/data/en_US/champion.json";
            var response = await _httpClient.GetAsync(dataDragonUrl);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Nie udało się załadować danych o bohaterach.");
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var championData = JsonDocument.Parse(jsonContent);

            //Przygotowanie mapowania championId -> nazwa + ikona
            _championMapping = new Dictionary<int, (string Name, string IconUrl)>();

            foreach (var property in championData.RootElement.GetProperty("data").EnumerateObject())
            {
                var championId = int.Parse(property.Value.GetProperty("key").GetString());
                var championName = property.Name;
                var iconUrl = $"https://ddragon.leagueoflegends.com/cdn/{latestVersion}/img/champion/{championName}.png";

                _championMapping[championId] = (championName, iconUrl);
            }
        }

        //Pobranie danych rankingowych graczy
        public async Task<List<RankedStatsDto>?> GetRankedStatsAsync(string summonerId)
        {
            var url = $"https://eun1.api.riotgames.com/lol/league/v4/entries/by-summoner/{summonerId}?api_key={_apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<RankedStatsDto>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        //Pobranie statystyk historii gier
        public async Task<List<string>?> GetMatchIdsAsync(string puuid, int count = 10)
        {
            var url = $"https://europe.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids?start=0&count={count}&api_key={_apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<string>>(jsonContent);
        }

        public async Task<MatchDetailsDto?> GetMatchDetailsAsync(string matchId)
        {
            var url = $"https://europe.api.riotgames.com/lol/match/v5/matches/{matchId}?api_key={_apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MatchDetailsDto>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        //Osiągniecia gracza w ostatnich meczach:
        public async Task<PlayerAchievementsDto?> GetPlayerAchievementsAsync(string puuid, int count = 10)
        {
            //Pobranie listy matchId
            var matchIds = await GetMatchIdsAsync(puuid, count);

            if (matchIds == null || matchIds.Count == 0)
            {
                return null;
            }

            //Analiza osiągnięć gracza
            var achievements = new PlayerAchievementsDto();

            foreach (var matchId in matchIds)
            {
                var matchDetails = await GetMatchDetailsAsync(matchId);
                if (matchDetails == null) continue;

                //Szukanie statystyk gracza w danym meczu
                var playerStats = matchDetails.Info.Participants.FirstOrDefault(p => p.Puuid == puuid);
                if (playerStats == null) continue;

                //Analiza osiągnięć
                achievements.MaxKills = Math.Max(achievements.MaxKills, playerStats.Kills);
                achievements.MaxAssists = Math.Max(achievements.MaxAssists, playerStats.Assists);
                achievements.MaxDamageDealt = Math.Max(achievements.MaxDamageDealt, playerStats.TotalDamageDealtToChampions);
                achievements.MaxCS = Math.Max(achievements.MaxCS, playerStats.TotalMinionsKilled);
                achievements.BestKDA = Math.Max(
                achievements.BestKDA, playerStats.Deaths == 0 ? playerStats.Kills + playerStats.Assists 
                    : Math.Round((double)(playerStats.Kills + playerStats.Assists) / playerStats.Deaths, 2));
            }

            return achievements;
        }
    }
}



//KLASY DTO DO METOD UŻYWANYCH WYŻEJ:

//Account-V1
public class AccountDto
{
    public string Puuid { get; set; }
    public string GameName { get; set; }
    public string TagLine { get; set; }
}

//Summoner-V4
public class SummonerLevelDto
{
    public string Id { get; set; }
    public string AccountId { get; set; }
    public int SummonerLevel { get; set; }
}

//Champion Mastery
public class ChampionMasteryWithNameDto
{
    public string ChampionName { get; set; }
    public int ChampionId { get; set; }
    public int ChampionLevel { get; set; }
    public int ChampionPoints { get; set; }
    public string IconUrl { get; set; }
}

//Osiągnięcia gracza
public class PlayerAchievementsDto
{
    public int MaxKills { get; set; } = 0;
    public int MaxAssists { get; set; } = 0;
    public int MaxDamageDealt { get; set; } = 0;
    public int MaxCS { get; set; } = 0;
    public double BestKDA { get; set; } = 0.0;
}

//Statystyki rankingowe
public class RankedStatsDto
{
    public string QueueType { get; set; }
    public string Tier { get; set; }
    public string Rank { get; set; }
    public int LeaguePoints { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public string IconUrl => $"https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-shared-components/global/default/images/{Tier.ToLower()}.png";
}

//Szczegóły meczu
public class MatchDetailsDto
{
    public Metadata Metadata { get; set; }
    public Info Info { get; set; }
}

//Dane meczu cz. 1
public class Metadata
{
    public string MatchId { get; set; }
    public string[] Participants { get; set; }
}

//Dane meczu cz. 2
public class Info
{
    public long GameDuration { get; set; }
    public string GameMode { get; set; }
    public string GameType { get; set; }
    public ParticipantDto[] Participants { get; set; }
}

//Dane szczegółowe meczu
public class ParticipantDto
{
    public string Puuid { get; set; }
    public string ChampionName { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public bool Win { get; set; }
    public int TotalMinionsKilled { get; set; }
    public int NeutralMinionsKilled { get; set; }
    public int TotalDamageDealtToChampions { get; set; }
    public string SummonerName { get; set; }
    public string Lane { get; set; }
    public string Role { get; set; }
}
