using System.Collections.Generic;
using System.Text;

namespace DevDude.AWSUploader
{
    public class UploadPlan
    {
        public string RemoteRoot;

        public List<string> Upload = new();
        public List<string> Skip = new();
        public List<string> Delete = new();


        public string GetSummary()
        {
            var builder = new StringBuilder();

            builder.AppendLine("===== Upload Plan =====");
            builder.AppendLine($"Remote : {RemoteRoot}");
            builder.AppendLine($"Upload : {Upload.Count}");
            builder.AppendLine($"Skip   : {Skip.Count}");
            builder.AppendLine($"Delete : {Delete.Count}");
            builder.AppendLine("=======================");

            return builder.ToString();
        }

        public string GetDetailedSummary()
        {
            var builder = new StringBuilder();

            builder.AppendLine(GetSummary());

            if (Upload.Count > 0)
            {
                builder.AppendLine("\nFiles to Upload:");

                foreach (var file in Upload)
                    builder.AppendLine($" + {file}");
            }

            if (Skip.Count > 0)
            {
                builder.AppendLine("\nFiles Skipped:");

                foreach (var file in Skip)
                    builder.AppendLine($" = {file}");
            }

            if (Delete.Count > 0)
            {
                builder.AppendLine("\nFiles to Delete:");

                foreach (var file in Delete)
                    builder.AppendLine($" - {file}");
            }

            return builder.ToString();
        }
    }
}