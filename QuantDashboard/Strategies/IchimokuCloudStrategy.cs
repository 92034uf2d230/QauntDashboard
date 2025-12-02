using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class IchimokuCloudStrategy : IStrategy
{
    public string Name => "일목균형표 (Ichimoku Cloud)";
    private string _status = "Initializing...";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 52) return TradingSignal.Hold;

        // 전환선(9), 기준선(26), 선행스팬B(52)
        decimal tenkan = (GetHigh(candles, 9) + GetLow(candles, 9)) / 2;
        decimal kijun = (GetHigh(candles, 26) + GetLow(candles, 26)) / 2;
        decimal spanA = (tenkan + kijun) / 2;
        decimal spanB = (GetHigh(candles, 52) + GetLow(candles, 52)) / 2;

        var close = candles.Last().ClosePrice;

        // ★ 상태 업데이트: 구름대와의 위치 관계
        decimal cloudTop = Math.Max(spanA, spanB);
        decimal cloudBottom = Math.Min(spanA, spanB);

        if (close > cloudTop) 
        {
            decimal diff = ((close - cloudTop) / cloudTop) * 100;
            _status = $"Above (+{diff:F2}%)";
        }
        else if (close < cloudBottom)
        {
            decimal diff = ((cloudBottom - close) / cloudBottom) * 100;
            _status = $"Below (-{diff:F2}%)";
        }
        else
        {
            _status = "In Cloud";
        }

        // 매매 신호
        if (close > cloudTop && tenkan > kijun) return TradingSignal.Buy;
        if (close < cloudBottom && tenkan < kijun) return TradingSignal.Sell;

        return TradingSignal.Hold;
    }

    public string GetStatusValue() => _status;

    private decimal GetHigh(List<IBinanceKline> c, int p) => c.Skip(c.Count - p).Max(x => x.HighPrice);
    private decimal GetLow(List<IBinanceKline> c, int p) => c.Skip(c.Count - p).Min(x => x.LowPrice);
}