using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;
using QuantDashboard.Strategies;

namespace QuantDashboard.Managers;

public class BacktestResult
{
    public decimal TotalPnL { get; set; }
    public decimal FinalBalance { get; set; }
    public decimal MaxDrawdown { get; set; }
    public int WinCount { get; set; }
    public int LossCount { get; set; }
    public string Log { get; set; } = string.Empty;
}

/// <summary>
/// Per-trade log for analysis (written to JSON).
/// </summary>
public class BacktestTradeRecord
{
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }

    public string Symbol { get; set; } = "";
    public KlineInterval Interval { get; set; }

    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }

    /// <summary>LONG / SHORT</summary>
    public TradingSignal Direction { get; set; }

    public decimal PnL { get; set; }
    public decimal Roe { get; set; }
    public decimal BalanceAfter { get; set; }

    /// <summary>Total score at entry.</summary>
    public int TotalScore { get; set; }

    /// <summary>Strategy signals at entry (key = C# type name, e.g. "SuperTrendStrategy").</summary>
    public Dictionary<string, TradingSignal> StrategySignals { get; set; } = new();

    /// <summary>Strategy status values at entry (key = C# type name).</summary>
    public Dictionary<string, string> StrategyStatusValues { get; set; } = new();
}

/// <summary>
/// Backtest manager aligned with live logic:
/// - Same strategy set & scoring
/// - Uses RiskManager for SL/TP/trailing
/// - Intra-candle price sequence (OHLC / OHLC reversed for short)
/// - Cooldown after exit
/// - Detailed per-trade JSON logs
/// </summary>
public class BacktestManager
{
    private readonly BinanceRestClient _client;
    private readonly RiskManager _riskManager;

    private readonly List<IStrategy> _strategies;
    private readonly IStrategy _adxStrat;

    private const decimal FeeRate = 0.0005m;

    private const int LongScoreThreshold = 7;
    private const int ShortScoreThreshold = -7;

    private const int CooldownBarsDefault = 12;

    private readonly List<BacktestTradeRecord> _tradeRecords = new();

    public BacktestManager()
    {
        _client = new BinanceRestClient();
        _riskManager = new RiskManager();

        _strategies = new List<IStrategy>
        {
            new SuperTrendStrategy(),        // 0
            new IchimokuCloudStrategy(),     // 1
            new MaCrossStrategy(),           // 2
            new LinRegStrategy(),            // 3

            new OrderBlockStrategy(),        // 4
            new FairValueGapStrategy(),      // 5
            new VwapReversionStrategy(),     // 6
            new WhaleAggressionStrategy(),   // 7
            new SmartMoneyStrategy(),        // 8

            new ZScoreStrategy(),            // 9
            new HurstExponentStrategy(),     // 10
            new EfficiencyRatioStrategy(),   // 11
            new VectorPatternStrategy(),     // 12
            new DeltaDivergenceStrategy(),   // 13

            new InsideBarStrategy(),         // 14
            new FractalBreakoutStrategy(),   // 15
            new RsiDivergenceStrategy(),     // 16
            new VolatilitySqueezeStrategy(), // 17
            new PatternCandleStrategy()      // 18
        };

        _adxStrat = new AdxFilterStrategy();
    }

    /// <summary>
    /// Fetch historical USDT futures klines for up to 1 year.
    /// </summary>
    private async Task<List<IBinanceKline>> FetchHistoryAsync(string symbol, KlineInterval interval)
    {
        var allCandles = new List<IBinanceKline>();

        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddDays(-365);

        DateTime? currentStart = startTime;

        while (true)
        {
            try
            {
                var result = await _client.UsdFuturesApi.ExchangeData
                    .GetKlinesAsync(symbol, interval, currentStart, null, 1000);

                if (!result.Success)
                    break;

                var data = result.Data.OrderBy(c => c.OpenTime).ToList();
                if (data.Count == 0)
                    break;

                data = data.Where(c => c.OpenTime <= endTime).ToList();
                if (data.Count == 0)
                    break;

                allCandles.AddRange(data);

                var lastTime = data.Last().OpenTime;

                if (lastTime >= endTime)
                    break;

                currentStart = lastTime.AddSeconds(1);
                await Task.Delay(50);
            }
            catch
            {
                break;
            }
        }

        var ordered = allCandles
            .GroupBy(c => c.OpenTime)
            .Select(g => g.First())
            .OrderBy(c => c.OpenTime)
            .ToList();

        return ordered;
    }

    public async Task<BacktestResult> RunBacktestAsync(
        string symbol,
        KlineInterval interval,
        decimal startBalance,
        decimal leverage)
    {
        var result = new BacktestResult();
        var sb = new StringBuilder();

        _tradeRecords.Clear();

        var history = await FetchHistoryAsync(symbol, interval);
        if (history.Count < 200)
        {
            result.Log = "Not enough data (Need > 200 candles)";
            return result;
        }

        decimal balance = startBalance;
        decimal peakBalance = startBalance;
        decimal maxDd = 0;

        TradingSignal currentPos = TradingSignal.Hold;
        decimal entryPrice = 0;
        decimal posAmount = 0;

        BacktestTradeRecord? currentTrade = null;

        int lastExitBarIndex = -99999;
        int cooldownBars = CooldownBarsDefault;

        sb.AppendLine("=== BACKTEST REPORT ===");
        sb.AppendLine($"Target: {symbol} | Interval: {interval} | Leverage: {leverage}x");
        sb.AppendLine(
            $"Data Range: {history.First().OpenTime:yyyy/MM/dd HH:mm} ~ {history.Last().OpenTime:yyyy/MM/dd HH:mm} ({history.Count} Candles)");
        sb.AppendLine("------------------------------------------------------------------");

        for (int i = 100; i < history.Count; i++)
        {
            var closedCandles = history.GetRange(i - 100, 100);
            var currentCandle = history[i];

            var s5 = _adxStrat.Analyze(closedCandles);

            var sigs = new List<TradingSignal>();
            foreach (var strat in _strategies)
                sigs.Add(strat.Analyze(closedCandles));

            int score = 0;

            // Tier 1 (3 points)
            score += Sc(sigs[4], 3);   // OrderBlock
            score += Sc(sigs[7], 3);   // Whale
            score += Sc(sigs[12], 3);  // Vector
            score += Sc(sigs[17], 3);  // VolatilitySqueeze

            // Tier 2 (2 points)
            score += Sc(sigs[0], 2);   // SuperTrend
            score += Sc(sigs[1], 2);   // Ichimoku
            score += Sc(sigs[5], 2);   // FVG
            score += Sc(sigs[6], 2);   // VWAP
            score += Sc(sigs[8], 2);   // SmartMoney
            score += Sc(sigs[13], 2);  // DeltaDiv
            score += Sc(sigs[14], 2);  // InsideBar
            score += Sc(sigs[15], 2);  // Fractal
            score += Sc(sigs[16], 2);  // RsiDiv
            score += Sc(sigs[18], 2);  // PatternCandle

            // Tier 3 (1 point)
            score += Sc(sigs[2], 1);   // MA
            score += Sc(sigs[3], 1);   // LinReg
            score += Sc(sigs[9], 1);   // ZScore
            score += Sc(sigs[11], 1);  // ER

            if (s5 == TradingSignal.Hold)
                score = (int)(score * 0.5);

            bool adxPass = true;
            if (score > 0 && s5 == TradingSignal.Sell) adxPass = false;
            if (score < 0 && s5 == TradingSignal.Buy) adxPass = false;

            // Intra-candle sim
            decimal[] tickPrices;
            if (currentPos == TradingSignal.Sell)
                tickPrices = new[]
                {
                    currentCandle.OpenPrice,
                    currentCandle.HighPrice,
                    currentCandle.LowPrice,
                    currentCandle.ClosePrice
                };
            else
                tickPrices = new[]
                {
                    currentCandle.OpenPrice,
                    currentCandle.LowPrice,
                    currentCandle.HighPrice,
                    currentCandle.ClosePrice
                };

            foreach (var price in tickPrices)
            {
                if (currentPos == TradingSignal.Hold)
                {
                    if ((i - lastExitBarIndex) < cooldownBars)
                        continue;

                    // LONG entry
                    if (score >= LongScoreThreshold && adxPass)
                    {
                        currentPos = TradingSignal.Buy;
                        entryPrice = price;
                        posAmount = CalculatePositionSize(balance, leverage, entryPrice, symbol, interval);

                        _riskManager.OnEntry(entryPrice);
                        _riskManager.UpdateDynamicSettings(interval, leverage, symbol);

                        currentTrade = new BacktestTradeRecord
                        {
                            EntryTime = currentCandle.OpenTime,
                            Symbol = symbol,
                            Interval = interval,
                            EntryPrice = entryPrice,
                            Direction = TradingSignal.Buy,
                            TotalScore = score
                        };

                        for (int idx = 0; idx < _strategies.Count; idx++)
                        {
                            var strat = _strategies[idx];
                            var sig = sigs[idx];

                            // ★ key = C# type name (pure English), ignores any Korean in Name
                            string key = strat.GetType().Name;

                            currentTrade.StrategySignals[key] = sig;
                            currentTrade.StrategyStatusValues[key] = strat.GetStatusValue();
                        }
                    }
                    // SHORT entry
                    else if (score <= ShortScoreThreshold && adxPass)
                    {
                        currentPos = TradingSignal.Sell;
                        entryPrice = price;
                        posAmount = CalculatePositionSize(balance, leverage, entryPrice, symbol, interval);

                        _riskManager.OnEntry(entryPrice);
                        _riskManager.UpdateDynamicSettings(interval, leverage, symbol);

                        currentTrade = new BacktestTradeRecord
                        {
                            EntryTime = currentCandle.OpenTime,
                            Symbol = symbol,
                            Interval = interval,
                            EntryPrice = entryPrice,
                            Direction = TradingSignal.Sell,
                            TotalScore = score
                        };

                        for (int idx = 0; idx < _strategies.Count; idx++)
                        {
                            var strat = _strategies[idx];
                            var sig = sigs[idx];
                            string key = strat.GetType().Name;

                            currentTrade.StrategySignals[key] = sig;
                            currentTrade.StrategyStatusValues[key] = strat.GetStatusValue();
                        }
                    }
                }
                else
                {
                    var exitSignal = _riskManager.AnalyzeExit(
                        closedCandles, currentPos, entryPrice, price, leverage);

                    bool rev = (currentPos == TradingSignal.Buy && score <= ShortScoreThreshold) ||
                               (currentPos == TradingSignal.Sell && score >= LongScoreThreshold);

                    if (rev)
                    {
                        exitSignal.Action = ExitAction.CloseAll;
                        exitSignal.Reason = "Signal Reversal";
                    }

                    if (exitSignal.Action == ExitAction.CloseAll)
                    {
                        decimal pnl = CalculatePnL(entryPrice, price, posAmount, currentPos);
                        balance += pnl;

                        string winLose = pnl > 0 ? "WIN" : "LOSS";
                        decimal roe = (entryPrice == 0 || leverage == 0)
                            ? 0
                            : (pnl / (posAmount * entryPrice / leverage)) * 100m;

                        sb.AppendLine(
                            $"[{currentCandle.OpenTime:MM-dd HH:mm}] EXIT ({exitSignal.Reason}) | " +
                            $"{winLose} | PnL: ${pnl:F2} | Bal: ${balance:F0}");

                        if (pnl > 0) result.WinCount++;
                        else result.LossCount++;

                        if (currentTrade != null)
                        {
                            currentTrade.ExitTime = currentCandle.OpenTime;
                            currentTrade.ExitPrice = price;
                            currentTrade.PnL = pnl;
                            currentTrade.Roe = roe;
                            currentTrade.BalanceAfter = balance;

                            _tradeRecords.Add(currentTrade);
                            currentTrade = null;
                        }

                        currentPos = TradingSignal.Hold;
                        lastExitBarIndex = i;
                        break;
                    }
                }
            }

            if (balance > peakBalance) peakBalance = balance;
            decimal dd = (peakBalance > 0) ? (peakBalance - balance) / peakBalance * 100 : 0;
            if (dd > maxDd) maxDd = dd;

            if (balance <= 0)
            {
                sb.AppendLine("!!! BANKRUPTCY !!!");
                break;
            }
        }

        result.FinalBalance = balance;
        result.TotalPnL = balance - startBalance;
        result.MaxDrawdown = maxDd;
        result.Log = sb.ToString();

        SaveTradeRecordsToJson(symbol, interval, sb);

        return result;
    }

    private int Sc(TradingSignal s, int w)
        => s == TradingSignal.Buy ? w : (s == TradingSignal.Sell ? -w : 0);

    private decimal CalculatePositionSize(
        decimal balance,
        decimal leverage,
        decimal price,
        string symbol,
        KlineInterval interval)
    {
        decimal volMultiplier = 1.0m;
        if (symbol == "BTCUSDT")
            volMultiplier = 1.0m;
        else if (symbol is "ETHUSDT" or "BNBUSDT" or "XRPUSDT" or "ADAUSDT")
            volMultiplier = 1.2m;
        else if (symbol is "SOLUSDT" or "AVAXUSDT")
            volMultiplier = 1.3m;
        else
            volMultiplier = 2.5m;

        decimal baseVol = interval switch
        {
            KlineInterval.OneMinute      => 0.003m,
            KlineInterval.FiveMinutes    => 0.005m,
            KlineInterval.FifteenMinutes => 0.008m,
            KlineInterval.OneHour        => 0.015m,
            KlineInterval.FourHour       => 0.03m,
            KlineInterval.OneDay         => 0.05m,
            _                            => 0.008m
        };

        decimal estimatedSlPercent = baseVol * volMultiplier;
        if (estimatedSlPercent <= 0) estimatedSlPercent = 0.01m;

        decimal riskAmount = balance * 0.02m;

        decimal safeSize = riskAmount / estimatedSlPercent;

        decimal maxSize = balance * leverage;

        decimal finalSize = Math.Min(safeSize, maxSize);

        if (finalSize < 50m) finalSize = 50m;

        return finalSize / price;
    }

    private decimal CalculatePnL(
        decimal entry,
        decimal curr,
        decimal amt,
        TradingSignal pos)
    {
        decimal rawDiff = (pos == TradingSignal.Buy) ? (curr - entry) : (entry - curr);
        decimal profit = rawDiff * amt;
        decimal fees = (entry * amt * FeeRate) + (curr * amt * FeeRate);
        return profit - fees;
    }

    private void SaveTradeRecordsToJson(
        string symbol,
        KlineInterval interval,
        StringBuilder sb)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logDir = Path.Combine(baseDir, "backtest_logs");

            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            string fileName =
                $"bt_{symbol}_{interval}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string fullPath = Path.Combine(logDir, fileName);

            string json = JsonSerializer.Serialize(_tradeRecords, options);
            File.WriteAllText(fullPath, json);

            sb.AppendLine();
            sb.AppendLine($"[BACKTEST LOG] Trade records saved to: {fullPath}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"[BACKTEST LOG ERROR] {ex.Message}");
        }
    }
}