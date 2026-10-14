using Microsoft.Extensions.Options;

namespace FinCore.Bff.Sessions;

/// <summary>
/// Deletes this mode's expired and revoked sessions every 15 minutes. <see cref="BffTicketStore"/> already rejects them
/// on use; this only removes sessions whose browser never came back, so encrypted tokens do not linger in the database.
/// </summary>
/// <remarks>Several gateway instances may purge at once; the delete is idempotent, so that is harmless.</remarks>
internal sealed partial class ExpiredSessionPurger(
    IBffSessionRepository sessions,
    IOptions<BffSessionOptions> options,
    TimeProvider timeProvider,
    ILogger<ExpiredSessionPurger> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            try
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                var deleted = await sessions
                    .DeleteExpiredAsync(options.Value.Mode, now - options.Value.IdleTimeout, now, stoppingToken)
                    .ConfigureAwait(false);
                if (deleted > 0)
                {
                    LogPurged(logger, deleted);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A database outage must not stop the gateway; the next tick tries again.
                LogPurgeFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} expired BFF sessions.")]
    private static partial void LogPurged(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Purging expired BFF sessions failed; retrying on the next run.")]
    private static partial void LogPurgeFailed(ILogger logger, Exception exception);
}
