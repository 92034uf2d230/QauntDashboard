using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class RsiDivergenceStrategy : IStrategy
{
    public string Name => "RSI 다이버전스 (Reversal)";
    private const int Period = 14;
    private string _status = "RSI: -";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 30) return TradingSignal.Hold;

        var rsiValues = CalculateRsiList(candles);
        double currentRsi = rsiValues.Last();

        // ★ 기본 상태 업데이트
        _status = $"RSI: {currentRsi:F1}";
        
        int lookback = 5; 

        var lastPriceLow = candles.Skip(candles.Count - lookback).Min(c => c.LowPrice);
        var lastRsiLow = rsiValues.Skip(rsiValues.Count - lookback).Min();
        
        var prevPriceLow = candles.Skip(candles.Count - lookback - 15).Take(15).Min(c => c.LowPrice);
        var prevRsiLow = rsiValues.Skip(rsiValues.Count - lookback - 15).Take(15).Min();

        // 1. 상승 다이버전스
        if (lastPriceLow < prevPriceLow && lastRsiLow > prevRsiLow)
        {
            if (lastRsiLow < 40) 
            {
                _status = $"Bull Div (RSI {currentRsi:F0})";
                return TradingSignal.Buy;
            }
        }

        // 2. 하락 다이버전스
        var lastPriceHigh = candles.Skip(candles.Count - lookback).Max(c => c.HighPrice);
        var lastRsiHigh = rsiValues.Skip(rsiValues.Count - lookback).Max();
        
        var prevPriceHigh = candles.Skip(candles.Count - lookback - 15).Take(15).Max(c => c.HighPrice);
        var prevRsiHigh = rsiValues.Skip(rsiValues.Count - lookback - 15).Take(15).Max();

        if (lastPriceHigh > prevPriceHigh && lastRsiHigh < prevRsiHigh)
        {
            if (lastRsiHigh > 60) 
            {
                _status = $"Bear Div (RSI {currentRsi:F0})";
                return TradingSignal.Sell;
            }
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;

    private List<double> CalculateRsiList(List<IBinanceKline> candles)
    {
        var rsiList = new List<double>();
        double u = 0, d = 0;
        
        for (int i = 1; i <= Period; i++) {
            double diff = (double)(candles[i].ClosePrice - candles[i - 1].ClosePrice);
            if (diff > 0) u += diff; else d -= diff;
        }
        double au = u / Period, ad = d / Period;
        rsiList.Add(100 - (100 / (1 + au / (ad == 0 ? 1 : ad))));

        for (int i = Period + 1; i < candles.Count; i++) {
            double diff = (double)(candles[i].ClosePrice - candles[i - 1].ClosePrice);
            if (diff > 0) { au = (au * (Period - 1) + diff) / Period; ad = (ad * (Period - 1)) / Period; }
            else { au = (au * (Period - 1)) / Period; ad = (ad * (Period - 1) - diff) / Period; }
            rsiList.Add(100 - (100 / (1 + au / (ad == 0 ? 1 : ad))));
        }
        return rsiList;
    }
}