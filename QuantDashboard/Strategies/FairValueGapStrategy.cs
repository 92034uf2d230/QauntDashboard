using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class FairValueGapStrategy : IStrategy
{
    public string Name => "FVG 갭 채움 (Gap Fill)";
    private string _status = "No Gap";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 5) return TradingSignal.Hold;

        var currentPrice = candles.Last().ClosePrice;
        bool gapFound = false;

        // 최근 10개 봉 내에서 발생한 FVG 찾기
        for (int i = candles.Count - 2; i >= candles.Count - 10; i--)
        {
            var c1 = candles[i - 2]; 
            var c3 = candles[i];     

            // 1. 상승 FVG
            if (c1.HighPrice < c3.LowPrice) 
            {
                decimal gapTop = c3.LowPrice;
                decimal gapBottom = c1.HighPrice;
                
                // ★ 상태 업데이트
                _status = $"Bull Gap ${gapBottom:F0}";
                gapFound = true;

                if (currentPrice <= gapTop && currentPrice >= gapBottom)
                    return TradingSignal.Buy;
            }

            // 2. 하락 FVG
            if (c1.LowPrice > c3.HighPrice)
            {
                decimal gapTop = c1.LowPrice;
                decimal gapBottom = c3.HighPrice;

                // ★ 상태 업데이트
                _status = $"Bear Gap ${gapTop:F0}";
                gapFound = true;

                if (currentPrice <= gapTop && currentPrice >= gapBottom)
                    return TradingSignal.Sell;
            }
        }

        if (!gapFound) _status = "No Gap";

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}