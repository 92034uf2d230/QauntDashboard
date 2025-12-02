using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class PatternCandleStrategy : IStrategy
{
    public string Name => "캔들 패턴 (Sniper)";
    private string _status = "No Pattern";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 3) return TradingSignal.Hold;

        var candle = candles[candles.Count - 2];
        var prevCandle = candles[candles.Count - 3];

        // 1. 망치형 (Hammer)
        decimal body = Math.Abs(candle.OpenPrice - candle.ClosePrice);
        decimal lowerTail = Math.Min(candle.OpenPrice, candle.ClosePrice) - candle.LowPrice;
        bool isHammer = lowerTail > (body * 2) && (candle.HighPrice - Math.Max(candle.OpenPrice, candle.ClosePrice)) < body;

        // 2. 상승 장악형 (Bullish Engulfing)
        bool isBullishEngulfing = prevCandle.ClosePrice < prevCandle.OpenPrice && 
                                  candle.ClosePrice > candle.OpenPrice &&         
                                  candle.OpenPrice < prevCandle.ClosePrice &&     
                                  candle.ClosePrice > prevCandle.OpenPrice;       

        // ★ 상태 업데이트
        if (isHammer) _status = "Hammer";
        else if (isBullishEngulfing) _status = "Engulfing";
        else _status = "No Pattern";

        if (isHammer || isBullishEngulfing)
        {
            return TradingSignal.Buy;
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}