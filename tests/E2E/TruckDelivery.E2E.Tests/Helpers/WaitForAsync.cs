namespace TruckDelivery.E2E.Tests.Helpers;

public static class WaitForAsync
{
    /// <summary>
    /// Polls <paramref name="fetchAsync"/> until <paramref name="condition"/> is met or
    /// <paramref name="timeout"/> elapses. Returns the last fetched value that satisfied the condition.
    /// Throws <see cref="TimeoutException"/> if condition is never met.
    /// </summary>
    public static async Task<T> UntilAsync<T>(
        Func<Task<T?>> fetchAsync,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        string? description = null)
    {
        timeout ??= TimeSpan.FromSeconds(45);
        pollInterval ??= TimeSpan.FromMilliseconds(500);

        var deadline = DateTime.UtcNow + timeout;
        T? last = default;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                last = await fetchAsync();
                if (last is not null && condition(last))
                    return last;
            }
            catch
            {
                // Swallow transient errors — service may not be ready yet
            }

            await Task.Delay(pollInterval.Value);
        }

        var desc = description ?? typeof(T).Name;
        throw new TimeoutException($"Condition not met within {timeout} for {desc}. Last value: {last}");
    }
}
