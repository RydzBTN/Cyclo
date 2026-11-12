namespace Cyclo.Api.Data.Entities.Activity;

public class ActivityFileEntity
{
    public Guid ActivityId { get; set; }
    public required byte[] Data { get; set; }
}