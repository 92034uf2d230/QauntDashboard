using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Binance.Net.Enums;
using QuantDashboard.Engine.Data;
using QuantDashboard.Enums;
using QuantDashboard.Managers;
using QuantDashboard.Models;
using QuantDashboard.Strategies;

namespace QuantDashboard
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Load settings from settings.json (or create default if not exists)
            var settings = SettingsManager.Instance.Load();

            Console.WriteLine("===========================================");
            Console.WriteLine("       QUANTBOT PRO - Starting...");
            Console.WriteLine("===========================================");
            Console.WriteLine($"Mode: {settings.Mode}");
            Console.WriteLine($"Settings File: {SettingsManager.Instance.SettingsFilePath}");
            Console.WriteLine();

            switch (settings.AppMode)
            {
                case AppMode.Backtest:
                    RunBacktestMode(settings);
                    break;
                case AppMode.Console:
                    RunConsoleMode(settings);
                    break;
                case AppMode.UI:
                default:
                    RunUIMode(settings, args);
                    break;
            }
        }

        /// <summary>
        /// Run backtest mode - execute backtest automatically and save results
        /// </summary>
        private static void RunBacktestMode(AppSettings settings)
        {
            Console.WriteLine("=== BACKTEST MODE ===");
            Console.WriteLine($"Symbol: {settings.BacktestSettings.Symbol}");
            Console.WriteLine($"Interval: {settings.BacktestSettings.Interval}");
            Console.WriteLine($"Start Balance: ${settings.BacktestSettings.StartBalance:N0}");
            Console.WriteLine($"Leverage: {settings.BacktestSettings.Leverage}x");
            Console.WriteLine();

            if (!settings.BacktestSettings.Enabled)
            {
                Console.WriteLine("Backtest is disabled in settings. Set BacktestSettings.Enabled to true.");
                return;
            }

            try
            {
                var backtester = new BacktestManager();
                var interval = ParseInterval(settings.BacktestSettings.Interval);

                Console.WriteLine("Starting backtest... (This may take a few minutes)");
                Console.WriteLine();

                var task = backtester.RunBacktestAsync(
                    settings.BacktestSettings.Symbol,
                    interval,
                    settings.BacktestSettings.StartBalance,
                    settings.BacktestSettings.Leverage
                );

                // Wait for completion
                task.Wait();
                var result = task.Result;

                // Print results to console
                Console.WriteLine();
                Console.WriteLine("===========================================");
                Console.WriteLine("         BACKTEST RESULTS");
                Console.WriteLine("===========================================");
                Console.WriteLine($"Final Balance: ${result.FinalBalance:N0}");
                Console.WriteLine($"Total PnL: {result.TotalPnL:+0;-0} ({result.TotalPnL / settings.BacktestSettings.StartBalance * 100:F1}%)");
                Console.WriteLine($"Win/Loss: {result.WinCount}W / {result.LossCount}L");
                Console.WriteLine($"Max Drawdown: -{result.MaxDrawdown:F2}%");
                Console.WriteLine();

                // Save results to file
                var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
                var resultDir = Path.Combine(baseDir, "backtest_results");

                if (!Directory.Exists(resultDir))
                    Directory.CreateDirectory(resultDir);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"backtest_{settings.BacktestSettings.Symbol}_{settings.BacktestSettings.Interval}_{timestamp}.txt";
                var filePath = Path.Combine(resultDir, fileName);

                File.WriteAllText(filePath, result.Log);
                Console.WriteLine($"Full report saved to: {filePath}");

                // Save summary as JSON
                var jsonFileName = $"backtest_{settings.BacktestSettings.Symbol}_{settings.BacktestSettings.Interval}_{timestamp}_summary.json";
                var jsonFilePath = Path.Combine(resultDir, jsonFileName);

                var summary = new
                {
                    Symbol = settings.BacktestSettings.Symbol,
                    Interval = settings.BacktestSettings.Interval,
                    StartBalance = settings.BacktestSettings.StartBalance,
                    FinalBalance = result.FinalBalance,
                    TotalPnL = result.TotalPnL,
                    WinCount = result.WinCount,
                    LossCount = result.LossCount,
                    MaxDrawdown = result.MaxDrawdown,
                    Timestamp = DateTime.Now
                };

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(jsonFilePath, JsonSerializer.Serialize(summary, jsonOptions));
                Console.WriteLine($"Summary JSON saved to: {jsonFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Backtest error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("Backtest completed. Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Run console mode - test PatternMatchingStrategy with CSV data
        /// </summary>
        private static void RunConsoleMode(AppSettings settings)
        {
            Console.WriteLine("=== CONSOLE MODE ===");
            Console.WriteLine($"Symbol: {settings.TradingSettings.DefaultSymbol}");
            Console.WriteLine($"Interval: {settings.TradingSettings.DefaultInterval}");
            Console.WriteLine();

            // Build path to CSV data
            var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var dataDir = Path.Combine(baseDir, "data", "futures");

            var filePath = Path.Combine(dataDir, $"{settings.TradingSettings.DefaultSymbol}_{settings.TradingSettings.DefaultInterval}.csv");

            // If specified data source exists, use it
            if (!string.IsNullOrEmpty(settings.BacktestSettings.DataSource))
            {
                var customPath = Path.Combine(baseDir, settings.BacktestSettings.DataSource);
                if (File.Exists(customPath))
                    filePath = customPath;
            }

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"CSV not found: {filePath}");
                Console.WriteLine("Please run download_futures_klines.py to download data to data/futures/");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"Loading candles from: {filePath}");
            var candles = CsvCandleLoader.LoadFromFile(filePath);
            Console.WriteLine($"Loaded {candles.Count} candles.");

            if (candles.Count < 100)
            {
                Console.WriteLine("Not enough candles (need at least 100).");
                Console.ReadLine();
                return;
            }

            // Create PatternMatchingStrategy with full history
            var strategy = new PatternMatchingStrategy(
                historicalCandles: candles,
                k: 20,
                threshold: 0.001
            );

            // Use recent candles as current flow
            int recentWindow = Math.Min(300, candles.Count);
            var recentCandles = candles.GetRange(candles.Count - recentWindow, recentWindow);

            // Get trading signal
            TradingSignal signal = strategy.Decide(recentCandles);

            var lastCandle = candles[^1];
            Console.WriteLine();
            Console.WriteLine("===========================================");
            Console.WriteLine("       PATTERN MATCHING ANALYSIS");
            Console.WriteLine("===========================================");
            Console.WriteLine($"Last candle timestamp: {lastCandle.Timestamp:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"Open={lastCandle.Open}, High={lastCandle.High}, Low={lastCandle.Low}, Close={lastCandle.Close}");
            Console.WriteLine($"Volume={lastCandle.Volume}, Trades={lastCandle.TradeCount}");
            Console.WriteLine();
            Console.WriteLine($"PatternMatchingStrategy signal: {signal} (Hold/Buy/Sell)");
            Console.WriteLine();

            Console.WriteLine("Console mode completed. Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Run UI mode - launch Avalonia application
        /// </summary>
        private static void RunUIMode(AppSettings settings, string[] args)
        {
            Console.WriteLine("=== UI MODE ===");
            Console.WriteLine($"Show Window: {settings.UISettings.ShowWindow}");
            Console.WriteLine($"Auto Start: {settings.UISettings.AutoStart}");
            Console.WriteLine($"Paper Trading: {settings.TradingSettings.PaperTrading}");
            Console.WriteLine();

            if (!settings.UISettings.ShowWindow)
            {
                Console.WriteLine("UISettings.ShowWindow is false. Exiting...");
                return;
            }

            // Enable file watcher for runtime settings changes
            SettingsManager.Instance.EnableFileWatcher();

            // Build and run Avalonia application
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        /// <summary>
        /// Parse interval string to KlineInterval
        /// </summary>
        private static KlineInterval ParseInterval(string interval)
        {
            return interval.ToLower() switch
            {
                "1m" => KlineInterval.OneMinute,
                "5m" => KlineInterval.FiveMinutes,
                "15m" => KlineInterval.FifteenMinutes,
                "1h" => KlineInterval.OneHour,
                "4h" => KlineInterval.FourHour,
                "1d" => KlineInterval.OneDay,
                _ => KlineInterval.FiveMinutes
            };
        }

        /// <summary>
        /// Build Avalonia application
        /// </summary>
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}