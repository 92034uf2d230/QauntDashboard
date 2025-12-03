using System;

namespace QuantDashboard.Engine.Models
{
    public class FeaturePoint
    {
        public DateTime Timestamp { get; set; }
        public double[] Vector { get; set; } = Array.Empty<double>();

        // 이 시점 이후의 수익률 레이블 (로그 수익률 합)
        public double? FutureReturn { get; set; }
    }
}