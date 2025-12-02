using System;
using System.Collections.Generic;
using System.Linq;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;

namespace QuantDashboard.Strategies;

public class VectorPatternStrategy : IStrategy
{
    public string Name => "벡터 패턴 매칭 (AI Pattern)";
    private string _status = "Sim: 0%";
    
    // 타겟 패턴: 급락 후 반등 (V자)
    private readonly double[] _targetPattern = { -0.5, -0.8, -1.0, -0.2, 0.1, 0.5, 0.8, 1.0, 1.2, 0.5 };

    public TradingSignal Analyze(List<IBinanceKline> candles)
    {
        int patternSize = _targetPattern.Length;
        if (candles.Count < patternSize + 1) return TradingSignal.Hold;

        var currentVector = new double[patternSize];
        var recentCandles = candles.Skip(candles.Count - patternSize - 1).ToList();

        for (int i = 0; i < patternSize; i++)
        {
            double change = (double)((recentCandles[i+1].ClosePrice - recentCandles[i].ClosePrice) 
                                     / recentCandles[i].ClosePrice) * 100;
            currentVector[i] = change;
        }

        // 코사인 유사도 계산
        double similarity = CalculateCosineSimilarity(currentVector, _targetPattern);

        // ★ 상태 업데이트: 유사도 % 표시
        _status = $"Match: {similarity*100:F0}%";

        // 85% 이상 일치하면 매수
        if (similarity > 0.85)
        {
            return TradingSignal.Buy;
        }

        return TradingSignal.Hold;
    }
    
    public string GetStatusValue() => _status;

    private double CalculateCosineSimilarity(double[] vecA, double[] vecB)
    {
        double dotProduct = 0.0;
        double magnitudeA = 0.0;
        double magnitudeB = 0.0;

        for (int i = 0; i < vecA.Length; i++)
        {
            dotProduct += vecA[i] * vecB[i];
            magnitudeA += Math.Pow(vecA[i], 2);
            magnitudeB += Math.Pow(vecB[i], 2);
        }

        if (magnitudeA == 0 || magnitudeB == 0) return 0;
        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}