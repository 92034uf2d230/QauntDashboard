using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class SuperTrendStrategy : IStrategy
{
    public string Name => "슈퍼트렌드 (SuperTrend)";
    private string _status = "Initializing...";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 20) return TradingSignal.Hold;

        // ATR 계산 (Period: 10)
        var atr = CalculateAtr(candles, 10);
        decimal multiplier = 3.0m;
        
        var last = candles.Last();
        decimal hl2 = (last.HighPrice + last.LowPrice) / 2;
        
        // 상단/하단 밴드 계산
        decimal basicUpper = hl2 + (multiplier * atr);
        decimal basicLower = hl2 - (multiplier * atr);

        // (간략화된 표시 로직: 현재 종가 위치로 추세 판단)
        // 실제로는 이전 봉의 추세 값을 기억해야 정확하지만, 
        // 여기서는 현재 상태를 직관적으로 보여주는 데 집중합니다.
        
        bool isUptrend = last.ClosePrice > basicLower; 

        // ★ 상태 업데이트 (UI 표시용)
        if (isUptrend)
            _status = $"UP (${basicLower:F0})";
        else
            _status = $"DOWN (${basicUpper:F0})";

        // 매매 신호
        if (last.ClosePrice > basicUpper) return TradingSignal.Buy;
        if (last.ClosePrice < basicLower) return TradingSignal.Sell;

        return TradingSignal.Hold;
    }

    public string GetStatusValue() => _status;

    private decimal CalculateAtr(List<IBinanceKline> candles, int period)
    {
        if (candles.Count <= period) return 0;
        decimal sumTr = 0;
        for (int i = candles.Count - period; i < candles.Count; i++)
        {
            decimal tr = Math.Max(candles[i].HighPrice - candles[i].LowPrice,
                Math.Max(Math.Abs(candles[i].HighPrice - candles[i-1].ClosePrice),
                    Math.Abs(candles[i].LowPrice - candles[i-1].ClosePrice)));
            sumTr += tr;
        }
        return sumTr / period;
    }
}