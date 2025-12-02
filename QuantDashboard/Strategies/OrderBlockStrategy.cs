using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class OrderBlockStrategy : IStrategy
{
    public string Name => "오더 블록 (Order Block)";
    private string _status = "Scanning..."; // 상태 표시용

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        if (candles.Count < 50) return TradingSignal.Hold;

        var currentPrice = candles.Last().ClosePrice;
        bool obFound = false; // 오더블록 발견 여부

        // 최근 50개 캔들 중에서 '유효한 오더블록' 찾기
        for (int i = candles.Count - 5; i >= candles.Count - 30; i--)
        {
            var candle = candles[i];     // 기준 캔들
            var nextCandle = candles[i+1]; // 반응 캔들

            // 1. 상승 오더블록 (Bullish OB)
            bool isBullishOB = candle.ClosePrice < candle.OpenPrice && // 음봉
                               nextCandle.ClosePrice > candle.OpenPrice && // 장악형 양봉
                               nextCandle.Volume > candle.Volume * 1.5m;   // 거래량 폭발

            if (isBullishOB)
            {
                decimal obUpper = candle.OpenPrice;
                decimal obLower = candle.ClosePrice;

                // ★ 상태 업데이트: 발견된 블록 위치 표시
                _status = $"Bull OB ${obLower:F0}";
                obFound = true;

                // Retest 진입 로직
                if (currentPrice <= obUpper && currentPrice >= obLower)
                {
                    return TradingSignal.Buy;
                }
            }

            // 2. 하락 오더블록 (Bearish OB)
            bool isBearishOB = candle.ClosePrice > candle.OpenPrice && // 양봉
                               nextCandle.ClosePrice < candle.OpenPrice && // 장악형 음봉
                               nextCandle.Volume > candle.Volume * 1.5m;

            if (isBearishOB)
            {
                decimal obLower = candle.OpenPrice;
                decimal obUpper = candle.ClosePrice;

                // ★ 상태 업데이트
                _status = $"Bear OB ${obUpper:F0}";
                obFound = true;

                // Retest 진입 로직
                if (currentPrice >= obLower && currentPrice <= obUpper)
                {
                    return TradingSignal.Sell;
                }
            }
        }

        if (!obFound) _status = "No Active OB"; // 발견된 게 없을 때

        return TradingSignal.Hold;
    }

    public string GetStatusValue() => _status;
}