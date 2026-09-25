namespace OpenAgent.Core.Retry;

public enum RetryAction
{
    Retry = 0,
    DoNotRetry = 1,
}

/// <summary>
/// Retry decisions live in exactly one place (spec section 200): network faults
/// back off and retry, policy and validation failures must never be retried.
/// </summary>
public interface IRetryPolicy
{
    int MaxAttempts { get; }

    RetryAction Classify(Exception error, int attemptNumber);

    TimeSpan DelayFor(int attemptNumber);
}
