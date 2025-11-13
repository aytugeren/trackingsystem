using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KuyumculukTakipProgrami.Api.Services;

public interface IPrintQueueService
{
    Task EnqueueAsync(IEnumerable<string> zpls, CancellationToken cancellationToken = default);
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
}
