using OpenAgent.Core.Error;
using OpenAgent.Core.Retry;

namespace OpenAgent.Core.Tests;

public sealed class RetryPolicyTests
{
    [Fact]
    public void Network_failures_are_retried()
    {
        var policy = new ExponentialBackoffRetryPolicy();

        Assert.Equal(
            RetryAction.Retry,
            policy.Classify(OpenAgentException.Network("relay unreachable"), 1));
    }

    [Fact]
    public void Permission_failures_are_never_retried()
    {
        var policy = new ExponentialBackoffRetryPolicy();

        Assert.Equal(
            RetryAction.DoNotRetry,
            policy.Classify(OpenAgentException.PermissionDenied("denied"), 1));
    }

    [Fact]
    public void Invalid_arguments_are_never_retried()
    {
        var policy = new ExponentialBackoffRetryPolicy();

        Assert.Equal(
            RetryAction.DoNotRetry,
            policy.Classify(OpenAgentException.InvalidArguments("bad schema"), 1));
    }

    [Fact]
    public void Timeouts_are_retried_but_unknown_errors_are_not()
    {
        var policy = new ExponentialBackoffRetryPolicy();

        Assert.Equal(RetryAction.Retry, policy.Classify(new TimeoutException(), 1));
        Assert.Equal(RetryAction.DoNotRetry, policy.Classify(new InvalidOperationException(), 1));
    }

    [Fact]
    public void The_attempt_budget_is_honoured()
    {
        var policy = new ExponentialBackoffRetryPolicy(maxAttempts: 3);

        Assert.Equal(RetryAction.Retry, policy.Classify(OpenAgentException.Network("x"), 2));
        Assert.Equal(RetryAction.DoNotRetry, policy.Classify(OpenAgentException.Network("x"), 3));
    }

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 2.0)]
    [InlineData(3, 4.0)]
    public void Delay_doubles_without_jitter(int attempt, double expectedSeconds)
    {
        var policy = new ExponentialBackoffRetryPolicy(jitterFraction: 0);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), policy.DelayFor(attempt));
    }

    [Fact]
    public void Delay_is_capped()
    {
        var policy = new ExponentialBackoffRetryPolicy(maxAttempts: 20, jitterFraction: 0);

        Assert.Equal(TimeSpan.FromSeconds(60), policy.DelayFor(15));
    }

    [Fact]
    public void Jitter_stays_inside_the_band()
    {
        var policy = new ExponentialBackoffRetryPolicy(jitterFraction: 0.2);

        for (var i = 0; i < 200; i++)
        {
            var delay = policy.DelayFor(1);
            Assert.InRange(delay.TotalSeconds, 0.8, 1.2);
        }
    }

    [Fact]
    public void Attempts_are_one_based() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialBackoffRetryPolicy().DelayFor(0));
}
