using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class MaCrossStrategy : IStrategy
{
    public string Name => "이동평균선 (MA Cross)";
    private string _status = "-";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 20) return TradingSignal.Hold;

        var ma20 = candles.Skip(candles.Count - 20).Average(c => c.ClosePrice);
        var currentPrice = candles.Last().ClosePrice;

        // ★ 상태 업데이트: MA와의 이격도 표시
        // 양수면 가격이 MA보다 위에 있음, 음수면 아래에 있음
        decimal gap = ((currentPrice - ma20) / ma20) * 100;
        _status = $"Gap: {gap:F2}%";

        if (currentPrice > ma20) return TradingSignal.Buy;
        else if (currentPrice < ma20) return TradingSignal.Sell;

        return TradingSignal.Hold;
    }

    public string GetStatusValue() => _status;
}