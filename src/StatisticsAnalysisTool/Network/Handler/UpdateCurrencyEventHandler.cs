using StatisticsAnalysisTool.Common;
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

public class UpdateCurrencyEventHandler : EventPacketHandler<UpdateCurrencyEvent>
{
    private static readonly HttpClient HttpClient = new();
    private const string AlbionWebUrl = "http://192.168.1.50:4000/api/albion/stats";

    private readonly TrackingController _trackingController;
    private readonly LiveStatsTracker _liveStatsTracker;

    public UpdateCurrencyEventHandler(TrackingController trackingController) : base((int) EventCodes.UpdateCurrency)
    {
        _trackingController = trackingController;
        _liveStatsTracker = _trackingController?.LiveStatsTracker;
    }

    protected override async Task OnActionAsync(UpdateCurrencyEvent value)
    {
        if (value.CityFaction != CityFaction.Unknown)
        {
            await _trackingController.AddNotificationAsync(SetFactionPointsNotification(value.CityFaction, value.GainedFactionCoins.DoubleValue, value.BonusPremiumGainedFractionFlagPoints.DoubleValue));
        }

        _liveStatsTracker.Add(ValueType.FactionPoints, value.GainedFactionCoins.DoubleValue, value.CityFaction);
        _trackingController.DungeonController?.AddValueToDungeon(value.GainedFactionCoins.DoubleValue, ValueType.FactionPoints, value.CityFaction);
        _trackingController.StatisticController?.AddValue(ValueType.FactionPoints, value.GainedFactionCoins.DoubleValue, value.CityFaction);

        var playerName = SettingsController.CurrentSettings.MainTrackingCharacterName
            ?? _trackingController.EntityController?.LocalUserData?.Username
            ?? "Unknown";

        var guild = _trackingController.EntityController?.LocalUserData?.GuildName ?? string.Empty;

        try
        {
            var payload = new
            {
                playerName,
                guild,
                fame = 0,
                silver = (long)Math.Round(value.GainedFactionCoins.DoubleValue),
                gold = 0,
                totalKills = 0,
                gathering = 0,
                crafting = 0,
                notes = "auto-sync currency"
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

            Console.WriteLine($"Currency sync => {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Currency sync error: " + ex.Message);
        }
    }

    private TrackingNotification SetFactionPointsNotification(CityFaction cityFaction, double gainedFractionPoints, double bonusPremiumGainedFractionPoints)
    {
        return new TrackingNotification(DateTime.Now, new FactionPointsNotificationFragment(LocalizationController.Translation("YOU_HAVE"), AttributeStatOperator.Plus, cityFaction, gainedFractionPoints,
            bonusPremiumGainedFractionPoints, LocalizationController.Translation("FACTION_POINTS"), LocalizationController.Translation("GAINED")), LoggingFilterType.Faction);
    }
}
