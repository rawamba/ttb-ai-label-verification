using Microsoft.Extensions.Logging;

namespace LabelVerification.IntegrationTests.TestSupport;

/// <summary>
/// Minimal logger used to verify structured operational telemetry.
/// </summary>
internal sealed class CapturingLogger<T>
    : ILogger<T>
{
    private readonly List<string> _messages = [];

    internal IReadOnlyList<string> Messages =>
        _messages;

    public IDisposable? BeginScope<TState>(
        TState state)
        where TState : notnull =>
        NullScope.Instance;

    public bool IsEnabled(
        LogLevel logLevel) =>
        true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _messages.Add(
            formatter(
                state,
                exception));
    }

    private sealed class NullScope
        : IDisposable
    {
        internal static readonly NullScope Instance =
            new();

        public void Dispose()
        {
        }
    }
}