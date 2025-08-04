using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SelfCdn.Parser
{
    internal class QualityParser
    {
        private static readonly Dictionary<string, string> QualityPatterns = new Dictionary<string, string>
        {
            ["8k|4320p|ultrahd8k|8k[\\s\\-]?uhd"] = "4320p",
            ["4k|2160p|uhd|ultra[\\s\\-]?hd|4k[\\s\\-]?uhd"] = "2160p",
            ["1440p|2k|qhd|quad[\\s\\-]?hd"] = "1440p",
            ["1080p|full[\\s\\-]?hd|fhd"] = "1080p",
            ["720p|hd(?!r)"] = "720p",
            ["480p|sd|standard[\\s\\-]?def"] = "480p",
            ["360p"] = "360p",
            ["240p"] = "240p",
            ["camrip|hdcam|cam|ts|telesync|tc|telecine|scr|screener|workprint|wp"] = "480p",
            ["bdrip|blu[\\s\\-]?ray|bd[\\s\\-]?remux|remux(?=\\b.*1080p)"] = "1080p",
            ["brrip(?=\\b.*720p)"] = "720p",
            ["web[\\s\\-]?dl|webdl(?=\\b.*1080p)"] = "1080p",
            ["web[\\s\\-]?dl|webdl(?=\\b.*720p)"] = "720p",
            ["web[\\s\\-]?dl|webdl(?=\\b.*480p)"] = "480p",
            ["web[\\s\\-]?rip|webrip(?=\\b.*720p)"] = "720p",
            ["web[\\s\\-]?dlrip|webdlrip(?=\\b.*720p)"] = "720p",
            ["dvd|dvd[\\s\\-]?rip|dvdrip|dvd[\\s\\-]?r|dvdr"] = "480p",
            ["tvrip|tv[\\s\\-]?rip|satrip|sat[\\s\\-]?rip|iptvrip|iptv[\\s\\-]?rip"] = "480p"
        };

        public static string Parse(string fileName)
        {
            fileName = fileName.ToLowerInvariant();
            return QualityPatterns.FirstOrDefault(kv => Regex.IsMatch(
                fileName, 
                $@"(^|[^a-z0-9])({kv.Key})([^a-z0-9]|$)",
                RegexOptions.IgnoreCase)).Value ?? "Unknown";
        }

        public static string GetQualityTagsPattern()
        {
            return string.Join("|", QualityPatterns.Keys.Select(k => k.Replace("[\\s\\-]", "(?:-|\\s)")));
        }
    }
}
