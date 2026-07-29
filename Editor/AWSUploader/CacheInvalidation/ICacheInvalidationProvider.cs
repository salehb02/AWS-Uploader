using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevDude.AWSUploader.CacheInvalidation
{
    /// <summary>
    /// Invalidates uploaded object keys in a CDN or any cache layer in front of S3.
    /// Implement this interface to add support for another cache provider.
    /// </summary>
    public interface ICacheInvalidationProvider : IDisposable
    {
        string ProviderName { get; }

        Task InvalidateAsync(
            IReadOnlyCollection<string> objectKeys,
            CancellationToken cancellationToken = default);
    }
}
