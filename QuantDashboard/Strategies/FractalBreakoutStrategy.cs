using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class FractalBreakoutStrategy : IStrategy
{
    public string Name => "프랙탈 돌파 (Bill Williams)";
    private string _status = "-";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 10) return TradingSignal.Hold;

        var currentPrice = candles.Last().ClosePrice;
        
        // 1. 상단 프랙탈 (Up Fractal) 찾기
        decimal lastFractalHigh = 0;
        for (int i = candles.Count - 3; i >= 2; i--)
        {
            var center = candles[i];
            if (center.HighPrice > candles[i-1].HighPrice && 
                center.HighPrice > candles[i-2].HighPrice &&
                center.HighPrice > candles[i+1].HighPrice && 
                center.HighPrice > candles[i+2].HighPrice)
            {
                lastFractalHigh = center.HighPrice;
                break; 
            }
        }

        // 2. 하단 프랙탈 (Down Fractal) 찾기
        decimal lastFractalLow = 0;
        for (int i = candles.Count - 3; i >= 2; i--)
        {
            var center = candles[i];
            if (center.LowPrice < candles[i-1].LowPrice && 
                center.LowPrice < candles[i-2].LowPrice &&
                center.LowPrice < candles[i+1].LowPrice && 
                center.LowPrice < candles[i+2].LowPrice)
            {
                lastFractalLow = center.LowPrice;
                break;
            }
        }

        // ★ 상태 업데이트: 중요 레벨 표시
        // 예: "R: $92000 S: $91500"
        _status = $"R:${lastFractalHigh:F0} S:${lastFractalLow:F0}";

        // 3. 진입 로직
        if (lastFractalHigh > 0 && currentPrice > lastFractalHigh)
            return TradingSignal.Buy;

        if (lastFractalLow > 0 && currentPrice < lastFractalLow)
            return TradingSignal.Sell;

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}