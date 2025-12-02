using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Binance.Net.Interfaces;
using QuantDashboard.Enums;
using QuantDashboard.Strategies;

namespace QuantDashboard.Managers;

public class LogManager
{
    private string _filePath;
    
    // 외부에서 "로그 파일 어디있어?"라고 물어볼 수 있게 공개
    public string CurrentLogPath => _filePath;

    public LogManager()
    {
        // [핵심 수정] 바탕화면(Desktop) 대신 -> "프로그램 실행 폴더/logs" 에 저장
        // 맥OS 권한 문제(TCC)를 우회하는 가장 확실한 방법입니다.
        string baseDir = AppDomain.CurrentDomain.BaseDirectory; 
        string logDir = Path.Combine(baseDir, "logs");

        // logs 폴더가 없으면 만듦
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        // 파일명: trade_log_2023-12-02.txt (날짜별로 분리 추천)
        string fileName = $"trade_log_{DateTime.Now:yyyy-MM-dd}.txt";
        _filePath = Path.Combine(logDir, fileName);
    }

    public void RecordEntry(TradingSignal positionType, int totalScore, decimal entryPrice, 
                            List<IBinanceKline> candles, 
                            List<(IStrategy strategy, TradingSignal signal)> strategyResults)
    {
        try
        {
            var sb = new StringBuilder();
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string action = positionType == TradingSignal.Buy ? "LONG" : "SHORT";

            sb.AppendLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
            sb.AppendLine($"┃ [ENTRY] {timestamp} | {action} | PRICE: ${entryPrice:F2} | SCORE: {totalScore,3} ┃");
            sb.AppendLine("┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┫");
            sb.AppendLine("┃ [STRATEGY SIGNALS]                                                       ┃");

            bool anySignal = false;
            foreach (var item in strategyResults)
            {
                if (item.signal != TradingSignal.Hold)
                {
                    string sigStr = item.signal == TradingSignal.Buy ? "BUY " : "SELL";
                    sb.AppendLine($"┃  - {item.strategy.Name,-35} : {sigStr} [{item.strategy.GetStatusValue()}]");
                    anySignal = true;
                }
            }
            
            if (!anySignal) sb.AppendLine("┃  (No specific signal, Forced Entry?)                                     ┃");

            sb.AppendLine("┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┫");
            sb.AppendLine($"┃ [CHART CONTEXT - LAST 50 CANDLES]                                        ┃");
            
            var recentCandles = candles.TakeLast(50).Reverse();
            foreach (var c in recentCandles)
            {
                sb.AppendLine($" {c.OpenTime:HH:mm},{c.OpenPrice},{c.HighPrice},{c.LowPrice},{c.ClosePrice},{c.Volume}");
            }
            sb.AppendLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
            sb.AppendLine(""); 

            File.AppendAllText(_filePath, sb.ToString());
        }
        catch (Exception ex)
        {
            // 파일 쓰기 실패 시 콘솔에라도 남김
            Console.WriteLine($"[Log Error] {ex.Message}");
        }
    }

    public void RecordExit(TradingSignal positionType, decimal entryPrice, decimal exitPrice, 
                           decimal pnl, decimal roe, string reason)
    {
        try
        {
            var sb = new StringBuilder();
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string type = positionType == TradingSignal.Buy ? "LONG" : "SHORT";
            string pnlStr = pnl >= 0 ? $"+${pnl:F2}" : $"-${Math.Abs(pnl):F2}";
            string result = pnl >= 0 ? "WIN " : "LOSS";

            sb.AppendLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
            sb.AppendLine($"┃ [EXIT]  {timestamp} | {type} | {result} ({reason})                         ");
            sb.AppendLine($"┃  • Flow: ${entryPrice:F2} -> ${exitPrice:F2}                             ");
            sb.AppendLine($"┃  • PnL : {pnlStr} (ROE {roe:F2}%)                                        ");
            sb.AppendLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
            sb.AppendLine(""); 

            File.AppendAllText(_filePath, sb.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Log Error] {ex.Message}");
        }
    }
}