using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Binance.Net.Enums;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using QuantDashboard.Enums;
using QuantDashboard.Engine;
using QuantDashboard.Managers;
using QuantDashboard.Models;

namespace QuantDashboard;

public partial class MainWindow : Window
{
    private RiskManager _riskManager;
    private LogManager _logManager;
    private TradingEngine _engine;

    private List<string> _logLines = new();
    private ObservableCollection<TradeRecord> _tradeHistory = new();

    public MainWindow()
    {
        InitializeComponent();

        _riskManager = new RiskManager();
        _logManager = new LogManager();
        _engine = new TradingEngine(_riskManager, _logManager);

        // 엔진 이벤트 연결
        _engine.OnLog = AddLog;
        _engine.OnStatusUpdate = OnEngineStatusUpdate;

        // 히스토리 공유
        _tradeHistory = _engine.TradeHistory;
        InitializeHistory();
        HistoryCombo.SelectionChanged += OnHistorySelectionChanged;

        // UI 이벤트
        LevSlider.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "Value")
            {
                _engine.ManualLeverage = (decimal)LevSlider.Value;
                if (LevText != null) LevText.Text = $"{_engine.ManualLeverage:F0}x";
                UpdateRiskSettings();
            }
        };

        TimeframeCombo.SelectionChanged += (s, e) =>
        {
            OnTimeframeChanged(s, e);
            UpdateRiskSettings();
        };

        SymbolCombo.SelectionChanged += (s, e) =>
        {
            if (SymbolCombo.SelectedItem is ComboBoxItem item)
            {
                string newSymbol = item.Content?.ToString() ?? "BTCUSDT";
                if (_engine.CurrentPosition != TradingSignal.Hold) return;

                if (_engine.SelectedSymbol != newSymbol)
                {
                    _engine.SelectedSymbol = newSymbol;
                    AddLog($"[SYSTEM] Target Coin Changed: {_engine.SelectedSymbol}");
                    UpdateRiskSettings();
                }
            }
        };

        UpdateRiskSettings();

        AddLog("System Initialized...");
        AddLog($"Log File: {_logManager.CurrentLogPath}");

        _engine.Start();
    }

    private void AddLog(string msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _logLines.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
            if (_logLines.Count > 50) _logLines.RemoveAt(_logLines.Count - 1);
            LogText.Text = string.Join("\n", _logLines);
        });
    }

    private async void OnRunBacktest(object? sender, RoutedEventArgs e)
    {
        AddLog("[SYSTEM] Starting 1-Year Backtest... (Please Wait)");

        try
        {
            var backtester = new BacktestManager();
            var result = await Task.Run(() => backtester.RunBacktestAsync(
                _engine.SelectedSymbol,
                _engine.SelectedInterval,
                _engine.VirtualBalance,
                _engine.ManualLeverage
            ));

            AddLog("=== BACKTEST DONE ===");
            AddLog($"Final Balance: ${result.FinalBalance:N0}");
            AddLog($"Total PnL: {result.TotalPnL:+0;-0} ({result.TotalPnL / _engine.VirtualBalance * 100:F1}%)");
            AddLog($"Win/Loss: {result.WinCount}W / {result.LossCount}L");
            AddLog($"Max Drawdown: -{result.MaxDrawdown:F2}%");

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string path = Path.Combine(desktop, $"backtest_{_engine.SelectedSymbol}.txt");
            File.WriteAllText(path, result.Log);
            AddLog($"Report saved: {path}");
        }
        catch (Exception ex)
        {
            AddLog($"[Backtest Error] {ex.Message}");
        }
    }

    private void UpdateRiskSettings()
    {
        _riskManager.UpdateDynamicSettings(_engine.SelectedInterval, _engine.ManualLeverage, _engine.SelectedSymbol);

        Dispatcher.UIThread.Post(() =>
        {
            if (_engine.CurrentPosition == TradingSignal.Hold)
            {
                decimal slRoe = _riskManager.CurrentSlPercent * _engine.ManualLeverage;
                decimal tpRoe = _riskManager.CurrentTpPercent * _engine.ManualLeverage;

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
            if (record.IsLive)
            {
                _engine.IsViewingHistory = false;
                UpdatePositionCardLiveState();
            }
            else
            {
                _engine.IsViewingHistory = true;
                DisplayHistoricalRecord(record);
            }
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
        PosSize.Text = $"${(r.EntryPrice * r.Amount):N0}";
        PosMargin.Text = $"${(r.EntryPrice * r.Amount / r.Leverage):N2}";
        PosLev.Text = $"{r.Leverage:F0}x";
        PosTp.Text = $"EXIT: {r.ExitReason}";
        PosSl.Text = $"{r.ExitTime:HH:mm:ss}";
    }

    private void UpdatePositionCardLiveState()
    {
        if (_engine.CurrentPosition == TradingSignal.Hold)
        {
            PosType.Text = "NO POSITION";
            PosType.Foreground = Brushes.Gray;
            PosBadge.Background = SolidColorBrush.Parse("#20FFFFFF");
            PosLev.Text = "";
            PosRoe.Text = "0.00%";
            PosRoe.Foreground = Brushes.Gray;
            PosEntry.Text = "-";
            PosMark.Text = "-";
            PosLiq.Text = "-";
            PosSize.Text = "-";
            PosMargin.Text = "-";
            PosPnl.Text = "$0.00";
            PosPnl.Foreground = Brushes.Gray;
            SymbolCombo.IsEnabled = true;
            UpdateRiskSettings();
        }
    }

    private void OnTimeframeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TimeframeCombo.SelectedItem is ComboBoxItem item)
        {
            string tag = item.Content?.ToString() ?? "5m";
            _engine.SelectedInterval = tag switch
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
    }

    private void OnWindowDrag(object? s, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnMinimize(object? s, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object? s, RoutedEventArgs e) =>
        WindowState = WindowState.Maximized == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object? s, RoutedEventArgs e) => Close();

    // 엔진 상태 콜백
    private void OnEngineStatusUpdate(int score, decimal currentRealtimePrice, decimal realPnL)
    {
        Dispatcher.UIThread.Post(() =>
        {
            PriceText.Text = $"${currentRealtimePrice:0.0000}";
            BalanceText.Text = $"${_engine.VirtualBalance + realPnL:N0}";

            if (!_engine.IsViewingHistory)
            {
                if (_engine.CurrentPosition == TradingSignal.Hold)
                {
                    UpdatePositionCardLiveState();
                    double cooldownLeft = 60 - (DateTime.Now - _engine.LastExitTime).TotalSeconds;
                    if (cooldownLeft > 0)
                    {
                        FinalDecision.Text = $"COOLDOWN ({cooldownLeft:F0}s)";
                        FinalDecision.Foreground = Brushes.Orange;
                    }
                    else
                    {
                        FinalDecision.Text = score > 0
                            ? $"BULLISH ({score})"
                            : (score < 0 ? $"BEARISH ({score})" : "WAITING");
                        FinalDecision.Foreground = score > 0
                            ? Brushes.LightGreen
                            : (score < 0 ? Brushes.Red : Brushes.Gray);
                    }
                }
                else
                {
                    SymbolCombo.IsEnabled = false;

                    bool isLong = _engine.CurrentPosition == TradingSignal.Buy;
                    PosType.Text = isLong ? "LONG (ACTIVE)" : "SHORT (ACTIVE)";
                    PosType.Foreground = isLong ? Brushes.LightGreen : Brushes.Red;

                    decimal netRoe = _riskManager.CalculateNetRoe(_engine.EntryPrice, currentRealtimePrice, _engine.CurrentPosition, _engine.ManualLeverage);

                    PosPnl.Text = $"{realPnL:+$#,##0.00;-$#,##0.00}";
                    PosPnl.Foreground = realPnL >= 0 ? Brushes.LightGreen : Brushes.Red;
                    PosRoe.Text = $"{netRoe:+0.00;-0.00}%";
                    PosRoe.Foreground = realPnL >= 0 ? Brushes.LightGreen : Brushes.Red;

                    PosEntry.Text = $"${_engine.EntryPrice:0.0000}";
                    PosMark.Text = $"${currentRealtimePrice:0.0000}";
                    PosLev.Text = $"{_engine.ManualLeverage}x";

                    decimal slRoe = _riskManager.CurrentSlPercent * _engine.ManualLeverage;
                    decimal tpRoe = _riskManager.CurrentTpPercent * _engine.ManualLeverage;
                    PosSl.Text = $"AUTO (-{slRoe:F0}%)";
                    PosTp.Text = $"AUTO (+{tpRoe:F0}%)";

                    FinalDecision.Text = "IN POSITION";
                    FinalDecision.Foreground = Brushes.White;
                }
            }
        });
    }
}