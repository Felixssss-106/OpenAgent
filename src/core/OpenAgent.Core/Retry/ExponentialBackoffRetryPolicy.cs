using OpenAgent.Core.Error;

namespace OpenAgent.Core.Retry;

/// <summary>
/// Exponential backoff with jitter (spec section 68): 1s, 2s, 4s, 8s … capped.
/// Only transient network faults are retried — policy, permission and validation
/// failures must never be re-attempted, because repeating a side effect is worse
/// than failing (spec section 67).
/// </summary>
public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _jitterFraction;

    public ExponentialBackoffRetryPolicy(
        int maxAttempts = 5,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        double jitterFraction = 0.2)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "At least one attempt is required.");
        }

        MaxAttempts = maxAttempts;
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(1);
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(60);
        _jitterFraction = Math.Clamp(jitterFraction, 0.0, 1.0);
    }

    public int MaxAttempts { get; }

    public RetryAction Classify(Exception error, int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (attemptNumber >= MaxAttempts)
        {
            return RetryAction.DoNotRetry;
        }

        if (error is OpenAgentException oa)
        {
            return oa.Family == ErrorCodes.Network ? RetryAction.Retry : RetryAction.DoNotRetry;
        }

        return error is TimeoutException ? RetryAction.Retry : RetryAction.DoNotRetry;
    }

    public TimeSpan DelayFor(int attemptNumber)
    {
        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), attemptNumber, "Attempts are 1-based.");
        }

        var doubled = _baseDelay.TotalSeconds * Math.Pow(2, attemptNumber - 1);
        var capped = Math.Min(doubled, _maxDelay.TotalSeconds);
        var jitter = capped * _jitterFraction * (Random.Shared.NextDouble() * 2 - 1);
        return TimeSpan.FromSeconds(Math.Max(0, capped + jitter));
    }
}
