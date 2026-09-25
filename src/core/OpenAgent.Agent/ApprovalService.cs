using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAgent.Core;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Events;
using OpenAgent.Core.Time;
using OpenAgent.Storage.Repositories;

namespace OpenAgent.Agent;

/// <summary>
/// Owns the lifecycle of an approval: create it, let the UI react to
/// <see cref="ApprovalRequested"/>, resolve it, and expire it if nobody answers
/// (spec sections 98, 197).
/// </summary>
public sealed class ApprovalService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);

    private readonly ApprovalRepository _repository;
    private readonly IEventBus _events;
    private readonly IUtcClock _clock;
    private readonly TimeSpan _lifetime;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        ApprovalRepository repository,
        IEventBus events,
        IUtcClock clock,
        ILogger<ApprovalService>? logger = null,
        TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);

        _repository = repository;
        _events = events;
        _clock = clock;
        _lifetime = lifetime ?? DefaultLifetime;
        _logger = logger ?? NullLogger<ApprovalService>.Instance;
    }

    public async Task<ApprovalRecord> RequestAsync(
        string taskId,
        string toolId,
        RiskLevel risk,
        bool reversible,
        string message,
        string argumentsSummary,
        string? affectedPaths = null,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var approval = new ApprovalRecord
        {
            TaskId = taskId,
            ToolId = toolId,
            Risk = risk,
            Reversible = reversible,
            Message = message,
            ArgumentsSummary = argumentsSummary,
            AffectedPaths = affectedPaths,
            Status = ApprovalStatus.Pending,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(_lifetime),
        };

        _repository.Insert(approval);

        await _events.PublishAsync(
            new ApprovalRequested(
                approval.Id,
                approval.TaskId,
                approval.ToolId,
                approval.Risk,
                approval.Reversible,
                approval.Message,
                approval.ExpiresAtUtc),
            cancellationToken).ConfigureAwait(false);

        return approval;
    }

    /// <summary>
    /// Resolving an already-expired or already-resolved approval is refused; the
    /// caller must re-request (spec section 109).
    /// </summary>
    public async Task<bool> ResolveAsync(
        string approvalId,
        bool approved,
        string resolvedBy,
        CancellationToken cancellationToken = default)
    {
        var approval = _repository.Get(approvalId);
        if (approval is null)
        {
            _logger.LogWarning("Approval {ApprovalId} not found.", approvalId);
            return false;
        }

        var now = _clock.UtcNow;
        if (!approval.IsPending(now))
        {
            _logger.LogWarning(
                "Approval {ApprovalId} is no longer pending (status {Status}, expires {Expires}).",
                approvalId,
                approval.Status,
                approval.ExpiresAtUtc);
            return false;
        }

        approval.Status = approved ? ApprovalStatus.Approved : ApprovalStatus.Denied;
        approval.ResolvedAtUtc = now;
        approval.ResolvedBy = resolvedBy;
        _repository.Update(approval);

        await _events.PublishAsync(
            new ApprovalResolved(approval.Id, approval.TaskId, approved, resolvedBy),
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    public IReadOnlyList<ApprovalRecord> Pending() => _repository.Pending(_clock.UtcNow);

    public int ExpireStale() => _repository.Expire(_clock.UtcNow);
}
