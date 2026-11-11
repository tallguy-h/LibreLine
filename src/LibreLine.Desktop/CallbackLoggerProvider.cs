// SPDX-License-Identifier: GPL-2.0-only
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace LibreLine.Desktop;

/// <summary>Formats the server's log as short lines for the window.</summary>
internal sealed class CallbackLoggerProvider(Action<string> write) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Logger(write);

    public void Dispose()
    {
    }

    private sealed class Logger(Action<string> write) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var level = logLevel switch
            {
                LogLevel.Warning => "warning: ",
                LogLevel.Error or LogLevel.Critical => "error: ",
                _ => "",
            };
            var line = string.Create(CultureInfo.InvariantCulture,
                $"{DateTime.Now:HH:mm:ss} {level}{formatter(state, exception)}");
            if (exception is not null)
            {
                line += $" ({exception.GetType().Name}: {exception.Message})";
            }

            write(line);
        }
    }
}
