using System;
using System.Text.Json.Serialization;

namespace QuantDashboard.Models
{
    /// <summary>
    /// Available application modes
    /// </summary>
    public enum AppMode
    {
        UI,
        Console,
        Backtest
    }

    /// <summary>
    /// Settings for backtest mode
    /// </summary>
    public class BacktestSettings
    {
        public bool Enabled { get; set; } = true;
        public string Symbol { get; set; } = "BTCUSDT";
        public string Interval { get; set; } = "5m";
        public decimal StartBalance { get; set; } = 10000m;
        public decimal Leverage { get; set; } = 10m;
        public string DataSource { get; set; } = "data/futures/BTCUSDT_5m.csv";
    }

    /// <summary>
    /// Settings for trading mode
    /// </summary>
    public class TradingSettings
    {
        public decimal InitialBalance { get; set; } = 10000m;
        public decimal DefaultLeverage { get; set; } = 10m;
        public string DefaultInterval { get; set; } = "5m";
        public string DefaultSymbol { get; set; } = "BTCUSDT";
        public bool PaperTrading { get; set; } = true;
    }

    /// <summary>
    /// Settings for UI mode
    /// </summary>
    public class UISettings
    {
        public bool ShowWindow { get; set; } = true;
        public bool AutoStart { get; set; } = false;
    }

    /// <summary>
    /// Root application settings class
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Maximum allowed leverage (based on exchange limits)
        /// </summary>
        public const decimal MaxLeverage = 125m;

        /// <summary>
        /// Application mode: "Console", "UI", or "Backtest"
        /// </summary>
        public string Mode { get; set; } = "UI";

        /// <summary>
        /// Backtest mode settings
        /// </summary>
        public BacktestSettings BacktestSettings { get; set; } = new BacktestSettings();

        /// <summary>
        /// Trading settings
        /// </summary>
        public TradingSettings TradingSettings { get; set; } = new TradingSettings();

        /// <summary>
        /// UI settings
        /// </summary>
        public UISettings UISettings { get; set; } = new UISettings();

        /// <summary>
        /// Parse Mode string to AppMode enum
        /// </summary>
        [JsonIgnore]
        public AppMode AppMode
        {
            get
            {
                if (Enum.TryParse<AppMode>(Mode, true, out var result))
                {
                    return result;
                }
                return AppMode.UI; // Default to UI
            }
        }

        /// <summary>
        /// Create default settings
        /// </summary>
        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                Mode = "UI",
                BacktestSettings = new BacktestSettings(),
                TradingSettings = new TradingSettings(),
                UISettings = new UISettings()
            };
        }

        /// <summary>
        /// Validate settings
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            errorMessage = string.Empty;

            // Validate Mode
            if (!Enum.TryParse<AppMode>(Mode, true, out _))
            {
                errorMessage = $"Invalid Mode: '{Mode}'. Valid values are: UI, Console, Backtest";
                return false;
            }

            // Validate BacktestSettings
            if (BacktestSettings.StartBalance <= 0)
            {
                errorMessage = "BacktestSettings.StartBalance must be greater than 0";
                return false;
            }

            if (BacktestSettings.Leverage <= 0 || BacktestSettings.Leverage > MaxLeverage)
            {
                errorMessage = $"BacktestSettings.Leverage must be between 1 and {MaxLeverage}";
                return false;
            }

            // Validate TradingSettings
            if (TradingSettings.InitialBalance <= 0)
            {
                errorMessage = "TradingSettings.InitialBalance must be greater than 0";
                return false;
            }

            if (TradingSettings.DefaultLeverage <= 0 || TradingSettings.DefaultLeverage > MaxLeverage)
            {
                errorMessage = $"TradingSettings.DefaultLeverage must be between 1 and {MaxLeverage}";
                return false;
            }

            return true;
        }
    }
}
