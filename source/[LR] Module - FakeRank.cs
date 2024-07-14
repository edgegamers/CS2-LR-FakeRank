using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using LevelsRanksApi;
using System.Text.Json;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;

namespace LevelsRanksModuleFakeRank;

[MinimumApiVersion(80)]
public class LevelsRanksModuleFakeRank : BasePlugin
{
    public override string ModuleName => "[LR] Module - FakeRank";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "ABKAM designed by RoadSide Romeo & Wend4r";

    private readonly Dictionary<string, (int competitiveRanking, int competitiveRankType)> _playerRanks = new();
    private ILevelsRanksApi? _api;
    private readonly PluginCapability<ILevelsRanksApi> _apiCapability = new("levels_ranks");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        base.OnAllPluginsLoaded(hotReload);

        _api = _apiCapability.Get();
        if (_api == null)
        {
            Server.PrintToConsole("Levels Ranks API is currently unavailable.");
            return;
        }

        CreateRanksConfig();

        RegisterListener<Listeners.OnTick>(OnTick);
        AddTimer(2.0f, () =>
        {
            var _ = FetchPlayerRanks();
        }, TimerFlags.REPEAT);
    }

    private void CreateRanksConfig()
    {
        var configDirectory = Path.Combine(Application.RootDirectory, "configs/plugins/LevelsRanks");
        var filePath = Path.Combine(configDirectory, "settings_fakerank.json");

        if (!File.Exists(filePath))
        {
            var defaultConfig = new
            {
                LR_FakeRank = new
                {
                    Type = "1",
                    FakeRank = new Dictionary<string, string>
                    {
                        { "1", "1" },
                        { "2", "2" },
                        { "3", "3" },
                        { "4", "4" },
                        { "5", "5" },
                        { "6", "6" },
                        { "7", "7" },
                        { "8", "8" },
                        { "9", "9" },
                        { "10", "10" },
                        { "11", "11" },
                        { "12", "12" },
                        { "13", "13" },
                        { "14", "14" },
                        { "15", "15" },
                        { "16", "16" },
                        { "17", "17" },
                        { "18", "18" }
                    }
                }
            };

            Directory.CreateDirectory(configDirectory);
            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
    }

    private async Task FetchPlayerRanks()
    {
        var players = Utilities.GetPlayers().Where(player => !player.IsBot && player.TeamNum != (int)CsTeam.Spectator);

        var maxConcurrentTasks = 10;
        var semaphore = new SemaphoreSlim(maxConcurrentTasks);

        var tasks = players.Select(async player =>
        {
            await semaphore.WaitAsync();
            try
            {
                await UpdatePlayerRank(player);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private Dictionary<string, int> _lastKnownLevels = new();

    private async Task UpdatePlayerRank(CCSPlayerController player)
    {
        var steamId64 = player.SteamID;
        var steamId = _api!.ConvertToSteamId(steamId64);
        var currentRanks = await _api.GetCurrentELOAsync();

        if (currentRanks.TryGetValue(steamId, out var currentElo))
        {
            if (!_lastKnownLevels.TryGetValue(steamId, out var lastLevel) || currentElo != lastLevel)
                _playerRanks[steamId] = (currentElo, 11);
            _lastKnownLevels[steamId] = currentElo;
        }
        else
        {
            Logger.LogWarning($"No rank found for player {steamId} (SteamID64: {steamId64})");
        }
    }

    private void OnTick()
    {
        var players = Utilities.GetPlayers().Where(player => !player.IsBot && player.TeamNum != (int)CsTeam.Spectator);

        foreach (var player in players)
        {
            var steamId64 = player.SteamID;
            var steamId = _api!.ConvertToSteamId(steamId64);

            if (_playerRanks.TryGetValue(steamId, out var rankInfo))
                if (player.CompetitiveRankType != (sbyte)rankInfo.competitiveRankType ||
                    player.CompetitiveRanking != rankInfo.competitiveRanking)
                {
                    player.CompetitiveRankType = (sbyte)rankInfo.competitiveRankType;
                    player.CompetitiveRanking = rankInfo.competitiveRanking;
                    player.CompetitiveWins = 777;
                }
        }
    }
}