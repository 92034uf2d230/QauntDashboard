using System;
using QuantDashboard.Enums;

namespace QuantDashboard.Models
{
    public class TradeRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public TradingSignal Type { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Amount { get; set; }
        public decimal PnL { get; set; }
        public decimal Roe { get; set; }
        public decimal Leverage { get; set; }
        public string ExitReason { get; set; } = string.Empty;
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public bool IsLive { get; set; } = false;
    }
}