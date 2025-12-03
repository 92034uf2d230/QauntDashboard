using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace QuantDashboard.Engine.Data
{
    public static class CsvCandleLoader
    {
        /// <summary>
        /// download_futures_klines.py가 만든 CSV
        /// (timestamp,open,high,low,close,volume,quote_volume,trade_count)
        /// 를 읽어서 Candle 리스트로 변환
        /// </summary>
        public static List<Candle> LoadFromFile(string filePath)
        {
            var candles = new List<Candle>();

            using (var reader = new StreamReader(filePath))
            {
                // 헤더 한 줄 스킵
                string? headerLine = reader.ReadLine();
                if (headerLine == null)
                    throw new InvalidOperationException("Empty CSV file: " + filePath);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(',');
                    if (parts.Length < 8) continue;

                    var timestamp = DateTime.Parse(
                        parts[0],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal
                    );
                    var open        = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
                    var high        = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
                    var low         = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
                    var close       = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
                    var volume      = decimal.Parse(parts[5], CultureInfo.InvariantCulture);
                    var quoteVolume = decimal.Parse(parts[6], CultureInfo.InvariantCulture);
                    var tradeCount  = int.Parse(parts[7],    CultureInfo.InvariantCulture);

                    candles.Add(new Candle
                    {
                        Timestamp   = timestamp,
                        Open        = open,
                        High        = high,
                        Low         = low,
                        Close       = close,
                        Volume      = volume,
                        QuoteVolume = quoteVolume,
                        TradeCount  = tradeCount
                    });
                }
            }

            return candles;
        }
    }
}