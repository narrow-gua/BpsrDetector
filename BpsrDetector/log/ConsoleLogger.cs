namespace BprpDetector.log;

public class ConsoleLogger : ILogger
{
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");
    public void Debug(string message) => Console.WriteLine($"[DEBUG] {message}");
    public void Error(string message) => Console.WriteLine($"[ERROR] {message}");
    public void Warn(string message) => Console.WriteLine($"[WARN] {message}");
}