using System.Collections.Generic;
using DevDude.AWSUploader.CacheInvalidation;

namespace DevDude.AWSUploader
{
    public class UploadConfig
    {
        /// <summary>
        /// S3-compatible endpoint.
        /// Example: https://s3.example.com
        /// </summary>
        public string ServiceUrl;

        /// <summary>
        /// Bucket Name
        /// </summary>
        public string BucketName;

        /// <summary>
        /// Access Key
        /// </summary>
        public string AccessKey;

        /// <summary>
        /// Use path-style S3 URLs. Some providers require this; AWS S3 usually does not.
        /// </summary>
        public bool ForcePathStyle = true;

        /// <summary>
        /// Optional S3 authentication region for providers that require one.
        /// </summary>
        public string AuthenticationRegion;

        /// <summary>
        /// Secret Key
        /// </summary>
        // public string SecretKey; // Now we read secret key from OS environment config

        /// <summary>
        /// Folder that will be uploaded.
        /// Example:
        /// D:/Project/ServerData/Android/1.4.2
        /// </summary>
        public string LocalFolder;

        /// <summary>
        /// Remote Folder inside Bucket.
        /// Example:
        /// Android/1.4.2
        /// </summary>
        public string RemoteFolder;

        /// <summary>
        /// Number of simultaneous uploads.
        /// </summary>
        public int ParallelUploads = 4;

        /// <summary>
        /// Retry count for failed uploads.
        /// </summary>
        public int RetryCount = 3;

        /// <summary>
        /// Ignore hidden files.
        /// </summary>
        public bool IgnoreHiddenFiles = true;

        /// <summary>
        /// Upload only changed files.
        /// </summary>
        public bool UploadChangedFilesOnly = true;

        /// <summary>
        /// Overwrite remote files if hash changed.
        /// </summary>
        public bool OverwriteExistingFiles = true;

        /// <summary>
        /// Upload upload-manifest.json after upload finishes.
        /// </summary>
        public bool UploadManifest = true;

        /// <summary>
        /// Delete remote files that no longer exist locally.
        /// Disabled by default for versioned Addressables.
        /// </summary>
        public bool DeleteRemovedFiles = false;

        /// <summary>
        /// Set public flag for uploaded files
        /// </summary>
        public bool MakeUploadedFilesPublic = false;

        /// <summary>
        /// Cache-Control value for Addressables catalogs and hash files.
        /// </summary>
        public string CatalogCacheControl = "no-cache";

        /// <summary>
        /// Cache-Control value for versioned bundles and other content files.
        /// </summary>
        public string ContentCacheControl = "public, max-age=31536000, immutable";

        /// <summary>
        /// Ignore file extensions.
        /// </summary>
        public readonly List<string> IgnoredExtensions = new()
        {
            ".meta",
            ".DS_Store",
            ".tmp"
        };

        /// <summary>
        /// Ignore exact file names.
        /// </summary>
        public readonly List<string> IgnoredFiles = new()
        {
            "Thumbs.db"
        };

        /// <summary>
        /// Manifest filename.
        /// </summary>
        public string ManifestFileName = ".upload-manifest.json";

        /// <summary>
        /// Final remote root address
        /// </summary>
        public string RemoteRoot;

        /// <summary>
        /// Optional cache implementation. When set, successfully uploaded object keys are
        /// invalidated after the files and manifest have been uploaded.
        /// Ownership is transferred to AWSUploader and it is disposed with the uploader.
        /// </summary>
        public ICacheInvalidationProvider CacheInvalidationProvider;
    }
}
