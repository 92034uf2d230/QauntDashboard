using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Binance.Net.Clients;
using Binance.Net.Enums;
using QuantDashboard.Enums;
using QuantDashboard.Managers;
using QuantDashboard.Models;
using QuantDashboard.Strategies;

namespace QuantDashboard.Engine
{
    public class TradingEngine
    {
        private readonly BinanceRestClient _client;
        private readonly RiskManager _riskManager;
        private readonly LogManager _logManager;

        // 전략들
        private readonly IStrategy _superTrend, _ichimoku, _maCross, _linReg, _adx;
        private readonly IStrategy _orderBlock, _fvg, _vwap, _whale, _smartMoney;
        private readonly IStrategy _zScore, _hurst, _efficiency, _vector, _delta;
        private readonly IStrategy _insideBar, _fractal, _rsiDiv, _squeeze, _pattern;

        private bool _isRunning = true;

        private DateTime _lastExitTime = DateTime.MinValue;

        private decimal _virtualBalance = 10000;
        private decimal _manualLeverage = 10;
        private KlineInterval _selectedInterval = KlineInterval.FiveMinutes;
        private string _selectedSymbol = "BTCUSDT";

        private TradingSignal _currentPosition = TradingSignal.Hold;
        private decimal _entryPrice, _positionAmount;

        private bool _isViewingHistory = false;
        private const decimal FeeRate = 0.0005m;

        // 엔진 상태 노출
        public decimal VirtualBalance => _virtualBalance;
        public decimal ManualLeverage
        {
            get => _manualLeverage;
            set => _manualLeverage = value;
        }
        public KlineInterval SelectedInterval
        {
            get => _selectedInterval;
            set => _selectedInterval = value;
        }
        public string SelectedSymbol
        {
            get => _selectedSymbol;
            set => _selectedSymbol = value;
        }
        public TradingSignal CurrentPosition => _currentPosition;
        public decimal EntryPrice => _entryPrice;
        public decimal PositionAmount => _positionAmount;
        public DateTime LastExitTime => _lastExitTime;
        public bool IsViewingHistory
        {
            get => _isViewingHistory;
            set => _isViewingHistory = value;
        }

        // UI와 공유할 트레이드 히스토리
        public ObservableCollection<TradeRecord> TradeHistory { get; } = new();

        // UI 콜백
        public Action<string>? OnLog;                          // 로그 출력용
        public Action<int, decimal, decimal>? OnStatusUpdate;  // (score, currentPrice, realPnL)

        public TradingEngine(RiskManager riskManager, LogManager logManager)
        {
            _riskManager = riskManager;
            _logManager = logManager;
            _client = new BinanceRestClient();

            _superTrend = new SuperTrendStrategy();
            _ichimoku = new IchimokuCloudStrategy();
            _maCross = new MaCrossStrategy();
            _linReg = new LinRegStrategy();
            _adx = new AdxFilterStrategy();

            _orderBlock = new OrderBlockStrategy();
            _fvg = new FairValueGapStrategy();
            _vwap = new VwapReversionStrategy();
            _whale = new WhaleAggressionStrategy();
            _smartMoney = new SmartMoneyStrategy();

            _zScore = new ZScoreStrategy();
            _hurst = new HurstExponentStrategy();
            _efficiency = new EfficiencyRatioStrategy();
            _vector = new VectorPatternStrategy();
            _delta = new DeltaDivergenceStrategy();

            _insideBar = new InsideBarStrategy();
            _fractal = new FractalBreakoutStrategy();
            _rsiDiv = new RsiDivergenceStrategy();
            _squeeze = new VolatilitySqueezeStrategy();
            _pattern = new PatternCandleStrategy();
        }

        public void Start()
        {
            Task.Run(BotLoop);
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void AddLogInternal(string msg)
        {
            OnLog?.Invoke(msg);
        }

        private decimal CalculateRealPnL(decimal entry, decimal current, decimal amount, TradingSignal pos)
        {
            decimal rawPnL = (pos == TradingSignal.Buy) ? (current - entry) : (entry - current);
            rawPnL *= amount;
            decimal entryFee = entry * amount * FeeRate;
            decimal exitFee = current * amount * FeeRate;
            return rawPnL - (entryFee + exitFee);
        }

        private async Task BotLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var result = await _client.UsdFuturesApi.ExchangeData.GetKlinesAsync(_selectedSymbol, _selectedInterval, limit: 1000);
                    if (!result.Success) { await Task.Delay(1000); continue; }

                    var allCandles = result.Data.ToList();
                    if (allCandles.Count < 100) { await Task.Delay(1000); continue; }

                    var currentRealtimePrice = allCandles.Last().ClosePrice;
                    var closedCandles = allCandles.Take(allCandles.Count - 1).ToList();

                    var s1 = _superTrend.Analyze(closedCandles);
                    var s2 = _ichimoku.Analyze(closedCandles);
                    var s3 = _maCross.Analyze(closedCandles);
                    var s4 = _linReg.Analyze(closedCandles);
                    var s5 = _adx.Analyze(closedCandles);

                    var s6 = _orderBlock.Analyze(closedCandles);
                    var s7 = _fvg.Analyze(closedCandles);
                    var s8 = _vwap.Analyze(closedCandles);
                    var s9 = _whale.Analyze(closedCandles);
                    var s10 = _smartMoney.Analyze(closedCandles);

                    var s11 = _zScore.Analyze(closedCandles);
                    var s12 = _hurst.Analyze(closedCandles);
                    var s13 = _efficiency.Analyze(closedCandles);
                    var s14 = _vector.Analyze(closedCandles);
                    var s15 = _delta.Analyze(closedCandles);

                    var s16 = _insideBar.Analyze(closedCandles);
                    var s17 = _fractal.Analyze(closedCandles);
                    var s18 = _rsiDiv.Analyze(closedCandles);
                    var s19 = _squeeze.Analyze(closedCandles);
                    var s20 = _pattern.Analyze(closedCandles);

                    int score = 0;
                    score += Sc(s6, 3) + Sc(s9, 3) + Sc(s14, 3) + Sc(s19, 3);
                    score += Sc(s1, 2) + Sc(s2, 2) + Sc(s7, 2) + Sc(s8, 2) + Sc(s10, 2) + Sc(s15, 2) + Sc(s16, 2) + Sc(s17, 2) + Sc(s18, 2) + Sc(s20, 2);
                    score += Sc(s3, 1) + Sc(s4, 1) + Sc(s11, 1) + Sc(s13, 1);

                    if (s5 == TradingSignal.Hold) score = (int)(score * 0.5);

                    bool adxFilterPass = true;
                    if (score > 0 && s5 == TradingSignal.Sell) adxFilterPass = false;
                    if (score < 0 && s5 == TradingSignal.Buy) adxFilterPass = false;

                    if (_currentPosition == TradingSignal.Hold)
                    {
                        if ((DateTime.Now - _lastExitTime).TotalSeconds < 60) { await Task.Delay(1000); continue; }

                        if (score >= 7 && adxFilterPass)
                        {
                            Enter(TradingSignal.Buy, currentRealtimePrice);
                            var results = new List<(IStrategy strategy, TradingSignal signal)> {
                                (_superTrend,s1), (_ichimoku,s2), (_maCross,s3), (_linReg,s4), (_adx,s5),
                                (_orderBlock,s6), (_fvg,s7), (_vwap,s8), (_whale,s9), (_smartMoney,s10),
                                (_zScore,s11), (_hurst,s12), (_efficiency,s13), (_vector,s14), (_delta,s15),
                                (_insideBar,s16), (_fractal,s17), (_rsiDiv,s18), (_squeeze,s19), (_pattern,s20)
                            };
                            _logManager.RecordEntry(TradingSignal.Buy, score, currentRealtimePrice, closedCandles, results);
                            AddLogInternal($"[LONG] {_selectedSymbol} Score {score}");
                        }
                        else if (score <= -7 && adxFilterPass)
                        {
                            Enter(TradingSignal.Sell, currentRealtimePrice);
                            var results = new List<(IStrategy strategy, TradingSignal signal)> {
                                (_superTrend,s1), (_ichimoku,s2), (_maCross,s3), (_linReg,s4), (_adx,s5),
                                (_orderBlock,s6), (_fvg,s7), (_vwap,s8), (_whale,s9), (_smartMoney,s10),
                                (_zScore,s11), (_hurst,s12), (_efficiency,s13), (_vector,s14), (_delta,s15),
                                (_insideBar,s16), (_fractal,s17), (_rsiDiv,s18), (_squeeze,s19), (_pattern,s20)
                            };
                            _logManager.RecordEntry(TradingSignal.Sell, score, currentRealtimePrice, closedCandles, results);
                            AddLogInternal($"[SHORT] {_selectedSymbol} Score {score}");
                        }
                    }
                    else
                    {
                        var exitSignal = _riskManager.AnalyzeExit(
                            closedCandles, _currentPosition, _entryPrice, currentRealtimePrice, _manualLeverage
                        );

                        bool rev = (_currentPosition == TradingSignal.Buy && score <= -7) ||
                                   (_currentPosition == TradingSignal.Sell && score >= 7);

                        if (rev) { exitSignal.Action = ExitAction.CloseAll; exitSignal.Reason = "Signal Reversal"; }

                        if (exitSignal.Action == ExitAction.CloseAll)
                        {
                            decimal realPnL = CalculateRealPnL(_entryPrice, currentRealtimePrice, _positionAmount, _currentPosition);
                            decimal netRoe = _riskManager.CalculateNetRoe(_entryPrice, currentRealtimePrice, _currentPosition, _manualLeverage);

                            _virtualBalance += realPnL;
                            AddLogInternal($"[CLOSED] {exitSignal.Reason} PnL:${realPnL:F2}");

                            _logManager.RecordExit(_currentPosition, _entryPrice, currentRealtimePrice, realPnL, netRoe, exitSignal.Reason);

                            var record = new TradeRecord
                            {
                                IsLive = false,
                                Type = _currentPosition,
                                EntryPrice = _entryPrice,
                                ExitPrice = currentRealtimePrice,
                                Amount = _positionAmount,
                                PnL = realPnL,
                                Roe = netRoe,
                                Leverage = _manualLeverage,
                                ExitReason = exitSignal.Reason,
                                EntryTime = DateTime.Now,
                                ExitTime = DateTime.Now,
                                Title = $"[{DateTime.Now:HH:mm}] {_selectedSymbol} {(_currentPosition == TradingSignal.Buy ? "L" : "S")} ${realPnL:+0.0;-0.0}"
                            };
                            TradeHistory.Insert(1, record);

                            _currentPosition = TradingSignal.Hold;
                            _lastExitTime = DateTime.Now;
                        }
                        else if (exitSignal.Action == ExitAction.ClosePartial)
                        {
                            decimal closeAmount = _positionAmount * exitSignal.AmountRatio;
                            decimal realPnL = CalculateRealPnL(_entryPrice, currentRealtimePrice, closeAmount, _currentPosition);
                            decimal netRoe = _riskManager.CalculateNetRoe(_entryPrice, currentRealtimePrice, _currentPosition, _manualLeverage);

                            _virtualBalance += realPnL;
                            _positionAmount -= closeAmount;

                            AddLogInternal($"[PARTIAL] {exitSignal.Reason} PnL:${realPnL:F2}");
                            _logManager.RecordExit(_currentPosition, _entryPrice, currentRealtimePrice, realPnL, netRoe, $"PARTIAL: {exitSignal.Reason}");

                            var record = new TradeRecord
                            {
                                IsLive = false,
                                Type = _currentPosition,
                                EntryPrice = _entryPrice,
                                ExitPrice = currentRealtimePrice,
                                Amount = closeAmount,
                                PnL = realPnL,
                                Roe = 0,
                                Leverage = _manualLeverage,
                                ExitReason = "Partial",
                                EntryTime = DateTime.Now,
                                ExitTime = DateTime.Now,
                                Title = $"[{DateTime.Now:HH:mm}] PARTIAL ${realPnL:+0.0;-0.0}"
                            };
                            TradeHistory.Insert(1, record);
                        }
                    }

                    decimal realPnLNow = 0;
                    if (_currentPosition != TradingSignal.Hold)
                        realPnLNow = CalculateRealPnL(_entryPrice, currentRealtimePrice, _positionAmount, _currentPosition);

                    OnStatusUpdate?.Invoke(score, currentRealtimePrice, realPnLNow);
                }
                catch (Exception ex)
                {
                    AddLogInternal($"[Engine Error] {ex.Message}");
                }

                await Task.Delay(1000);
            }
        }

        private void Enter(TradingSignal s, decimal p)
        {
            _currentPosition = s;
            _entryPrice = p;
            _positionAmount = (_virtualBalance * _manualLeverage) / p;
            _riskManager.OnEntry(p);
            _riskManager.UpdateDynamicSettings(_selectedInterval, _manualLeverage, _selectedSymbol);
        }

        private int Sc(TradingSignal s, int w) =>
            s == TradingSignal.Buy ? w : (s == TradingSignal.Sell ? -w : 0);
    }
}