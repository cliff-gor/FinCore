namespace FinCore.IntegrationTests.Shared;

public static class Eventually
{
    /// <summary>Polls until <paramref name="condition"/> holds or the timeout elapses (async event flows).</summary>
    public static async Task<bool> HoldsAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition().ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        return await condition().ConfigureAwait(false);
    }
}
