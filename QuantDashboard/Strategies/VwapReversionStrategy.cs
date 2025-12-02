using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class VwapReversionStrategy : IStrategy
{
    public string Name => "VWAP 평균 회귀 (Institutions)";
    private const int Period = 24; 
    private string _status = "-";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < Period) return TradingSignal.Hold;

        var data = candles.Skip(candles.Count - Period).ToList();
        
        decimal totalPV = 0; 
        decimal totalVolume = 0;

        foreach (var c in data)
        {
            decimal typicalPrice = (c.HighPrice + c.LowPrice + c.ClosePrice) / 3;
            totalPV += typicalPrice * c.Volume;
            totalVolume += c.Volume;
        }

        if (totalVolume == 0) return TradingSignal.Hold;

        decimal vwap = totalPV / totalVolume;
        decimal currentPrice = candles.Last().ClosePrice;

        // 2. 이격도 계산
        decimal disparity = ((currentPrice - vwap) / vwap) * 100;

        // ★ 상태 업데이트: 이격도 표시 (예: "Disp: -1.2%")
        _status = $"Disp: {disparity:F2}%";

        // 3. 진입 로직
        if (disparity < -3.0m) 
        {
            if (candles.Last().ClosePrice > candles.Last().OpenPrice)
                return TradingSignal.Buy;
        }

        if (disparity > 3.0m)
        {
            if (candles.Last().ClosePrice < candles.Last().OpenPrice)
                return TradingSignal.Sell;
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}