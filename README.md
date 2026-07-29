# AWS-Uploader
Upload Unity Addressables to S3 compatible storage.

# Installation

## Install via Unity Package Manager (UPM)

1. Open your Unity project.
2. Go to **Window → Package Manager**.
3. Click the **+** button in the top-left corner.
4. Select **Add package from git URL...**
5. Enter the repository URL:

```text
https://github.com/salehb02/AWS-Uploader.git
```

Or install a specific version:

```text
https://github.com/salehb02/AWS-Uploader.git#1.0.0
```

6. Click **Add** and wait for Unity to import the package.

---

## Requirements

- Unity 2022.3 or newer
- .NET Standard 2.1 compatible scripting runtime
- AWS S3 compatible Object Storage
  - Amazon S3
  - ArvanCloud Object Storage
  - MinIO
  - Cloudflare R2
  - Backblaze B2 S3
  - Any S3-compatible provider

Store the S3 secret in the `S3_SECRET_KEY` operating-system environment variable. The legacy
`AWS_SECRET_KEY` name remains supported for backwards compatibility.

---

## Verify Installation

After the package is installed, the uploader window will be available from:

```
Tools → DevDude → Addressables Uploader
```

## Cache invalidation

Cache invalidation is provider-based and independent from the S3 implementation. The uploader sends
only object keys uploaded in the current run to the configured `ICacheInvalidationProvider`.

### ArvanCloud

1. Select `ArvanCloud` under `Cache Invalidation Provider` in your `AWSUploadSettings` asset.
2. Set `Cdn Domain` to the domain configured in ArvanCloud CDN (for example, `cdn.example.com`).
3. Create an ArvanCloud API key with CDN cache purge permission.
4. Store it in the `ARVAN_API_KEY` operating-system environment variable and restart Unity and Unity Hub.

The API key may be stored with or without the `Apikey ` prefix. Purging runs only after every file
and the upload manifest have been uploaded successfully. A purge failure is reported as an upload
error so a stale CDN deployment is not silently treated as successful.

### Supporting another CDN

Implement `ICacheInvalidationProvider`, then assign an instance to
`UploadConfig.CacheInvalidationProvider`. Editor integrations can add their provider to
`CacheInvalidationProviderType` and construct it in `AWSUploaderWindow.CreateCacheInvalidationProvider`.

```csharp
public sealed class MyCacheProvider : ICacheInvalidationProvider
{
    public string ProviderName => "My CDN";

    public Task InvalidateAsync(
        IReadOnlyCollection<string> objectKeys,
        CancellationToken cancellationToken = default)
    {
        // Convert object keys to the format expected by the provider and call its API.
        return Task.CompletedTask;
    }

    public void Dispose() { }
}
```
