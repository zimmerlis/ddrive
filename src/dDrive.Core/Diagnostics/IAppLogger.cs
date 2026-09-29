namespace dDrive.Core.Diagnostics;

public interface IAppLogger
{
    void LogInformation(string eventName, string message);
    void LogWarning(string eventName, string message);
    void LogError(string eventName, string message, Exception? exception = null);
}