using Api.Common;
using Application.DTOs.Library;
using Application.Interfaces;
using Domain.Utility.Admin;
using Domain.Utility.Library;
using Domain.Utility.Material;

namespace Api.Endpoints;

public static class LibraryEndpoints
{
    public static void MapLibraryEndpoints(this WebApplication app)
    {
        var library = app.MapGroup("api/library")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        library.MapGet("/", List);
        library.MapGet("/{itemId:guid}", Get);
        library.MapPost("/{itemId:guid}/import", Import);
        library.MapGet("/requests", MyRequests);
        library.MapPost("/requests", CreateRequest);
        library.MapDelete("/requests/{requestId:guid}", CancelRequest);

        // The owner's side, for the admin panel: nothing here is open to other users
        var admin = app.MapGroup("api/admin/library")
            .RequireAuthorization(AdminAccess.Policy)
            .RequireRateLimiting("api");

        admin.MapGet("/requests", AdminRequests);
        admin.MapGet("/requests/{requestId:guid}", AdminRequest);
        admin.MapPost("/requests/{requestId:guid}/approve", Approve);
        admin.MapPost("/requests/{requestId:guid}/reject", Reject);
        admin.MapPost("/items", Publish);
        admin.MapDelete("/items/{itemId:guid}", Remove);

        static async Task<IResult> List(
            ILibraryService service, string? search = null, MaterialType? type = null, int page = 1, int pageSize = 24) =>
            TypedResults.Ok(await service.ListAsync(search, type, page, pageSize));

        static async Task<IResult> Get(Guid itemId, ILibraryService service) =>
            await service.GetAsync(itemId) is { } item ? TypedResults.Ok(item) : TypedResults.NotFound();

        static async Task<IResult> Import(Guid itemId, ILibraryService service) =>
            await service.ImportAsync(itemId) is { } material ? TypedResults.Ok(material) : TypedResults.NotFound();

        static async Task<IResult> MyRequests(ILibraryService service) =>
            TypedResults.Ok(await service.GetMyRequestsAsync());

        static async Task<IResult> CreateRequest(LibraryRequestCreateDto dto, ILibraryService service) =>
            TypedResults.Ok(await service.CreateRequestAsync(dto));

        static async Task<IResult> CancelRequest(Guid requestId, ILibraryService service) =>
            await service.CancelRequestAsync(requestId) ? TypedResults.NoContent() : TypedResults.NotFound();

        static async Task<IResult> AdminRequests(ILibraryAdminService service, LibraryRequestStatus? status = LibraryRequestStatus.Pending) =>
            TypedResults.Ok(await service.GetRequestsAsync(status));

        static async Task<IResult> AdminRequest(Guid requestId, ILibraryAdminService service) =>
            await service.GetRequestAsync(requestId) is { } request ? TypedResults.Ok(request) : TypedResults.NotFound();

        // Every decision goes to the audit log
        static async Task<IResult> Approve(Guid requestId, ILibraryAdminService service, IAdminAudit audit)
        {
            if (await service.ApproveAsync(requestId) is not { } request) return TypedResults.NotFound();

            await audit.RecordAsync(AdminAction.ApproveRequest, request.Id, $"{request.Type}: {request.Title}");
            return TypedResults.Ok(request);
        }

        static async Task<IResult> Reject(Guid requestId, LibraryRejectDto dto, ILibraryAdminService service, IAdminAudit audit)
        {
            if (await service.RejectAsync(requestId, dto.Reason, dto.Comment) is not { } request) return TypedResults.NotFound();

            await audit.RecordAsync(AdminAction.RejectRequest, request.Id, $"{request.Type}: {request.Title} ({dto.Reason})");
            return TypedResults.Ok(request);
        }

        static async Task<IResult> Publish(LibraryAdminPublishDto dto, ILibraryAdminService service, IAdminAudit audit)
        {
            if (await service.PublishAsync(dto) is not { } item) return TypedResults.NotFound();

            await audit.RecordAsync(AdminAction.PublishItem, item.Id, item.Title);
            return TypedResults.Ok(item);
        }

        static async Task<IResult> Remove(Guid itemId, ILibraryAdminService service, IAdminAudit audit)
        {
            if (!await service.RemoveItemAsync(itemId)) return TypedResults.NotFound();

            await audit.RecordAsync(AdminAction.RemoveItem, itemId);
            return TypedResults.NoContent();
        }
    }
}
