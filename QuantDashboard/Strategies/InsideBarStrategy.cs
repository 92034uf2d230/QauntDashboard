using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class InsideBarStrategy : IStrategy
{
    public string Name => "인사이드 바 (Volatility Breakout)";
    private string _status = "No Pattern";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 3) return TradingSignal.Hold;

        var motherBar = candles[candles.Count - 3]; 
        var insideBar = candles[candles.Count - 2]; 
        var breakCandle = candles.Last();           

        // 1. 인사이드 바 조건 확인
        bool isInside = insideBar.HighPrice < motherBar.HighPrice && 
                        insideBar.LowPrice > motherBar.LowPrice;

        // ★ 상태 업데이트
        if (isInside)
            _status = "Inside Formed"; // 패턴 형성됨 (대기 중)
        else
            _status = "No Pattern";

        if (!isInside) return TradingSignal.Hold;

        // 2. 진입 로직 (돌파)
        if (breakCandle.ClosePrice > motherBar.HighPrice)
        {
            _status = "Breakout UP"; // 상방 돌파
            return TradingSignal.Buy;
        }

        if (breakCandle.ClosePrice < motherBar.LowPrice)
        {
            _status = "Breakout DOWN"; // 하방 돌파
            return TradingSignal.Sell;
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}