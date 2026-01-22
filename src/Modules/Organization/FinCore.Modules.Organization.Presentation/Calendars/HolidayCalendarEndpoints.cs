using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Web;
using FinCore.BuildingBlocks.Web.Authorization;
using FinCore.Modules.Organization.Application.Calendars;
using FinCore.Modules.Organization.Presentation.Institutions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinCore.Modules.Organization.Presentation.Calendars;

/// <summary>Request body: the full new set of weekend days and future holidays (spec 002 replace semantics).</summary>
public sealed record ReplaceHolidayCalendarRequest(IReadOnlyList<DayOfWeek> WeekendDays, IReadOnlyList<HolidayDto> Holidays);

/// <summary>Holiday calendar endpoints (spec §12, spec 002).</summary>
public static class HolidayCalendarEndpoints
{
    public static IEndpointRouteBuilder MapHolidayCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/institutions/{institutionId:guid}/holidays")
            .WithTags("Holidays")
            .WithGroupName(InstitutionEndpoints.OpenApiGroup);

        group.MapGet("/", async (Guid institutionId, int? year, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new GetHolidayCalendarQuery(institutionId, year), ct).ConfigureAwait(false);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
            })
            .WithName("GetHolidayCalendar")
            .WithSummary("Weekend days and holidays (optionally for one year)")
            .RequirePermission(Permissions.Organization.Read)
            .Produces<HolidayCalendarResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/", async (Guid institutionId, ReplaceHolidayCalendarRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(
                    new ReplaceHolidayCalendarCommand(institutionId, request.WeekendDays, request.Holidays), ct).ConfigureAwait(false);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
            })
            .WithName("ReplaceHolidayCalendar")
            .WithSummary("Replace weekend days and future holidays; past holidays are immutable")
            .RequirePermission(Permissions.Organization.Manage)
            .Produces<HolidayCalendarResponse>()
            .ProducesValidationProblem();

        return app;
    }
}
