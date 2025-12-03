using System.Text.Json. Serialization;

namespace QuantDashboard.Models
{
    public class AppSettings
    {
        [JsonPropertyName("Mode")]
        public string Mode { get; set; } = "UI";

        [JsonPropertyName("BacktestSettings")]
        public BacktestSettingsModel BacktestSettings { get; set; } = new();

        [JsonPropertyName("TradingSettings")]
        public TradingSettingsModel TradingSettings { get; set; } = new();

        [JsonPropertyName("UISettings")]
        public UISettingsModel UISettings { get; set; } = new();
    }

    public class BacktestSettingsModel
    {
        [JsonPropertyName("Enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("Symbol")]
        public string Symbol { get; set; } = "BTCUSDT";

        [JsonPropertyName("Interval")]
        public string Interval { get; set; } = "5m";

        [JsonPropertyName("StartBalance")]
        public decimal StartBalance { get; set; } = 10000;

        [JsonPropertyName("Leverage")]
        public decimal Leverage { get; set; } = 10;

        [JsonPropertyName("DataSource")]
        public string DataSource { get; set; } = "data/futures/BTCUSDT_5m.csv";

        [JsonPropertyName("OutputPath")]
        public string OutputPath { get; set; } = "";
    }

    public class TradingSettingsModel
    {
        [JsonPropertyName("InitialBalance")]
        public decimal InitialBalance { get; set; } = 10000;

        [JsonPropertyName("DefaultLeverage")]
        public decimal DefaultLeverage { get; set; } = 10;

        [JsonPropertyName("DefaultInterval")]
        public string DefaultInterval { get; set; } = "5m";

        [JsonPropertyName("DefaultSymbol")]
        public string DefaultSymbol { get; set; } = "BTCUSDT";

        [JsonPropertyName("PaperTrading")]
        public bool PaperTrading { get; set; } = true;

        [JsonPropertyName("RealTradingEnabled")]
        public bool RealTradingEnabled { get; set; } = false;
    }

    public class UISettingsModel
    {
        [JsonPropertyName("ShowWindow")]
        public bool ShowWindow { get; set; } = true;

        [JsonPropertyName("AutoStart")]
        public bool AutoStart { get; set; } = true;
    }
}