namespace Cyclo.Api.Contracts.Activities;

public record ActivityResponse(
    Guid Id,
    string Status,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ProcessingStartedAt,
    DateTimeOffset? ProcessingEndedAt,
    string FileName,
    string? FailureReason,
    ActivitySummaryResponse? Summary
    );