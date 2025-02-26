using System.Reactive.Linq;

namespace LSP2GXL.Utils;

public static class AsyncUtils
{
    public static Task<T?> WaitOrDefaultAsync<T>(this Task<T?> task, TimeSpan timeout, T? defaultValue = default, CancellationToken cancellationToken = default)
    {
        try
        {
            return task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return Task.FromResult(defaultValue);
        }
    }

    public static Task<T> WaitOrThrowAsync<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return task.WaitAsync(timeout, cancellationToken);
    }

    public static Task WaitOrThrowAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return task.WaitAsync(timeout, cancellationToken);
    }

    public static async Task WaitUntilAsync(Func<bool> condition, int sleepMs = 50, CancellationToken cancellationToken = default)
    {
        while (!condition())
        {
            await Task.Delay(sleepMs, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    public static async Task<bool> WaitUntilTimeoutAsync(Func<bool> condition, TimeSpan timeout, int sleepMs = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            await WaitUntilAsync(condition, sleepMs, cancellationToken).WaitAsync(timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public static IAsyncEnumerable<T> ObserveForAsync<T>(this IObservable<T> observable, TimeSpan timeout)
    {
        return observable.Timeout(timeout).Catch(Observable.Empty<T>()).ToAsyncEnumerable();
    }

    public static IAsyncEnumerable<T> ObserveEnumerableForAsync<T>(this IObservable<IEnumerable<T>> observable, TimeSpan timeout)
    {
        return observable.Timeout(timeout).SelectMany(x => x).Catch(Observable.Empty<T>()).ToAsyncEnumerable();
    }
}
