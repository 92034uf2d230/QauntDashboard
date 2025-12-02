using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class EfficiencyRatioStrategy : IStrategy
{
    public string Name => "효율적 시장 비율 (Noise Filter)";
    private const int Period = 10;
    private string _status = "ER: 0";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < Period + 1) return TradingSignal.Hold;

        var data = candles.Skip(candles.Count - Period - 1).ToList();

        // Direction / Volatility
        decimal netChange = Math.Abs(data.Last().ClosePrice - data[0].ClosePrice);
        decimal sumOfChanges = 0;
        for (int i = 1; i < data.Count; i++)
            sumOfChanges += Math.Abs(data[i].ClosePrice - data[i - 1].ClosePrice);

        if (sumOfChanges == 0) return TradingSignal.Hold;

        decimal er = netChange / sumOfChanges;

        // ★ 상태 업데이트: 효율성 비율 표시
        // 0.6 이상이면 "Clean", 아니면 "Noisy"
        string quality = er > 0.6m ? "Clean" : "Noisy";
        _status = $"ER: {er:F2} ({quality})";

        // ER > 0.6 (노이즈 적음) 일 때만 진입
        if (er > 0.6m)
        {
            bool isUp = data.Last().ClosePrice > data[0].ClosePrice;
            return isUp ? TradingSignal.Buy : TradingSignal.Sell;
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}