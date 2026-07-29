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
