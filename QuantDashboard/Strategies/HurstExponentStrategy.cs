using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class HurstExponentStrategy : IStrategy
{
    public string Name => "허스트 지수 (Market Regime)";
    private string _status = "Hurst: -";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 30) return TradingSignal.Hold;

        double h = CalculateHurst(candles);

        // ★ 상태 업데이트: 시장 성격 표시
        // 0.5 근처: 랜덤워크, >0.6: 추세장, <0.4: 역추세장
        string type = h > 0.6 ? "Trend" : (h < 0.4 ? "MeanRev" : "Random");
        _status = $"H: {h:F2} ({type})";

        // 랜덤 구간(0.4~0.6)은 매매 금지
        if (h > 0.4 && h < 0.6) return TradingSignal.Hold;

        var last = candles.Last();
        var ma20 = candles.Skip(candles.Count - 20).Average(c => c.ClosePrice);

        // 추세장 (>0.6): 추세 추종
        if (h >= 0.6)
        {
            if (last.ClosePrice > ma20) return TradingSignal.Buy;
            else return TradingSignal.Sell;
        }

        // 역추세장 (<0.4): 평균 회귀
        if (h <= 0.4)
        {
            if (last.ClosePrice > ma20 * 1.02m) return TradingSignal.Sell;
            else if (last.ClosePrice < ma20 * 0.98m) return TradingSignal.Buy;
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;

    private double CalculateHurst(List<IBinanceKline> candles)
    {
        // 간소화된 R/S 분석
        var data = candles.Skip(candles.Count - 30).ToList();
        int n = data.Count;
        double mean = (double)data.Average(c => c.ClosePrice);
        
        double sumSqDiff = data.Sum(c => Math.Pow((double)c.ClosePrice - mean, 2));
        double stdDev = Math.Sqrt(sumSqDiff / n);

        double maxDev = 0, minDev = 0, currentDev = 0;
        foreach (var c in data)
        {
            currentDev += ((double)c.ClosePrice - mean);
            if (currentDev > maxDev) maxDev = currentDev;
            if (currentDev < minDev) minDev = currentDev;
        }
        
        if (stdDev == 0) return 0.5;
        
        double rs = (maxDev - minDev) / stdDev;
        return Math.Log(rs) / Math.Log(n / 2.0); 
    }
}