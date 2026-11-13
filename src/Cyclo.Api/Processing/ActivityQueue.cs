using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Cyclo.Api.Processing;

public class ActivityQueue(IOptions<ActivityQueueOptions> options)
{
    private readonly Channel<Guid> _queue = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity: options.Value.Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });
    
    public int RetryAfterSeconds => options.Value.RetryAfterSeconds;
    
    public bool TryEnqueue(Guid activityId) =>
        _queue.Writer.TryWrite(activityId);
    
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => 
        _queue.Reader.ReadAllAsync(ct);
    
    public bool IsFull => _queue.Reader.Count >= options.Value.Capacity;
}