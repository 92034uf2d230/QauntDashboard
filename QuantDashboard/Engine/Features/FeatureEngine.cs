using System;
using System.Collections.Generic;
using System.Linq;
using QuantDashboard.Engine.Data;
using QuantDashboard.Engine.Models;

namespace QuantDashboard.Engine.Features
{
    public static class FeatureEngine
    {
        // 로그 수익률
        private static double[] ComputeLogReturns(IReadOnlyList<Candle> candles)
        {
            var n = candles.Count;
            var returns = new double[n];
            returns[0] = 0.0;

            for (int i = 1; i < n; i++)
            {
                double prevClose = (double)candles[i - 1].Close;
                double close     = (double)candles[i].Close;
                returns[i]       = Math.Log(close / prevClose);
            }

            return returns;
        }

        // 단순 이동평균
        private static double[] ComputeSma(double[] values, int period)
        {
            var n = values.Length;
            var sma = new double[n];
            double sum = 0;

            for (int i = 0; i < n; i++)
            {
                sum += values[i];
                if (i >= period)
                    sum -= values[i - period];

                sma[i] = i >= period - 1 ? sum / period : double.NaN;
            }

            return sma;
        }

        // 롤링 표준편차
        private static double[] ComputeRollingStd(double[] values, int period)
        {
            var n = values.Length;
            var std = new double[n];

            for (int i = 0; i < n; i++)
            {
                if (i < period - 1)
                {
                    std[i] = double.NaN;
                    continue;
                }

                double mean = 0;
                for (int j = i - period + 1; j <= i; j++)
                    mean += values[j];
                mean /= period;

                double sumSq = 0;
                for (int j = i - period + 1; j <= i; j++)
                {
                    double diff = values[j] - mean;
                    sumSq += diff * diff;
                }

                std[i] = Math.Sqrt(sumSq / period);
            }

            return std;
        }

        // RSI (Wilder)
        private static double[] ComputeRsi(double[] closes, int period = 14)
        {
            var n = closes.Length;
            var rsi = new double[n];
            if (n == 0) return rsi;

            double gain = 0;
            double loss = 0;

            // 초기 구간
            for (int i = 1; i <= period && i < n; i++)
            {
                double change = closes[i] - closes[i - 1];
                if (change > 0) gain += change;
                else loss -= change;
            }

            gain /= period;
            loss /= period;

            if (period < n)
            {
                rsi[period] = loss == 0
                    ? 100
                    : 100 - (100 / (1 + (gain / loss)));
            }

            // 이후
            for (int i = period + 1; i < n; i++)
            {
                double change = closes[i] - closes[i - 1];
                double up = Math.Max(change, 0);
                double down = Math.Max(-change, 0);

                gain = (gain * (period - 1) + up) / period;
                loss = (loss * (period - 1) + down) / period;

                if (loss == 0)
                    rsi[i] = 100;
                else
                {
                    double rs = gain / loss;
                    rsi[i] = 100 - (100 / (1 + rs));
                }
            }

            // 초기 값 NaN
            for (int i = 0; i < period && i < n; i++)
                rsi[i] = double.NaN;

            return rsi;
        }

        public static List<FeaturePoint> BuildFeatures(
            List<Candle> candles,
            int windowForReturn = 20,
            int windowForVol = 20,
            int windowForZ = 20,
            int rsiPeriod = 14,
            int futureHorizon = 4   // 이후 몇 개 캔들의 수익률을 볼지
        )
        {
            int n = candles.Count;
            if (n == 0) return new List<FeaturePoint>();

            var closes  = candles.Select(c => (double)c.Close).ToArray();
            var volumes = candles.Select(c => (double)c.Volume).ToArray();
            var logRets = ComputeLogReturns(candles);

            var volStd   = ComputeRollingStd(logRets, windowForVol);
            var priceStd = ComputeRollingStd(closes,   windowForZ);
            var priceSma = ComputeSma(closes,          windowForZ);
            var volSma   = ComputeSma(volumes,         windowForZ);
            var rsi      = ComputeRsi(closes,          rsiPeriod);

            var featurePoints = new List<FeaturePoint>(n);

            int minIdx = Math.Max(Math.Max(windowForReturn, windowForVol), windowForZ);
            int lastIdxForFeature = n - futureHorizon - 1;

            for (int i = minIdx; i <= lastIdxForFeature; i++)
            {
                // 최근 windowForReturn 기간 누적 로그 수익률
                double sumRet = 0;
                for (int j = i - windowForReturn + 1; j <= i; j++)
                    sumRet += logRets[j];

                // 거래량 비정상성 (평균 대비 비율)
                double volZ;
                if (double.IsNaN(volSma[i]))
                    volZ = 0;
                else
                    volZ = (volumes[i] - volSma[i]) / (volSma[i] + 1e-9);

                // 가격 ZScore
                double priceZ;
                if (double.IsNaN(priceSma[i]) || double.IsNaN(priceStd[i]) || priceStd[i] == 0)
                    priceZ = 0;
                else
                    priceZ = (closes[i] - priceSma[i]) / priceStd[i];

                // 변동성
                double volatility = double.IsNaN(volStd[i]) ? 0 : volStd[i];

                // MA 괴리
                double maGap;
                if (double.IsNaN(priceSma[i]) || priceSma[i] == 0)
                    maGap = 0;
                else
                    maGap = closes[i] / priceSma[i] - 1.0;

                double rsiVal = double.IsNaN(rsi[i]) ? 50.0 : rsi[i];

                var vector = new[]
                {
                    sumRet,        // 최근 N캔들 누적 수익률
                    volZ,          // 거래량 비정상성
                    priceZ,        // 가격 ZScore
                    volatility,    // 변동성
                    maGap,         // MA 괴리
                    rsiVal         // RSI
                };

                // 미래 수익률 (예: 다음 futureHorizon 캔들의 로그 수익률 합)
                double futureRet = 0;
                for (int j = i + 1; j <= i + futureHorizon; j++)
                    futureRet += logRets[j];

                featurePoints.Add(new FeaturePoint
                {
                    Timestamp    = candles[i].Timestamp,
                    Vector       = vector,
                    FutureReturn = futureRet
                });
            }

            return featurePoints;
        }
    }
}