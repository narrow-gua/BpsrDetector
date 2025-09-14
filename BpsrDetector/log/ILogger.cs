namespace BprpDetector.log;

public interface ILogger
{
    void Info(string message);
    void Debug(string message);
    void Error(string message);
    void Warn(string message);
}