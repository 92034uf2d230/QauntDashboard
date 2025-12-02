using System.Collections.Generic;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public interface IStrategy
{
    string Name { get; }
    
    TradingSignal Analyze(List<IBinanceKline> candles);

    // [신규] 현재 지표의 상태값 반환 (예: "RSI: 55.4" or "Gap: 0.25%")
    // 기본값은 "-"로 설정하여 기존 파일들이 에러나지 않게 함
    string GetStatusValue() => "-"; 
}