namespace TaskbarLyriz.Core.Abstractions;

public interface IAppLogger : IDisposable
{
    void Write(
        AppLogLevel level,
        string eventName,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null);
}

public enum AppLogLevel
{
    Debug,
    Information,
    Warning,
    Error,
    Critical,
}

public static class AppLoggerExtensions
{
    public static void Information(this IAppLogger logger, string eventName, string message) =>
        logger.Write(AppLogLevel.Information, eventName, message);

    public static void Warning(
        this IAppLogger logger,
        string eventName,
        string message,
        Exception? exception = null) =>
        logger.Write(AppLogLevel.Warning, eventName, message, exception);

    public static void Error(
        this IAppLogger logger,
        string eventName,
        string message,
        Exception exception) =>
        logger.Write(AppLogLevel.Error, eventName, message, exception);
}
