using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class AdxFilterStrategy : IStrategy
{
    public string Name => "ADX 추세 강도 (Filter)";
    private const int Period = 14;
    
    // 상태 표시용 변수
    private string _status = "Initializing..."; 

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < Period * 2) 
        {
            _status = "Loading...";
            return TradingSignal.Hold;
        }

        // ADX 계산 (Wilder's Smoothing)
        // 원본 로직 유지: DX를 ADX 대용으로 사용하여 민감도 확보
        var (adx, plusDI, minusDI) = CalculateADX(candles);

        // ★ [핵심] 상태 업데이트 (UI 표시용)
        string trendState = "Weak";
        if (adx >= 20)
        {
            if (plusDI > minusDI) trendState = "Bull";
            else if (minusDI > plusDI) trendState = "Bear";
        }
        
        // 예: "ADX: 15.4 (Weak)" 또는 "ADX: 32.1 (Bull)"
        _status = $"ADX: {adx:F1} ({trendState})";

        // 1. 필터링: 추세가 너무 약하면(20 미만) 매매 금지
        if (adx < 20) 
        {
            return TradingSignal.Hold; 
        }

        // 2. 추세가 강할 때 방향 제시
        // +DI가 -DI보다 높으면 상승세
        if (plusDI > minusDI && adx > 25) return TradingSignal.Buy;
        
        // -DI가 +DI보다 높으면 하락세
        if (minusDI > plusDI && adx > 25) return TradingSignal.Sell;

        return TradingSignal.Hold;
    }

    // UI에서 호출할 함수
    public string GetStatusValue() => _status;

    private (double ADX, double PlusDI, double MinusDI) CalculateADX(List<IBinanceKline> candles)
    {
        // ADX 계산은 복잡하므로, 가장 최근 데이터 기준으로 약식 계산 (DX 사용)
        
        double trSum = 0;
        double plusDmSum = 0;
        double minusDmSum = 0;

        // 데이터 슬라이싱
        var data = candles.Skip(candles.Count - Period - 1).ToList();

        for (int i = 1; i <= Period; i++)
        {
            double highDiff = (double)(data[i].HighPrice - data[i-1].HighPrice);
            double lowDiff = (double)(data[i-1].LowPrice - data[i].LowPrice);
            
            double tr = Math.Max((double)(data[i].HighPrice - data[i].LowPrice), 
                        Math.Max(Math.Abs((double)data[i].HighPrice - (double)data[i-1].ClosePrice), 
                                 Math.Abs((double)data[i].LowPrice - (double)data[i-1].ClosePrice)));

            double plusDm = (highDiff > lowDiff && highDiff > 0) ? highDiff : 0;
            double minusDm = (lowDiff > highDiff && lowDiff > 0) ? lowDiff : 0;

            trSum += tr;
            plusDmSum += plusDm;
            minusDmSum += minusDm;
        }

        if (trSum == 0) return (0, 0, 0);

        double plusDI = (plusDmSum / trSum) * 100;
        double minusDI = (minusDmSum / trSum) * 100;
        
        double div = plusDI + minusDI;
        double dx = (div == 0) ? 0 : Math.Abs(plusDI - minusDI) / div * 100;
        
        // 원본 로직대로 DX를 반환
        return (dx, plusDI, minusDI);
    }
}