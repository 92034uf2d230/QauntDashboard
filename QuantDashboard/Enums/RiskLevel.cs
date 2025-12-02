namespace QuantDashboard.Enums;

public enum RiskLevel
{
    Low,    // 안전 (최대 5배)
    Medium, // 보통 (최대 20배)
    High,   // 위험 (최대 50배)
    Degen   // 야수 (최대 100배 - 청산 위험 큼)
}