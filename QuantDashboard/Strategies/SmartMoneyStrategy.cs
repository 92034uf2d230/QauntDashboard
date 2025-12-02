using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class SmartMoneyStrategy : IStrategy
{
    public string Name => "세력 매집 포착 (Volume Anomaly)";
    private string _status = "-";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 21) return TradingSignal.Hold;

        var lastCandle = candles.Last();
        var avgVolume = candles.Skip(candles.Count - 21).Take(20).Average(c => c.Volume);
        
        // ★ 상태 업데이트: 거래량 폭발 배수 표시
        if (avgVolume == 0) avgVolume = 1;
        decimal volRatio = lastCandle.Volume / avgVolume;
        
        _status = $"Vol Spike: {volRatio:F1}x";

        var avgRange = candles.Skip(candles.Count - 21).Take(20).Average(c => c.HighPrice - c.LowPrice);
        var currentRange = lastCandle.HighPrice - lastCandle.LowPrice;

        // 거래량 3배 이상 & 캔들 크기는 평균 이하 (매집)
        if (lastCandle.Volume > avgVolume * 3 && currentRange < avgRange)
        {
            var ma20 = candles.Skip(candles.Count - 21).Take(20).Average(c => c.ClosePrice);
            if (lastCandle.ClosePrice > ma20)
            {
                return TradingSignal.Buy;
            }
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}