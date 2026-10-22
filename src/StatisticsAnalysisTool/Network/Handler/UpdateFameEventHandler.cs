using StatisticsAnalysisTool.Common.UserSettings;
using StatisticsAnalysisTool.Enumerations;
using StatisticsAnalysisTool.EventLogging;
using StatisticsAnalysisTool.EventLogging.Notification;
using StatisticsAnalysisTool.Localization;
using StatisticsAnalysisTool.Network.Events;
using StatisticsAnalysisTool.Network.Manager;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ValueType = StatisticsAnalysisTool.Enumerations.ValueType;

namespace StatisticsAnalysisTool.Network.Handler;

public class UpdateFameEventHandler : EventPacketHandler<UpdateFameEvent>
{
    private static readonly HttpClient HttpClient = new();
    private const string AlbionWebUrl = "http://192.168.1.85:4000/api/albion/stats";

    private readonly LiveStatsTracker _liveStatsTracker;
    private readonly TrackingController _trackingController;

    public UpdateFameEventHandler(TrackingController trackingController) : base((int) EventCodes.UpdateFame)
    {
        _trackingController = trackingController;
        _liveStatsTracker = _trackingController?.LiveStatsTracker;
    }

    protected override async Task OnActionAsync(UpdateFameEvent value)
    {
        await _trackingController.AddNotificationAsync(SetPveFameNotification(
            value.TotalPlayerFame.DoubleValue,
            value.TotalGainedFame,
            value.ZoneFame.DoubleValue,
            value.PremiumFame,
            value.SatchelFame.DoubleValue,
            value.IsBonusFactorActive,
            value.BonusFactorInPercent));

        _liveStatsTracker.Add(ValueType.Fame, value.TotalGainedFame);
        _trackingController.DungeonController?.AddValueToDungeon(value.TotalGainedFame, ValueType.Fame);
        _trackingController.StatisticController?.AddValue(ValueType.Fame, value.TotalGainedFame);

        var playerName = SettingsController.CurrentSettings.MainTrackingCharacterName
            ?? _trackingController.EntityController?.LocalUserData?.Username
            ?? "Unknown";

        var guild = _trackingController.EntityController?.LocalUserData?.GuildName ?? string.Empty;

        await SendStatsToWebAsync(
            playerName: playerName,
            guild: guild,
            fame: (long)Math.Round(value.TotalGainedFame),
            silver: 0,
            gold: 0,
            totalKills: 0,
            gathering: 0,
            crafting: 0,
            notes: "auto-sync fame");
    }

    private static async Task SendStatsToWebAsync(
        string playerName,
        string guild,
        long fame,
        long silver,
        long gold,
        long totalKills,
        long gathering,
        long crafting,
        string notes)
    {
        try
        {
            var payload = new
            {
                playerName,
                guild,
                fame,
                silver,
                gold,
                totalKills,
                gathering,
                crafting,
                notes
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, AlbionWebUrl)
            {
                Content = content
            };

            request.Headers.Add("X-Client-Token", "dev-secret");

            var response = await HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Fame sync => {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fame sync error: " + ex.Message);
        }
    }

    private TrackingNotification SetPveFameNotification(double totalPlayerFame, double totalGainedFame, double zoneFame, double premiumFame,
        double satchelFame, bool isBonusFactorActive, double bonusFactorInPercent)
    {
        return new TrackingNotification(DateTime.Now, new FameNotificationFragment(LocalizationController.Translation("YOU_HAVE"), AttributeStatOperator.Plus, totalPlayerFame, totalGainedFame,
            LocalizationController.Translation("FAME"), PvpPveType.Pve, zoneFame, premiumFame, satchelFame, isBonusFactorActive, bonusFactorInPercent,
            LocalizationController.Translation("GAINED")), LoggingFilterType.Fame);
    }
}
