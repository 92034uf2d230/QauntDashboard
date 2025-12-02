using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class LinRegStrategy : IStrategy
{
    public string Name => "선형 회귀 + RSI (Smart Trend)";
    
    private const int LookbackPeriod = 20; 
    private const double SlopeThreshold = 0.15; // 기울기 임계값
    private const int RsiPeriod = 14;           // RSI 표준 기간
    
    private string _status = "Initializing..."; // 상태 표시용 변수

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        // 데이터가 충분치 않으면 대기 (기울기 20개 + RSI 계산용 여유분 필요)
        if (candles.Count < LookbackPeriod + RsiPeriod) 
        {
            _status = "Loading...";
            return TradingSignal.Hold;
        }

        // 1. 기울기 계산 (최근 20개)
        var recentData = candles.Skip(candles.Count - LookbackPeriod).ToList();
        double slope = CalculateSlope(recentData);

        // 2. RSI 계산 (현재 시점)
        double currentRsi = CalculateRSI(candles, RsiPeriod);

        // ★ [핵심] 상태 업데이트 (UI 표시용)
        // 예: "Slope: 0.25 | RSI: 62.5"
        _status = $"Slope: {slope:F2} | RSI: {currentRsi:F0}";

        // 3. 하이브리드 진입 로직
        // 롱(Long): 기울기가 상승세(>0.15) + RSI가 너무 고점이 아닐 때 (<70)
        if (slope > SlopeThreshold && currentRsi < 70) 
            return TradingSignal.Buy;
        
        // 숏(Short): 기울기가 하락세(<-0.15) + RSI가 너무 바닥이 아닐 때 (>30)
        if (slope < -SlopeThreshold && currentRsi > 30) 
            return TradingSignal.Sell;

        return TradingSignal.Hold; 
    }

    // UI에서 호출할 함수
    public string GetStatusValue() => _status;

    // 기울기 계산 (정규화 버전)
    private double CalculateSlope(List<IBinanceKline> data)
    {
        int n = data.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        double baselinePrice = (double)data[0].ClosePrice;

        for (int i = 0; i < n; i++)
        {
            double x = i; 
            // 가격을 퍼센트(%)로 정규화하여 기울기 계산
            double y = ((double)data[i].ClosePrice - baselinePrice) / baselinePrice * 100;
            sumX += x; sumY += y; sumXY += (x * y); sumX2 += (x * x);
        }
        return (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
    }

    // RSI 계산 함수 (Wilder's Smoothing 적용)
    private double CalculateRSI(List<IBinanceKline> data, int period)
    {
        if (data.Count < period + 1) return 50; 

        double u = 0, d = 0;
        
        // 첫 RSI 평균
        for (int i = 1; i <= period; i++)
        {
            double diff = (double)(data[i].ClosePrice - data[i - 1].ClosePrice);
            if (diff > 0) u += diff;
            else d -= diff;
        }
        
        double au = u / period;
        double ad = d / period;

        // 이후 스무딩
        for (int i = period + 1; i < data.Count; i++)
        {
            double diff = (double)(data[i].ClosePrice - data[i - 1].ClosePrice);
            if (diff > 0)
            {
                au = (au * (period - 1) + diff) / period;
                ad = (ad * (period - 1) + 0) / period;
            }
            else
            {
                au = (au * (period - 1) + 0) / period;
                ad = (ad * (period - 1) - diff) / period;
            }
        }

        if (ad == 0) return 100;
        double rs = au / ad;
        return 100 - (100 / (1 + rs));
    }
}