using System;
using System.IO;
using Avalonia;
using QuantDashboard.Engine. Data;
using QuantDashboard. Enums;
using QuantDashboard.Managers;
using QuantDashboard. Strategies;

namespace QuantDashboard
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var settings = SettingsManager.Instance. Load();

            switch (settings.Mode. ToUpper())
            {
                case "BACKTEST":
                    RunBacktestMode(settings);
                    break;
                case "CONSOLE":
                    RunConsoleMode(settings);
                    break;
                case "UI":
                default:
                    RunUIMode(args);
                    break;
            }
        }

        private static void RunBacktestMode(Models.AppSettings settings)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("     BACKTEST MODE - AUTOMATED RUN");
            Console.WriteLine("==============================================");

            if (! settings.BacktestSettings. Enabled)
            {
                Console.WriteLine("[ERROR] Backtest is disabled in settings. json");
                Console.WriteLine("Set 'BacktestSettings. Enabled' to true");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"Symbol: {settings.BacktestSettings.Symbol}");
            Console. WriteLine($"Interval: {settings.BacktestSettings.Interval}");
            Console.WriteLine($"Start Balance: ${settings.BacktestSettings.StartBalance}");
            Console.WriteLine($"Leverage: {settings.BacktestSettings.Leverage}x");
            Console.WriteLine("==============================================\n");

            Console.WriteLine("Starting backtest...\n");

            try
            {
                var backtester = new BacktestManager();
                var interval = ParseInterval(settings.BacktestSettings.Interval);

                var result = backtester.RunBacktestAsync(
                    settings. BacktestSettings.Symbol,
                    interval,
                    settings.BacktestSettings.StartBalance,
                    settings.BacktestSettings. Leverage
                ). Result;

                Console.WriteLine("\n==============================================");
                Console.WriteLine("          BACKTEST RESULTS");
                Console.WriteLine("==============================================");
                Console.WriteLine($"Final Balance: ${result.FinalBalance:N0}");
                Console.WriteLine($"Total PnL: {result.TotalPnL:+0;-0} ({result.TotalPnL / settings.BacktestSettings.StartBalance * 100:F1}%)");
                Console.WriteLine($"Win/Loss: {result.WinCount}W / {result.LossCount}L");
                Console.WriteLine($"Max Drawdown: -{result.MaxDrawdown:F2}%");
                Console.WriteLine("==============================================");

                string outputPath = settings.BacktestSettings.OutputPath;
                if (string.IsNullOrEmpty(outputPath))
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder. Desktop);
                    outputPath = Path.Combine(desktop, $"backtest_{settings.BacktestSettings.Symbol}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                }

                File.WriteAllText(outputPath, result.Log);
                Console.WriteLine($"\nReport saved: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] Backtest failed: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadLine();
        }

        private static void RunConsoleMode(Models. AppSettings settings)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("  CONSOLE MODE - PATTERN MATCHING TEST");
            Console.WriteLine("==============================================");

            var baseDir = Path.GetFullPath(Path. Combine(AppContext.BaseDirectory, "..", "..", "..", ".. "));
            var dataDir = Path.Combine(baseDir, "data", "futures");
            var filePath = Path.Combine(dataDir, $"{settings.TradingSettings.DefaultSymbol}_15m.csv");

            if (! File.Exists(filePath))
            {
                Console.WriteLine($"CSV not found: {filePath}");
                Console.WriteLine("Run download_futures_klines.py first.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Loading candles...");
            var candles = CsvCandleLoader.LoadFromFile(filePath);
            Console.WriteLine($"Loaded {candles.Count} candles.");

            if (candles.Count < 100)
            {
                Console. WriteLine("Not enough candles.");
                Console.ReadLine();
                return;
            }

            var strategy = new PatternMatchingStrategy(
                historicalCandles: candles,
                k: 20,
                threshold: 0.001
            );

            int recentWindow = Math.Min(300, candles.Count);
            var recentCandles = candles.GetRange(candles.Count - recentWindow, recentWindow);

            TradingSignal signal = strategy. Decide(recentCandles);

            var lastCandle = candles[^1];
            Console.WriteLine($"\nLast candle: {lastCandle. Timestamp:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"Open={lastCandle.Open}, High={lastCandle.High}, Low={lastCandle.Low}, Close={lastCandle.Close}");
            Console.WriteLine($"Volume={lastCandle.Volume}, Trades={lastCandle.TradeCount}");
            Console.WriteLine($"\nPatternMatchingStrategy signal: {signal}\n");

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        private static void RunUIMode(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        private static Binance.Net.Enums.KlineInterval ParseInterval(string interval)
        {
            return interval. ToLower() switch
            {
                "1m" => Binance.Net.Enums.KlineInterval.OneMinute,
                "5m" => Binance.Net.Enums.KlineInterval.FiveMinutes,
                "15m" => Binance.Net.Enums.KlineInterval.FifteenMinutes,
                "1h" => Binance.Net.Enums.KlineInterval.OneHour,
                "4h" => Binance.Net.Enums.KlineInterval.FourHour,
                "1d" => Binance.Net.Enums.KlineInterval.OneDay,
                _ => Binance.Net. Enums.KlineInterval.FiveMinutes
            };
        }
    }
}