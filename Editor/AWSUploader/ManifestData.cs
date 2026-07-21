using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace DevDude.AWSUploader
{
    [Serializable]
    public class ManifestData
    {
        public Dictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase);
    }

    public static class UploadManifest
    {
        public static ManifestData Create(UploadConfig config)
        {
            return new ManifestData
            {
                Files = HashUtility.BuildManifest(config)
            };
        }

        public static ManifestData Load(string filePath)
        {
            if (!File.Exists(filePath))
                return new ManifestData();

            var json = File.ReadAllText(filePath);

            return JsonConvert.DeserializeObject<ManifestData>(json) ?? new ManifestData();
        }

        public static void Save(string filePath, ManifestData manifest)
        {
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);

            File.WriteAllText(filePath, json);
        }

        public static List<string> GetChangedFiles(ManifestData local, ManifestData remote)
        {
            var changed = new List<string>();

            foreach (var pair in local.Files)
            {
                if (!remote.Files.TryGetValue(pair.Key, out var remoteHash))
                {
                    changed.Add(pair.Key);
                    continue;
                }

                if (!string.Equals(pair.Value, remoteHash, StringComparison.OrdinalIgnoreCase))
                {
                    changed.Add(pair.Key);
                }
            }

            return changed;
        }
    }
}