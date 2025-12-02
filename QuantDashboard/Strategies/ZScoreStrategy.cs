using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class ZScoreStrategy : IStrategy
{
    public string Name => "Z-Score 통계 (Statistics)";
    private const int Period = 20;
    private string _status = "-";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < Period) return TradingSignal.Hold;

        var data = candles.Skip(candles.Count - Period).ToList();
        
        // 1. 평균 & 표준편차
        double sum = data.Sum(c => (double)c.ClosePrice);
        double mean = sum / Period;

        double sumSquares = data.Sum(c => Math.Pow((double)c.ClosePrice - mean, 2));
        double stdDev = Math.Sqrt(sumSquares / Period);

        if (stdDev == 0) return TradingSignal.Hold;

        // 2. Z-Score 계산
        double currentPrice = (double)candles.Last().ClosePrice;
        double zScore = (currentPrice - mean) / stdDev;

        // ★ 상태 업데이트: 점수 표시
        // 예: "Score: 2.15 (Over)"
        string state = zScore > 2 ? "(Over)" : (zScore < -2 ? "(Under)" : "(Normal)");
        _status = $"Score: {zScore:F2} {state}";

        // 3. 진입 로직
        // 과매수(>2.0) + 음봉 발생
        if (zScore > 2.0)
        {
            if (candles.Last().ClosePrice < candles.Last().OpenPrice)
                return TradingSignal.Sell;
        }

        // 과매도(<-2.0) + 양봉 발생
        if (zScore < -2.0)
        {
            if (candles.Last().ClosePrice > candles.Last().OpenPrice)
                return TradingSignal.Buy;
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}