using IPA.Logging;

namespace BetterBSAvatar
{
    internal static class Log
    {
        private static Logger _logger;

        internal static void Init(Logger logger)
        {
            _logger = logger;
        }

        internal static void Info(string message)
        {
            _logger?.Info(message);
        }

        internal static void Debug(string message)
        {
            _logger?.Debug(message);
        }

        internal static void Warn(string message)
        {
            _logger?.Warn(message);
        }

        internal static void Error(string message)
        {
            _logger?.Error(message);
        }

        internal static void Error(System.Exception exception)
        {
            _logger?.Error(exception);
        }
    }
}
