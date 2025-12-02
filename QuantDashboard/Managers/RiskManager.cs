using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Enums; 
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Managers;

public enum ExitAction { Hold, CloseAll, ClosePartial, MoveStopLoss }

public class ExitSignal
{
    public ExitAction Action { get; set; }
    public string Reason { get; set; }
    public decimal AmountRatio { get; set; }
}

public class RiskManager
{
    public decimal CurrentTpPercent { get; private set; }
    public decimal CurrentSlPercent { get; private set; }
    public decimal CurrentTrailingGap { get; private set; }

    private bool _isPartialClosed = false; 
    private decimal _peakPrice = 0;
    private const decimal FeeRate = 0.0005m; 

    public void OnEntry(decimal entryPrice)
    {
        _isPartialClosed = false;
        _peakPrice = entryPrice;
    }

    public void UpdateDynamicSettings(KlineInterval interval, decimal leverage, string symbol)
    {
        // 1. 시간별 기본 변동성 (Base)
        decimal baseVolatility = interval switch
        {
            KlineInterval.OneMinute => 0.3m,       
            KlineInterval.FiveMinutes => 0.5m,     
            KlineInterval.FifteenMinutes => 0.8m,  
            KlineInterval.OneHour => 1.5m,         
            KlineInterval.FourHour => 3.0m,        
            _ => 0.8m
        };

        // ★ [핵심] 코인 등급별 변동성 가중치 적용 (Tier System)
        decimal volMultiplier = 1.0m; // 기본값 (BTC)

        if (symbol == "BTCUSDT") 
        {
            volMultiplier = 1.0m;
        }
        else if (symbol == "ETHUSDT" || symbol == "BNBUSDT" || symbol == "XRPUSDT" || symbol == "ADAUSDT") 
        {
            volMultiplier = 1.2m; // 메이저 알트: 조금 더 여유 (1.2배)
        }
        else if (symbol == "SOLUSDT" || symbol == "AVAXUSDT") 
        {
            volMultiplier = 1.3m; // 변동성 큰 메이저: (1.3배)
        }
        else 
        {
            volMultiplier = 1.6m; // 도지(DOGE) 등 밈코인: 아주 넉넉하게 (1.6배)
        }

        baseVolatility *= volMultiplier;

        // 2. 레버리지 패널티 (이차함수 효과)
        double dampener = Math.Sqrt((double)leverage); 
        decimal safetyFactor = (decimal)(Math.Sqrt(10.0) / dampener); 
        
        if (safetyFactor > 1.0m) safetyFactor = 1.0m;
        if (safetyFactor < 0.2m) safetyFactor = 0.2m; 

        // 3. 최종 값 설정
        CurrentSlPercent = baseVolatility * safetyFactor;
        CurrentTpPercent = CurrentSlPercent * 2.0m;
        CurrentTrailingGap = CurrentSlPercent * 0.6m;

        // 청산 방지 로직
        decimal liquidationLimit = (100.0m / leverage) * 0.8m;
        if (CurrentSlPercent > liquidationLimit)
        {
            CurrentSlPercent = liquidationLimit;
            CurrentTrailingGap = CurrentSlPercent * 0.5m;
        }
    }

    public ExitSignal AnalyzeExit(List<IBinanceKline> candles, TradingSignal position, decimal entryPrice, decimal currentPrice, decimal manualLeverage)
    {
        var result = new ExitSignal { Action = ExitAction.Hold };
        
        if (position == TradingSignal.Buy && currentPrice > _peakPrice) _peakPrice = currentPrice;
        if (position == TradingSignal.Sell && currentPrice < _peakPrice) _peakPrice = currentPrice;

        decimal priceChangePct = Math.Abs((currentPrice - entryPrice) / entryPrice) * 100;
        decimal pnlDir = (position == TradingSignal.Buy) ? (currentPrice - entryPrice) : (entryPrice - currentPrice);

        if (pnlDir < 0 && priceChangePct >= CurrentSlPercent)
            return new ExitSignal { Action = ExitAction.CloseAll, Reason = $"Auto SL (-{priceChangePct:F2}%)" };

        if (!_isPartialClosed && pnlDir > 0 && priceChangePct >= CurrentTpPercent)
        {
            _isPartialClosed = true;
            return new ExitSignal { Action = ExitAction.ClosePartial, AmountRatio = 0.5m, Reason = $"Auto TP (+{priceChangePct:F2}%)" };
        }

        bool hitStop = false;
        if (position == TradingSignal.Buy)
        {
            decimal trailingPrice = _peakPrice * (1 - CurrentTrailingGap / 100);
            if (_isPartialClosed) {
                decimal breakEven = entryPrice * (1 + FeeRate * 2.5m);
                if (trailingPrice < breakEven) trailingPrice = breakEven;
            }
            if (currentPrice < trailingPrice) hitStop = true;
        }
        else 
        {
            decimal trailingPrice = _peakPrice * (1 + CurrentTrailingGap / 100);
            if (_isPartialClosed) {
                decimal breakEven = entryPrice * (1 - FeeRate * 2.5m);
                if (trailingPrice > breakEven) trailingPrice = breakEven;
            }
            if (currentPrice > trailingPrice) hitStop = true;
        }

        if (hitStop) return new ExitSignal { Action = ExitAction.CloseAll, Reason = "Trailing Stop" };

        return result;
    }

    public decimal CalculateNetRoe(decimal entry, decimal curr, TradingSignal pos, decimal lev)
    {
        if (entry == 0) return 0;
        decimal rawChange = (curr - entry) / entry;
        decimal grossRoe = (pos == TradingSignal.Buy ? rawChange : -rawChange) * lev;
        decimal totalFeeRate = (FeeRate + FeeRate) * lev; 
        return (grossRoe - totalFeeRate) * 100;
    }
}