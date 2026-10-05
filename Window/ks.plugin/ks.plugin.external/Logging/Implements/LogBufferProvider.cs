using ks.plugin.external.Logging.Interfaces;
using Microsoft.Extensions.Logging;

namespace ks.plugin.external.Logging.Implements
{
    public sealed class LogBufferProvider : ILoggerProvider
    {
        private readonly ILogBuffer _buffer;

        public LogBufferProvider(ILogBuffer buffer)
        {
            _buffer = buffer;
        }

        public ILogger CreateLogger(string categoryName) => new BufferLogger(categoryName, _buffer);

        public void Dispose()
        {
        }

        private sealed class BufferLogger : ILogger
        {
            private readonly string _category;
            private readonly ILogBuffer _buffer;

            public BufferLogger(string category, ILogBuffer buffer)
            {
                _category = category[(category.LastIndexOf('.') + 1)..];
                _buffer = buffer;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                var line = $"{DateTime.Now:HH:mm:ss} {GetLevelName(logLevel)} {_category}: {formatter(state, exception)}";
                if (exception != null && !line.Contains(exception.Message, StringComparison.Ordinal))
                {
                    line += $" ({exception.GetType().Name}: {exception.Message})";
                }

                _buffer.Add(line);
            }

            private static string GetLevelName(LogLevel logLevel) => logLevel switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                LogLevel.Critical => "crit",
                _ => "    ",
            };
        }
    }
}
