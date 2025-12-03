using System;

namespace QuantDashboard.Engine.Data
{
    public class Candle
    {
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public decimal QuoteVolume { get; set; }
        public int TradeCount { get; set; }

        public bool IsBullish => Close > Open;
        public bool IsBearish => Close < Open;
    }
}