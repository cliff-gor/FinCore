using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.Modules.Identity.Application.Approvals;
using Quartz;

namespace FinCore.Modules.Identity.Infrastructure.Jobs;

/// <summary>Daily sweep that expires pending approval requests older than 7 days (spec 002).</summary>
[DisallowConcurrentExecution]
internal sealed class ExpireApprovalRequestsJob(IDispatcher dispatcher) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) =>
        await dispatcher.Send(new ExpireApprovalRequestsCommand(), cancellationToken).ConfigureAwait(false);
}
