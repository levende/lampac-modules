using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SelfCDN
{
    public static class QualityClassifier
    {
        private static Dictionary<string, string> QualityPatterns = new Dictionary<string, string>()
        {
            ["4k|2160p|uhd|ultra[\\s\\-]?hd"] = "2160p",
            ["1080p|full[\\s\\-]?hd|fhd|bdrip|blu[\\-\\s]?ray|remux|web[\\-\\s]?dl(?=\\b.*1080p)"] = "1080p",
            ["web[\\-\\s]?dlrip|720p|hd(?!r)|hdrip|web[\\-\\s]?rip|web[\\-\\s]?dl(?=\\b.*720p)|brrip"] = "720p",
            ["480p|sd|dvd|dvdrip|tvrip|satrip|iptvrip|web[\\-\\s]?dl(?=\\b.*480p)"] = "480p",
            ["camrip|hdcam|cam|ts|telesync|tc|telecine|scr|screener|workprint|wp"] = "480p",
        };

        static bool Contains(string input, string pattern) =>
            Regex.IsMatch(input, $@"(^|[^a-z0-9])({pattern})([^a-z0-9]|$)", RegexOptions.IgnoreCase);

        public static string Classify(string fileName, string filePath)
        {
            fileName = fileName.ToLowerInvariant();

            string detectedPattern = QualityPatterns.FirstOrDefault(kv => Contains(fileName, kv.Key)).Value;

            if (!string.IsNullOrEmpty(detectedPattern))
            {
                return detectedPattern;
            }

            FileInfo fileInfo = new FileInfo(filePath);
            long sizeInMb = fileInfo.Length / (1024 * 1024);

            return sizeInMb switch
            {
                > 4000 => "2160p",
                > 1000 => "1080p",
                > 500 => "720p",
                _ => "480p"
            };
        }
    }
}
