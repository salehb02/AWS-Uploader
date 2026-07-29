using UnityEngine;

namespace DevDude.AWSUploader.Editor
{
    [CreateAssetMenu(fileName = "AWSUploadSettings", menuName = "DevDude/AWS Upload Settings")]
    public class AWSUploadSettings : ScriptableObject
    {
        [Header("S3")]
        public string serviceUrl;
        public string bucketName;
        public string accessKey;
        public bool forcePathStyle = true;
        public string authenticationRegion;

        [Header("Local")]
        public string localFolder;

        [Header("Remote")]
        public string remoteFolder = "Addressables";
        public string manifestFileName = ".upload-manifest.json";

        [Header("Upload")]
        [Min(1)]
        public int parallelUploads = 4;

        [Min(1)]
        public int retryCount = 3;

        public bool deleteRemovedFiles = false;
        public bool makeUploadedFilesPublic = false;

        [Tooltip("Cache-Control for catalog and .hash files.")]
        public string catalogCacheControl = "no-cache";

        [Tooltip("Cache-Control for versioned bundles and other content files.")]
        public string contentCacheControl = "public, max-age=31536000, immutable";

        [Header("Cache Invalidation")]
        [Tooltip("Optional CDN/cache provider to invalidate after a successful upload.")]
        public CacheInvalidationProviderType cacheInvalidationProvider = CacheInvalidationProviderType.None;

        [Header("ArvanCloud Provider")]
        [Tooltip("The domain configured in ArvanCloud CDN, without scheme or path.")]
        public string cdnDomain;

        [Tooltip("ArvanCloud CDN API base URL.")]
        public string arvanCdnApiUrl = "https://napi.arvancloud.ir/cdn/4.0";
    }
}
