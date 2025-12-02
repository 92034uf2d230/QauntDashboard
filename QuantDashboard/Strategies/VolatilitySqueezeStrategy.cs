using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class VolatilitySqueezeStrategy : IStrategy
{
    public string Name => "폭풍전야 (Volatility Squeeze)";
    private string _status = "Width: -";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 20) return TradingSignal.Hold;

        // 볼린저 밴드 계산
        var recentData = candles.Skip(candles.Count - 20).ToList();
        double avg = (double)recentData.Average(c => c.ClosePrice);
        double sumSquares = recentData.Sum(c => Math.Pow((double)c.ClosePrice - avg, 2));
        double stdDev = Math.Sqrt(sumSquares / 20);
        
        // 밴드 폭 (너비)
        double bandWidth = (stdDev * 4) / avg; 

        // ★ 상태 업데이트: 밴드 폭 표시
        // 1.5% 미만이면 (Squeeze) 표시 추가
        string sqz = bandWidth < 0.015 ? "(Squeeze)" : "";
        _status = $"Width: {bandWidth*100:F2}% {sqz}";

        // 2. 진입 로직
        if (bandWidth < 0.015) 
        {
            var last = candles.Last();
            if (last.ClosePrice > (decimal)avg && last.ClosePrice > last.OpenPrice)
                return TradingSignal.Buy;
            
            else if (last.ClosePrice < (decimal)avg && last.ClosePrice < last.OpenPrice)
                return TradingSignal.Sell;
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}