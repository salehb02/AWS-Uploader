using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace DevDude.AWSUploader
{
    public static class HashUtility
    {
        /// <summary>
        /// Calculates SHA256 hash of a file.
        /// </summary>
        public static string ComputeFileHash(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();

            var hash = sha.ComputeHash(stream);

            return BytesToHex(hash);
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new System.Text.StringBuilder(bytes.Length * 2);

            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        /// <summary>
        /// Builds a manifest (RelativePath -> SHA256)
        /// </summary>
        public static Dictionary<string, string> BuildManifest(UploadConfig config)
        {
            var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(config.LocalFolder, "*", SearchOption.AllDirectories))
            {
                if (ShouldIgnore(file, config))
                    continue;

                var relativePath = Path.GetRelativePath(config.LocalFolder, file).Replace("\\", "/");

                manifest[relativePath] = ComputeFileHash(file);
            }

            return manifest;
        }

        private static bool ShouldIgnore(string file, UploadConfig config)
        {
            if (config.IgnoreHiddenFiles)
            {
                var attributes = File.GetAttributes(file);

                if ((attributes & FileAttributes.Hidden) != 0)
                    return true;
            }

            var extension = Path.GetExtension(file);

            if (config.IgnoredExtensions.Any(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var fileName = Path.GetFileName(file);

            if (config.IgnoredFiles.Contains(fileName))
                return true;

            return false;
        }
    }
}