using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using QuantDashboard.Strategies;
using QuantDashboard.Enums;
using QuantDashboard.Managers; 

namespace QuantDashboard;

public class TradeRecord
{
    public string Id { get; set; }
    public string Title { get; set; }
    public TradingSignal Type { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal PnL { get; set; }
    public decimal Roe { get; set; }
    public decimal Leverage { get; set; }
    public string ExitReason { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public bool IsLive { get; set; } = false;
}

public partial class MainWindow : Window
{
    private BinanceRestClient _client;
    private bool _isRunning = true;
    
    private DateTime _lastExitTime = DateTime.MinValue; 
    private RiskManager _riskManager;
    private LogManager _logManager;

    private IStrategy _superTrend, _ichimoku, _maCross, _linReg, _adx;
    private IStrategy _orderBlock, _fvg, _vwap, _whale, _smartMoney;
    private IStrategy _zScore, _hurst, _efficiency, _vector, _delta;
    private IStrategy _insideBar, _fractal, _rsiDiv, _squeeze, _pattern;

    private decimal _virtualBalance = 10000;
    private decimal _manualLeverage = 10;
    private bool _isManualTpSlEnabled = true;
    private KlineInterval _selectedInterval = KlineInterval.FiveMinutes;
    private string _selectedSymbol = "BTCUSDT"; 

    private TradingSignal _currentPosition = TradingSignal.Hold;
    private decimal _entryPrice, _positionAmount;
    
    private List<string> _logLines = new List<string>();
    private ObservableCollection<TradeRecord> _tradeHistory = new ObservableCollection<TradeRecord>();
    private bool _isViewingHistory = false;
    private const decimal FeeRate = 0.0005m;

    public MainWindow()
    {
        InitializeComponent();
        _client = new BinanceRestClient();
        _riskManager = new RiskManager();
        _logManager = new LogManager();

        // 1. 전략 초기화
        _superTrend = new SuperTrendStrategy(); _ichimoku = new IchimokuCloudStrategy(); _maCross = new MaCrossStrategy(); _linReg = new LinRegStrategy(); _adx = new AdxFilterStrategy();
        _orderBlock = new OrderBlockStrategy(); _fvg = new FairValueGapStrategy(); _vwap = new VwapReversionStrategy(); _whale = new WhaleAggressionStrategy(); _smartMoney = new SmartMoneyStrategy();
        _zScore = new ZScoreStrategy(); _hurst = new HurstExponentStrategy(); _efficiency = new EfficiencyRatioStrategy(); _vector = new VectorPatternStrategy(); _delta = new DeltaDivergenceStrategy();
        _insideBar = new InsideBarStrategy(); _fractal = new FractalBreakoutStrategy(); _rsiDiv = new RsiDivergenceStrategy(); _squeeze = new VolatilitySqueezeStrategy(); _pattern = new PatternCandleStrategy();

        // 2. UI 이벤트
        LevSlider.PropertyChanged += (s, e) => { 
            if (e.Property.Name == "Value") { 
                _manualLeverage = (decimal)LevSlider.Value; 
                if (LevText != null) LevText.Text = $"{_manualLeverage:F0}x"; 
                UpdateRiskSettings(); 
            }
        };

        TimeframeCombo.SelectionChanged += (s, e) => {
             OnTimeframeChanged(s, e);
             UpdateRiskSettings(); 
        };

        SymbolCombo.SelectionChanged += (s, e) => {
            if (SymbolCombo.SelectedItem is ComboBoxItem item)
            {
                string newSymbol = item.Content?.ToString() ?? "BTCUSDT";
                if (_currentPosition != TradingSignal.Hold) return; 

                if (_selectedSymbol != newSymbol)
                {
                    _selectedSymbol = newSymbol;
                    AddLog($"[SYSTEM] Target Coin Changed: {_selectedSymbol}");
                    UpdateRiskSettings(); 
                }
            }
        };
        
        InitializeHistory();
        HistoryCombo.SelectionChanged += OnHistorySelectionChanged;

        UpdateRiskSettings();
        
        // 초기 로그 출력
        AddLog($"System Initialized...");
        AddLog($"Log File: {_logManager.CurrentLogPath}");

        Task.Run(BotLoop);
    }

    // ★ [오류 해결] AddLog 함수 추가 (UI 로그 출력용)
    private void AddLog(string msg)
    {
        Dispatcher.UIThread.Post(() => {
            _logLines.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
            if (_logLines.Count > 50) _logLines.RemoveAt(_logLines.Count - 1);
            LogText.Text = string.Join("\n", _logLines);
        });
    }

    private void UpdateRiskSettings()
    {
        _riskManager.UpdateDynamicSettings(_selectedInterval, _manualLeverage, _selectedSymbol);

        Dispatcher.UIThread.Post(() => {
            if (_currentPosition == TradingSignal.Hold)
            {
                decimal slRoe = _riskManager.CurrentSlPercent * _manualLeverage;
                decimal tpRoe = _riskManager.CurrentTpPercent * _manualLeverage;

                if (TpInput != null) TpInput.Text = $"{_riskManager.CurrentTpPercent:F2}";
                if (SlInput != null) SlInput.Text = $"{_riskManager.CurrentSlPercent:F2}";

                if (PosSl != null) PosSl.Text = $"SL: -{_riskManager.CurrentSlPercent:F2}% (ROE -{slRoe:F0}%)";
                if (PosTp != null) PosTp.Text = $"TP: +{_riskManager.CurrentTpPercent:F2}% (ROE +{tpRoe:F0}%)";
            }
        });
    }

    private void InitializeHistory()
    {
        _tradeHistory.Add(new TradeRecord { Title = "🟢 LIVE MONITORING", IsLive = true });
        HistoryCombo.ItemsSource = _tradeHistory;
        HistoryCombo.SelectedIndex = 0;
    }

    private void OnHistorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HistoryCombo.SelectedItem is TradeRecord record)
        {
            if (record.IsLive) { _isViewingHistory = false; UpdatePositionCardLiveState(); }
            else { _isViewingHistory = true; DisplayHistoricalRecord(record); }
        }
    }

    private void DisplayHistoricalRecord(TradeRecord r)
    {
        PosType.Text = r.Type == TradingSignal.Buy ? "LONG (CLOSED)" : "SHORT (CLOSED)";
        PosType.Foreground = r.PnL >= 0 ? Brushes.LightGreen : Brushes.Red;
        PosBadge.Background = r.PnL >= 0 ? SolidColorBrush.Parse("#3000FF00") : SolidColorBrush.Parse("#30FF0000");
        PosPnl.Text = $"{r.PnL:+$#,##0.00;-$#,##0.00}";
        PosPnl.Foreground = r.PnL >= 0 ? Brushes.LightGreen : Brushes.Red;
        PosRoe.Text = $"{r.Roe:+0.00;-0.00}%";
        PosRoe.Foreground = r.PnL >= 0 ? Brushes.LightGreen : Brushes.Red;
        PosEntry.Text = $"${r.EntryPrice:F4}"; 
        PosMark.Text = $"${r.ExitPrice:F4}";
        PosLiq.Text = "CLOSED";
        PosSize.Text = $"${(r.EntryPrice * r.Amount):N0}"; PosMargin.Text = $"${(r.EntryPrice * r.Amount / r.Leverage):N2}";
        PosLev.Text = $"{r.Leverage:F0}x"; PosTp.Text = $"EXIT: {r.ExitReason}"; PosSl.Text = $"{r.ExitTime:HH:mm:ss}";
    }

    private void UpdatePositionCardLiveState()
    {
        if (_currentPosition == TradingSignal.Hold)
        {
            PosType.Text = "NO POSITION"; PosType.Foreground = Brushes.Gray; PosBadge.Background = SolidColorBrush.Parse("#20FFFFFF");
            PosLev.Text = ""; PosRoe.Text = "0.00%"; PosRoe.Foreground = Brushes.Gray;
            PosEntry.Text = "-"; PosMark.Text = "-"; PosLiq.Text = "-";
            PosSize.Text = "-"; PosMargin.Text = "-";
            PosPnl.Text = "$0.00"; PosPnl.Foreground = Brushes.Gray;
            SymbolCombo.IsEnabled = true; 
            UpdateRiskSettings(); 
        }
    }

    private void OnTimeframeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TimeframeCombo.SelectedItem is ComboBoxItem item)
        {
            string tag = item.Content?.ToString() ?? "5m"; 
            _selectedInterval = tag switch { "1m" => KlineInterval.OneMinute, "5m" => KlineInterval.FiveMinutes, "15m" => KlineInterval.FifteenMinutes, "1h" => KlineInterval.OneHour, "4h" => KlineInterval.FourHour, "1d" => KlineInterval.OneDay, _ => KlineInterval.FiveMinutes };
        }
    }
    private void OnWindowDrag(object? s, PointerPressedEventArgs e) { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); }
    private void OnMinimize(object? s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximize(object? s, RoutedEventArgs e) => WindowState = WindowState.Maximized == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object? s, RoutedEventArgs e) => Close();

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
                // 데이터 요청
                var result = await _client.UsdFuturesApi.ExchangeData.GetKlinesAsync(_selectedSymbol, _selectedInterval, limit: 1000);
                if (!result.Success) { await Task.Delay(1000); continue; }
                
                var allCandles = result.Data.ToList();
                if (allCandles.Count < 100) { await Task.Delay(1000); continue; }

                var currentRealtimePrice = allCandles.Last().ClosePrice; 
                var closedCandles = allCandles.Take(allCandles.Count - 1).ToList();

                // 전략 분석
                var s1 = _superTrend.Analyze(closedCandles); var s2 = _ichimoku.Analyze(closedCandles); var s3 = _maCross.Analyze(closedCandles); var s4 = _linReg.Analyze(closedCandles); var s5 = _adx.Analyze(closedCandles);
                var s6 = _orderBlock.Analyze(closedCandles); var s7 = _fvg.Analyze(closedCandles); var s8 = _vwap.Analyze(closedCandles); var s9 = _whale.Analyze(closedCandles); var s10 = _smartMoney.Analyze(closedCandles);
                var s11 = _zScore.Analyze(closedCandles); var s12 = _hurst.Analyze(closedCandles); var s13 = _efficiency.Analyze(closedCandles); var s14 = _vector.Analyze(closedCandles); var s15 = _delta.Analyze(closedCandles);
                var s16 = _insideBar.Analyze(closedCandles); var s17 = _fractal.Analyze(closedCandles); var s18 = _rsiDiv.Analyze(closedCandles); var s19 = _squeeze.Analyze(closedCandles); var s20 = _pattern.Analyze(closedCandles);

                // 점수 계산
                int score = 0;
                score += Sc(s6, 3) + Sc(s9, 3) + Sc(s14, 3) + Sc(s19, 3);
                score += Sc(s1, 2) + Sc(s2, 2) + Sc(s7, 2) + Sc(s8, 2) + Sc(s10, 2) + Sc(s15, 2) + Sc(s16, 2) + Sc(s17, 2) + Sc(s18, 2) + Sc(s20, 2);
                score += Sc(s3, 1) + Sc(s4, 1) + Sc(s11, 1) + Sc(s13, 1);
                
                if (s5 == TradingSignal.Hold) score = (int)(score * 0.5);

                // ADX 역추세 필터 (수정됨)
                bool adxFilterPass = true;
                if (score > 0 && s5 == TradingSignal.Sell) adxFilterPass = false;
                if (score < 0 && s5 == TradingSignal.Buy) adxFilterPass = false;

                // [수정] log 변수 제거됨 (AddLog 함수 사용)

                if (_currentPosition == TradingSignal.Hold) 
                {
                    if ((DateTime.Now - _lastExitTime).TotalSeconds < 60) { await Task.Delay(1000); continue; }

                    if (score >= 7 && adxFilterPass) { 
                        Enter(TradingSignal.Buy, currentRealtimePrice);
                        var results = new List<(IStrategy, TradingSignal)> {
                            (_superTrend,s1), (_ichimoku,s2), (_maCross,s3), (_linReg,s4), (_adx,s5),
                            (_orderBlock,s6), (_fvg,s7), (_vwap,s8), (_whale,s9), (_smartMoney,s10),
                            (_zScore,s11), (_hurst,s12), (_efficiency,s13), (_vector,s14), (_delta,s15),
                            (_insideBar,s16), (_fractal,s17), (_rsiDiv,s18), (_squeeze,s19), (_pattern,s20)
                        };
                        _logManager.RecordEntry(TradingSignal.Buy, score, currentRealtimePrice, closedCandles, results);
                        AddLog($"[LONG] {_selectedSymbol} Score {score}");
                    }
                    else if (score <= -7 && adxFilterPass) { 
                        Enter(TradingSignal.Sell, currentRealtimePrice); 
                        var results = new List<(IStrategy, TradingSignal)> {
                            (_superTrend,s1), (_ichimoku,s2), (_maCross,s3), (_linReg,s4), (_adx,s5),
                            (_orderBlock,s6), (_fvg,s7), (_vwap,s8), (_whale,s9), (_smartMoney,s10),
                            (_zScore,s11), (_hurst,s12), (_efficiency,s13), (_vector,s14), (_delta,s15),
                            (_insideBar,s16), (_fractal,s17), (_rsiDiv,s18), (_squeeze,s19), (_pattern,s20)
                        };
                        _logManager.RecordEntry(TradingSignal.Sell, score, currentRealtimePrice, closedCandles, results);
                        AddLog($"[SHORT] {_selectedSymbol} Score {score}");
                    }
                } 
                else 
                {
                    var exitSignal = _riskManager.AnalyzeExit(
                        closedCandles, _currentPosition, _entryPrice, currentRealtimePrice, _manualLeverage
                    );

                    bool rev = (_currentPosition == TradingSignal.Buy && score < -3) || 
                               (_currentPosition == TradingSignal.Sell && score > 3);
                    
                    if (rev) { exitSignal.Action = ExitAction.CloseAll; exitSignal.Reason = "Signal Reversal"; }

                    if (exitSignal.Action == ExitAction.CloseAll)
                    {
                        decimal realPnL = CalculateRealPnL(_entryPrice, currentRealtimePrice, _positionAmount, _currentPosition);
                        decimal netRoe = _riskManager.CalculateNetRoe(_entryPrice, currentRealtimePrice, _currentPosition, _manualLeverage);

                        _virtualBalance += realPnL;
                        AddLog($"[CLOSED] {exitSignal.Reason} PnL:${realPnL:F2}");

                        _logManager.RecordExit(_currentPosition, _entryPrice, currentRealtimePrice, realPnL, netRoe, exitSignal.Reason);

                        var record = new TradeRecord {
                            IsLive = false, Type = _currentPosition, EntryPrice = _entryPrice, ExitPrice = currentRealtimePrice, Amount = _positionAmount, PnL = realPnL, Roe = netRoe, Leverage = _manualLeverage, ExitReason = exitSignal.Reason, EntryTime = DateTime.Now, ExitTime = DateTime.Now,
                            Title = $"[{DateTime.Now:HH:mm}] {_selectedSymbol} {(_currentPosition==TradingSignal.Buy?"L":"S")} ${realPnL:+0.0;-0.0}"
                        };
                        Dispatcher.UIThread.Post(() => { _tradeHistory.Insert(1, record); });
                        
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
                        
                        AddLog($"[PARTIAL] {exitSignal.Reason} PnL:${realPnL:F2}");
                        _logManager.RecordExit(_currentPosition, _entryPrice, currentRealtimePrice, realPnL, netRoe, $"PARTIAL: {exitSignal.Reason}");

                        var record = new TradeRecord {
                            IsLive = false, Type = _currentPosition, EntryPrice = _entryPrice, ExitPrice = currentRealtimePrice, Amount = closeAmount, PnL = realPnL, Roe = 0, Leverage = _manualLeverage, ExitReason = "Partial", EntryTime = DateTime.Now, ExitTime = DateTime.Now,
                            Title = $"[{DateTime.Now:HH:mm}] PARTIAL ${realPnL:+0.0;-0.0}"
                        };
                        Dispatcher.UIThread.Post(() => { _tradeHistory.Insert(1, record); });
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() => {
                    decimal realPnL = 0;
                    if (_currentPosition != TradingSignal.Hold)
                         realPnL = CalculateRealPnL(_entryPrice, currentRealtimePrice, _positionAmount, _currentPosition);
                    
                    PriceText.Text = $"${currentRealtimePrice:0.0000}";
                    BalanceText.Text = $"${_virtualBalance + realPnL:N0}";

                    if (!_isViewingHistory) 
                    {
                        if (_currentPosition == TradingSignal.Hold)
                        {
                            UpdatePositionCardLiveState();
                            double cooldownLeft = 60 - (DateTime.Now - _lastExitTime).TotalSeconds;
                            if (cooldownLeft > 0) { FinalDecision.Text = $"COOLDOWN ({cooldownLeft:F0}s)"; FinalDecision.Foreground = Brushes.Orange; }
                            else { 
                                FinalDecision.Text = score > 0 ? $"BULLISH ({score})" : (score < 0 ? $"BEARISH ({score})" : "WAITING"); 
                                FinalDecision.Foreground = score > 0 ? Brushes.LightGreen : (score < 0 ? Brushes.Red : Brushes.Gray); 
                            }
                        }
                        else
                        {
                            SymbolCombo.IsEnabled = false; 

                            bool isLong = _currentPosition == TradingSignal.Buy;
                            PosType.Text = isLong ? "LONG (ACTIVE)" : "SHORT (ACTIVE)";
                            PosType.Foreground = isLong ? Brushes.LightGreen : Brushes.Red;
                            
                            decimal netRoe = _riskManager.CalculateNetRoe(_entryPrice, currentRealtimePrice, _currentPosition, _manualLeverage);

                            PosPnl.Text = $"{realPnL:+$#,##0.00;-$#,##0.00}";
                            PosPnl.Foreground = realPnL >= 0 ? Brushes.LightGreen : Brushes.Red;
                            PosRoe.Text = $"{netRoe:+0.00;-0.00}%";
                            PosRoe.Foreground = realPnL >= 0 ? Brushes.LightGreen : Brushes.Red;

                            PosEntry.Text = $"${_entryPrice:0.0000}"; PosMark.Text = $"${currentRealtimePrice:0.0000}";
                            PosLev.Text = $"{_manualLeverage}x";
                            
                            decimal liqPrice = isLong ? _entryPrice * (1 - (1 / _manualLeverage)) : _entryPrice * (1 + (1 / _manualLeverage));
                            PosLiq.Text = $"${liqPrice:0.0000}";
                            PosSize.Text = $"{(_entryPrice*_positionAmount):N0} USDT";
                            PosMargin.Text = $"{((_entryPrice*_positionAmount)/_manualLeverage):N2} USDT";
                            
                            decimal slRoe = _riskManager.CurrentSlPercent * _manualLeverage;
                            decimal tpRoe = _riskManager.CurrentTpPercent * _manualLeverage;
                            PosSl.Text = $"AUTO (-{slRoe:F0}%)";
                            PosTp.Text = $"AUTO (+{tpRoe:F0}%)";

                            FinalDecision.Text = "IN POSITION";
                            FinalDecision.Foreground = Brushes.White;
                        }
                    }

                    Upd(CardSuperTrend, ValSuperTrend, SigSuperTrend, s1, _superTrend);
                    Upd(CardIchimoku, ValIchimoku, SigIchimoku, s2, _ichimoku);
                    Upd(CardMa, ValMa, SigMa, s3, _maCross);
                    Upd(CardLinReg, ValLinReg, SigLinReg, s4, _linReg);
                    Upd(CardAdx, ValAdx, SigAdx, s5, _adx);
                    
                    Upd(CardOrderBlock, ValOrderBlock, SigOrderBlock, s6, _orderBlock);
                    Upd(CardFvg, ValFvg, SigFvg, s7, _fvg);
                    Upd(CardVwap, ValVwap, SigVwap, s8, _vwap);
                    Upd(CardWhale, ValWhale, SigWhale, s9, _whale);
                    Upd(CardSmartMoney, ValSmartMoney, SigSmartMoney, s10, _smartMoney);
                    
                    Upd(CardZScore, ValZScore, SigZScore, s11, _zScore);
                    Upd(CardHurst, ValHurst, SigHurst, s12, _hurst);
                    Upd(CardEfficiency, ValEfficiency, SigEfficiency, s13, _efficiency);
                    Upd(CardVector, ValVector, SigVector, s14, _vector);
                    Upd(CardDelta, ValDelta, SigDelta, s15, _delta);
                    
                    Upd(CardInsideBar, ValInsideBar, SigInsideBar, s16, _insideBar);
                    Upd(CardFractal, ValFractal, SigFractal, s17, _fractal);
                    Upd(CardRsiDiv, ValRsiDiv, SigRsiDiv, s18, _rsiDiv);
                    Upd(CardSqueeze, ValSqueeze, SigSqueeze, s19, _squeeze);
                    Upd(CardPattern, ValPattern, SigPattern, s20, _pattern);
                });
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
            await Task.Delay(1000);
        }
    }

    private void Enter(TradingSignal s, decimal p) {
        _currentPosition = s; _entryPrice = p; _positionAmount = (_virtualBalance * _manualLeverage) / p;
        _riskManager.OnEntry(p);
        _riskManager.UpdateDynamicSettings(_selectedInterval, _manualLeverage, _selectedSymbol);
    }

    private int Sc(TradingSignal s, int w) => s == TradingSignal.Buy ? w : (s == TradingSignal.Sell ? -w : 0);
    
    private void Upd(Border card, TextBlock valTxt, TextBlock sigTxt, TradingSignal s, IStrategy strat) 
    {
        sigTxt.Text = s == TradingSignal.Buy ? "BUY" : (s == TradingSignal.Sell ? "SELL" : "HOLD");
        sigTxt.Foreground = s == TradingSignal.Buy ? Brushes.LightGreen : (s == TradingSignal.Sell ? Brushes.Red : Brushes.Gray);
        valTxt.Text = strat.GetStatusValue(); 
        if (s == TradingSignal.Buy) { card.Background = SolidColorBrush.Parse("#2000FF00"); card.BorderBrush = Brushes.LightGreen; }
        else if (s == TradingSignal.Sell) { card.Background = SolidColorBrush.Parse("#20FF0000"); card.BorderBrush = Brushes.Red; }
        else { card.Background = SolidColorBrush.Parse("#0F172A"); card.BorderBrush = SolidColorBrush.Parse("#334155"); }
    }
}