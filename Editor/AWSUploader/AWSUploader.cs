using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DevDude.AWSUploader
{
    public class AWSUploader : IDisposable
    {
        public static readonly Version VERSION = new Version(1, 1, 0);

        private readonly UploadConfig _config;

        private readonly AmazonS3Client _client;
        private ManifestData _cachedRemoteManifest;

        private bool _disposed;

        public event Action<int, int, float> OnUploadProgress; /// completedFiles, totalFiles, progress(0-1)

        public AWSUploader(UploadConfig config)
        {
            _config = config;

            var secretKey =
                Environment.GetEnvironmentVariable("S3_SECRET_KEY") ??
                Environment.GetEnvironmentVariable("AWS_SECRET_KEY");

            if (string.IsNullOrEmpty(secretKey))
            {
                _config.CacheInvalidationProvider?.Dispose();
                throw new Exception(
                    "S3_SECRET_KEY environment variable is missing. " +
                    "AWS_SECRET_KEY is also supported for backwards compatibility. " +
                    "Add one of them to your system environment variables then restart Unity and Unity Hub.");
            }

            var credentials = new BasicAWSCredentials(config.AccessKey, secretKey);

            var s3Config = new AmazonS3Config
            {
                ServiceURL = config.ServiceUrl,
                ForcePathStyle = config.ForcePathStyle,
            };

            if (!string.IsNullOrWhiteSpace(config.AuthenticationRegion))
                s3Config.AuthenticationRegion = config.AuthenticationRegion;

            _client = new AmazonS3Client(credentials, s3Config);
        }

        ~AWSUploader()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _client?.Dispose();
                _config.CacheInvalidationProvider?.Dispose();
            }

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AWSUploader));
        }

        public async Task UploadFolderAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            await ValidateAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var uploadErrors = new ConcurrentBag<Exception>();

            var localManifest = new ManifestData
            {
                Files = await HashUtility.BuildManifestAsync(_config, cancellationToken)
            };
            var remoteManifest = await DownloadManifestAsync(cancellationToken);

            var plan = BuildUploadPlan(localManifest, remoteManifest);

            Debug.Log(plan.GetSummary());

            Debug.Log(
                $"Upload: {plan.Upload.Count} | " +
                $"Skip: {plan.Skip.Count} | " +
                $"Delete: {plan.Delete.Count}");

            using var semaphore = new SemaphoreSlim(_config.ParallelUploads);

            int totalFiles = plan.Upload.Count;
            int completedFiles = 0;

            var uploadTasks = new List<Task>();

            foreach (var relativePath in plan.Upload)
            {
                uploadTasks.Add(UploadSingleFileAsync(relativePath, semaphore, cancellationToken, totalFiles, () => Interlocked.Increment(ref completedFiles), uploadErrors));
            }

            if (uploadTasks.Count > 0)
                await Task.WhenAll(uploadTasks);

            if (uploadErrors.Count > 0)
            {
                var message = new StringBuilder();

                message.AppendLine($"Upload failed for {uploadErrors.Count} files:");

                foreach (var error in uploadErrors)
                {
                    message.AppendLine(error.Message);
                }

                throw new Exception(message.ToString());
            }

            if (_config.DeleteRemovedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DeleteRemoteFilesAsync(plan.Delete, cancellationToken);
            }
            else
            {
                Debug.Log("Remote file deletion disabled.");
            }

            await UploadManifestAsync(localManifest, cancellationToken);

            if (_config.CacheInvalidationProvider != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var invalidationPaths = new List<string>(plan.Upload);

                if (_config.DeleteRemovedFiles)
                    invalidationPaths.AddRange(plan.Delete);

                await InvalidateFilesAsync(invalidationPaths, cancellationToken);
            }

            Debug.Log("Upload Finished.");
        }

        private async Task InvalidateFilesAsync(List<string> relativePaths, CancellationToken cancellationToken)
        {
            if (relativePaths.Count == 0)
                return;

            var objectKeys = new List<string>(relativePaths.Count);

            foreach (var relativePath in relativePaths)
                objectKeys.Add($"{_config.RemoteRoot}/{relativePath}".Replace("\\", "/"));

            await _config.CacheInvalidationProvider.InvalidateAsync(objectKeys, cancellationToken);
            Debug.Log(
                $"{_config.CacheInvalidationProvider.ProviderName} cache invalidated " +
                $"for {objectKeys.Count} uploaded files.");
        }

        private async Task UploadSingleFileAsync(string relativePath, SemaphoreSlim semaphore, CancellationToken cancellationToken, int totalFiles, Func<int> incrementCompleted, ConcurrentBag<Exception> uploadErrors)
        {
            bool entered = false;

            try
            {
                await semaphore.WaitAsync(cancellationToken);
                entered = true;

                cancellationToken.ThrowIfCancellationRequested();

                var localPath = Path.Combine(_config.LocalFolder, relativePath);

                await UploadFileAsync(localPath, relativePath, cancellationToken);

                var completed = incrementCompleted();

                OnUploadProgress?.Invoke(completed, totalFiles, (float)completed / totalFiles);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                uploadErrors.Add(new Exception($"Failed uploading: {relativePath}", ex));
            }
            finally
            {
                if (entered)
                    semaphore.Release();
            }
        }

        private async Task UploadFileAsync(string localFile, string relativePath, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var key = $"{_config.RemoteRoot}/{relativePath}".Replace("\\", "/");

            Exception lastException = null;

            for (int attempt = 1; attempt <= _config.RetryCount; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = _config.BucketName,
                        Key = key,
                        FilePath = localFile,
                        ContentType = GetContentType(localFile)
                    };

                    SetCacheControl(request, GetCacheControl(localFile));

                    if (_config.MakeUploadedFilesPublic)
                        request.CannedACL = S3CannedACL.PublicRead;

                    request.StreamTransferProgress += (_, e) =>
                    {
                        Debug.Log($"{relativePath} : {e.PercentDone}%");
                    };

                    await _client.PutObjectAsync(request, cancellationToken);

                    Debug.Log($"Uploaded : {relativePath}");
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    Debug.LogWarning($"Upload failed ({attempt}/{_config.RetryCount}) : {relativePath}\n{ex.Message}");

                    if (attempt < _config.RetryCount)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            throw new Exception($"Upload failed after {_config.RetryCount} attempts: {relativePath}", lastException);
        }

        private async Task DeleteRemoteFilesAsync(List<string> files, CancellationToken token)
        {
            ThrowIfDisposed();

            if (files == null || files.Count == 0)
                return;

            const int maxObjectsPerRequest = 1000;

            for (var start = 0; start < files.Count; start += maxObjectsPerRequest)
            {
                token.ThrowIfCancellationRequested();
                var count = Math.Min(maxObjectsPerRequest, files.Count - start);
                var objects = new List<KeyVersion>(count);

                for (var index = start; index < start + count; index++)
                {
                    var key = $"{_config.RemoteRoot}/{files[index]}".Replace("\\", "/");

                    objects.Add(new KeyVersion { Key = key });
                }

                var request = new DeleteObjectsRequest
                {
                    BucketName = _config.BucketName,
                    Objects = objects
                };

                var response = await _client.DeleteObjectsAsync(request, token);

                foreach (var deleted in response.DeletedObjects)
                    Debug.Log($"Deleted: {deleted.Key}");

                if (response.DeleteErrors.Count > 0)
                {
                    foreach (var error in response.DeleteErrors)
                        Debug.LogError($"Delete failed: {error.Key} - {error.Message}");

                    throw new Exception($"Failed deleting {response.DeleteErrors.Count} objects.");
                }
            }
        }

        public async Task<ManifestData> GetRemoteManifestAsync()
        {
            return await DownloadManifestAsync();
        }

        public UploadPlan CreateUploadPlan(ManifestData local, ManifestData remote)
        {
            return BuildUploadPlan(local, remote);
        }

        private async Task<ManifestData> DownloadManifestAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_cachedRemoteManifest != null)
                return _cachedRemoteManifest;

            var key = $"{_config.RemoteRoot}/{_config.ManifestFileName}".Replace("\\", "/");

            _cachedRemoteManifest = await DownloadJsonAsync<ManifestData>(key, cancellationToken);

            return _cachedRemoteManifest;
        }

        private async Task UploadManifestAsync(ManifestData manifest, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var key = $"{_config.RemoteRoot}/{_config.ManifestFileName}".Replace("\\", "/");

            await UploadJsonAsync(key, manifest, cancellationToken);

            _cachedRemoteManifest = manifest;

            Debug.Log($"Manifest uploaded: {key}");
        }

        private string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".json" => "application/json",
                ".hash" => "application/octet-stream",

                ".bundle" => "application/octet-stream",
                ".bytes" => "application/octet-stream",

                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",

                ".txt" => "text/plain",
                ".xml" => "application/xml",

                _ => "application/octet-stream"
            };
        }

        private string GetCacheControl(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".json" || extension == ".hash"
                ? _config.CatalogCacheControl
                : _config.ContentCacheControl;
        }

        private static void SetCacheControl(PutObjectRequest request, string cacheControl)
        {
            if (!string.IsNullOrWhiteSpace(cacheControl))
                request.Headers.CacheControl = cacheControl;
        }

        private async Task ValidateAsync(CancellationToken cancellationToken)
        {
            ValidateConfig();

            ValidateLocalFolder();

            await ValidateBucketAccess(cancellationToken);

            Debug.Log("Validation passed.");
        }

        private void ValidateConfig()
        {
            if (string.IsNullOrEmpty(_config.ServiceUrl))
                throw new Exception("S3 Service URL is empty.");

            if (string.IsNullOrEmpty(_config.BucketName))
                throw new Exception("Bucket name is empty.");

            if (string.IsNullOrEmpty(_config.AccessKey))
                throw new Exception("Access key is empty.");

            if (string.IsNullOrEmpty(_config.LocalFolder))
                throw new Exception("Local folder is empty.");
        }

        private void ValidateLocalFolder()
        {
            if (!Directory.Exists(_config.LocalFolder))
            {
                throw new DirectoryNotFoundException($"Local folder not found: {_config.LocalFolder}");
            }

            var files = Directory.GetFiles(_config.LocalFolder, "*", SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                throw new Exception("Local folder contains no files.");
            }
        }

        private async Task ValidateBucketAccess(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = _config.BucketName,
                    MaxKeys = 1
                };

                await _client.ListObjectsV2Async(request, cancellationToken);
            }
            catch (AmazonS3Exception e)
            {
                throw new Exception($"Cannot access bucket: {e.Message}", e);
            }
        }

        private async Task<T> DownloadJsonAsync<T>(string key, CancellationToken cancellationToken) where T : new()
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = _config.BucketName,
                    Key = key
                };

                using var response = await _client.GetObjectAsync(request, cancellationToken);
                using var reader = new StreamReader(response.ResponseStream);

                var json = await reader.ReadToEndAsync();

                return JsonConvert.DeserializeObject<T>(json) ?? new T();
            }
            catch (AmazonS3Exception e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound ||
    e.ErrorCode == "NoSuchKey")
                {
                    Debug.Log($"Remote file not found: {key}");
                    return new T();
                }

                throw;
            }
        }

        private async Task UploadJsonAsync<T>(string key, T data, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);

            var bytes = Encoding.UTF8.GetBytes(json);

            using var stream = new MemoryStream(bytes);

            var request = new PutObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = "application/json"
            };

            SetCacheControl(request, _config.CatalogCacheControl);

            await _client.PutObjectAsync(request, cancellationToken);
        }

        private UploadPlan BuildUploadPlan(ManifestData localManifest, ManifestData remoteManifest)
        {
            var plan = new UploadPlan
            {
                RemoteRoot = _config.RemoteRoot
            };

            foreach (var localFile in localManifest.Files)
            {
                if (!remoteManifest.Files.TryGetValue(localFile.Key, out var remoteHash))
                {
                    // New File
                    plan.Upload.Add(localFile.Key);
                    continue;
                }

                if (!string.Equals(localFile.Value, remoteHash, StringComparison.OrdinalIgnoreCase))
                {
                    // File edited
                    plan.Upload.Add(localFile.Key);
                    continue;
                }

                // No change
                plan.Skip.Add(localFile.Key);
            }

            // Files on remote but removed from local
            foreach (var remoteFile in remoteManifest.Files)
            {
                if (!localManifest.Files.ContainsKey(remoteFile.Key))
                {
                    plan.Delete.Add(remoteFile.Key);
                }
            }

            return plan;
        }
    }
}
