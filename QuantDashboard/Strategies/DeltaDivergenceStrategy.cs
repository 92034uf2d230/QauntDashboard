using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class DeltaDivergenceStrategy : IStrategy
{
    public string Name => "델타 다이버전스 (Volume Delta)";
    private string _status = "Neutral";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 10) return TradingSignal.Hold;

        var data = candles.Skip(candles.Count - 5).ToList();
        var deltas = new List<decimal>();
        foreach(var c in data)
        {
            decimal delta = c.TakerBuyBaseVolume - (c.Volume - c.TakerBuyBaseVolume);
            deltas.Add(delta);
        }

        bool priceHigherHigh = data.Last().HighPrice > data[0].HighPrice;
        bool deltaLowerHigh = deltas.Last() < deltas[0];

        bool priceLowerLow = data.Last().LowPrice < data[0].LowPrice;
        bool deltaHigherLow = deltas.Last() > deltas[0];

        // ★ 상태 업데이트
        if (priceHigherHigh && deltaLowerHigh)
        {
            _status = "Bear Div (Sell)";
            return TradingSignal.Sell;
        }
        else if (priceLowerLow && deltaHigherLow)
        {
            _status = "Bull Div (Buy)";
            return TradingSignal.Buy;
        }
        else
        {
            // 다이버전스 없음 -> 현재 델타 방향만 표시
            _status = deltas.Last() > 0 ? "Delta +" : "Delta -";
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}