using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class WhaleAggressionStrategy : IStrategy
{
    public string Name => "고래 공격성 (Taker Flow)";
    private string _status = "Vol: Low";

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 5) return TradingSignal.Hold;

        var last = candles.Last();
        if (last.Volume < 100) 
        {
            _status = "Vol: Low";
            return TradingSignal.Hold;
        }

        decimal buyVol = last.TakerBuyBaseVolume; 
        decimal sellVol = last.Volume - last.TakerBuyBaseVolume;

        // ★ 상태 업데이트: 매수/매도 비율 표시
        // 1.0 이상이면 매수 우세, 이하면 매도 우세
        if (sellVol == 0) sellVol = 1; 
        decimal ratio = buyVol / sellVol;
        
        _status = $"BuyRatio: {ratio:F2}x";

        bool isAggressiveBuy = buyVol > sellVol * 1.5m; 
        bool isPanicSell = sellVol > buyVol * 1.5m;

        if (isAggressiveBuy)
        {
            if (last.ClosePrice > last.OpenPrice) return TradingSignal.Buy;
        }
        else if (isPanicSell)
        {
            if (last.ClosePrice < last.OpenPrice) return TradingSignal.Sell;
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;
}