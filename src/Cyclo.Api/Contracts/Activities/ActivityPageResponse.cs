namespace Cyclo.Api.Contracts.Activities;

public record ActivityPageResponse(
    IReadOnlyList<ActivityListItemResponse> Items,
    string? NextCursor
    );