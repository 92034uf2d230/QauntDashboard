using System;
using System.IO;
using System.Text.Json;
using QuantDashboard.Models;

namespace QuantDashboard. Managers
{
    public class SettingsManager
    {
        private static SettingsManager?  _instance;
        private static readonly object _lock = new object();
        private const string SettingsFileName = "settings.json";

        public AppSettings CurrentSettings { get; private set; }

        private SettingsManager()
        {
            CurrentSettings = new AppSettings();
        }

        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SettingsManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public AppSettings Load()
        {
            try
            {
                string settingsPath = GetSettingsPath();

                if (! File.Exists(settingsPath))
                {
                    Console.WriteLine($"[SettingsManager] settings. json not found. Creating default at: {settingsPath}");
                    CreateDefaultSettings(settingsPath);
                }

                string json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                });

                if (settings != null)
                {
                    CurrentSettings = settings;
                    Console.WriteLine($"[SettingsManager] Loaded settings.  Mode: {CurrentSettings.Mode}");
                }
                else
                {
                    Console.WriteLine("[SettingsManager] Failed to deserialize settings.  Using defaults.");
                    CurrentSettings = new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Console. WriteLine($"[SettingsManager] Error loading settings: {ex. Message}");
                Console.WriteLine("[SettingsManager] Using default settings.");
                CurrentSettings = new AppSettings();
            }

            return CurrentSettings;
        }

        public void Save()
        {
            try
            {
                string settingsPath = GetSettingsPath();
                string json = JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(settingsPath, json);
                Console.WriteLine($"[SettingsManager] Settings saved to: {settingsPath}");
            }
            catch (Exception ex)
            {
                Console. WriteLine($"[SettingsManager] Error saving settings: {ex. Message}");
            }
        }

        private void CreateDefaultSettings(string path)
        {
            var defaultSettings = new AppSettings
            {
                Mode = "UI",
                BacktestSettings = new BacktestSettingsModel
                {
                    Enabled = false,
                    Symbol = "BTCUSDT",
                    Interval = "5m",
                    StartBalance = 10000,
                    Leverage = 10,
                    DataSource = "data/futures/BTCUSDT_5m.csv",
                    OutputPath = ""
                },
                TradingSettings = new TradingSettingsModel
                {
                    InitialBalance = 10000,
                    DefaultLeverage = 10,
                    DefaultInterval = "5m",
                    DefaultSymbol = "BTCUSDT",
                    PaperTrading = true,
                    RealTradingEnabled = false
                },
                UISettings = new UISettingsModel
                {
                    ShowWindow = true,
                    AutoStart = true
                }
            };

            string json = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }

        private string GetSettingsPath()
        {
            string baseDir = AppContext.BaseDirectory;
            string settingsPath = Path.Combine(baseDir, SettingsFileName);

            if (!File.Exists(settingsPath))
            {
                string projectRoot = Path.GetFullPath(Path. Combine(baseDir, "..", "..", ". .", ".."));
                settingsPath = Path.Combine(projectRoot, SettingsFileName);
            }

            return settingsPath;
        }
    }
}