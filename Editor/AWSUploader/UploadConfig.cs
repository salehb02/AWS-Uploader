using System.Collections.Generic;

namespace DevDude.AWSUploader
{
    public class UploadConfig
    {
        /// <summary>
        /// Arvan S3 Endpoint
        /// Example: https://s3.ir-thr-at1.arvanstorage.ir
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
        public bool MakeUploadedFilesPublic = true;

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
    }
}