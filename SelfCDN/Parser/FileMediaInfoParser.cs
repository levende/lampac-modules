using System;
using System.IO;
using System.Text.RegularExpressions;
using SelfCdn.Registry.Models;

namespace SelfCdn.Registry.Parser
{
    internal static class MediaFileParser
    {
        private static string NormalizeName(string fileName)
        {
            // Remove file extension and normalize delimiters
            string name = Regex.Replace(fileName, @"\.[a-zA-Z0-9]+$", "", RegexOptions.IgnoreCase);
            return Regex.Replace(name, @"[._]", " ").Trim();
        }

        private static (int? Season, int? Episode) ParseSeasonEpisode(string normalizedName, bool isOva)
        {
            // Prefer SxxExx pattern
            var sxxExxMatch = Regex.Match(normalizedName, @"S(\d{1,2})E(\d{1,2})", RegexOptions.IgnoreCase);
            if (sxxExxMatch.Success)
            {
                return (int.Parse(sxxExxMatch.Groups[1].Value), int.Parse(sxxExxMatch.Groups[2].Value));
            }

            if (isOva) return (null, null);

            // Other patterns: Season xx Episode xx, Part xx, Exx, Episode xx, [xx] or [xx-x]
            var match = Regex.Match(normalizedName,
                @"(?:Season\s*(\d{1,2})\s*Episode\s*#?(\d{1,2}))|(?:Part\s*(\d{1,2}))|(?:E(\d{1,2}))|(?:Episode\s*#?(\d{1,2}))|(?:\[(\d{1,2})(?:-\d)?\])",
                RegexOptions.IgnoreCase);

            if (!match.Success) return (null, null);

            if (match.Groups[1].Success && match.Groups[2].Success) // Season xx Episode xx
                return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
            if (match.Groups[3].Success) // Part xx
                return (null, int.Parse(match.Groups[3].Value));
            if (match.Groups[4].Success) // Exx
                return (null, int.Parse(match.Groups[4].Value));
            if (match.Groups[5].Success) // Episode xx
                return (null, int.Parse(match.Groups[5].Value));
            if (match.Groups[6].Success) // [xx] or [xx-x]
                return (null, int.Parse(match.Groups[6].Value));

            return (null, null);
        }

        private static string GetTitlePattern(int? season, int? episode, string qualityTags)
        {
            if (season.HasValue && episode.HasValue)
            {
                return $@"^(?:\[\d+\]\s*)?(.*?)(?=\s+S0?{season}E0?{episode}\b|\s+\d{{4}}\b)|S0?{season}E0?{episode}\s+(.*?)(?=\s+[\[\(\-]|\d{{4}}\b|\b{qualityTags}\b|$)";
            }
            return $@"^(?:\[[^\]]+\]\s*)?(.+?)(?=\s*(?:\(\d{{4}}\)|\d{{4}}|\[[^\]]+\]|\({qualityTags}\)|\b{qualityTags}\b)|$)";
        }

        private static string GetEpisodeNamePattern(int? season, int? episode, string qualityTags)
        {
            if (season.HasValue && episode.HasValue)
            {
                return $@"(?<=S0?{season}E0?{episode}\s*[-:]?\s*)(.*?)(?=\s*(?:\[.*?({qualityTags})\]|\d{{4}}|\[|\(|$))";
            }
            return $@"(?<=[\[]\d{{1,2}}(?:-\d)?[\]]\s*[-:]?\s*)(.*?)(?=\s*(?:\[.*?({qualityTags})\]|\d{{4}}|\[|\(|$))";
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = Regex.Replace(text, @"\s*\($", "").Trim(); // Remove trailing parentheses
            text = Regex.Replace(text, @"\s{2,}", " ").Trim(); // Normalize spaces
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        public static MediaMetadata ParseMediaFileName(string filePath)
        {
            var mediaInfo = new MediaMetadata { FileName = filePath };

            var fileName = Path.GetFileName(filePath);
            string normalizedName = NormalizeName(fileName);

            // Check for OVA
            bool isOva = Regex.IsMatch(normalizedName, @"\bOVA\b", RegexOptions.IgnoreCase);

            // Parse season and episode
            (mediaInfo.SeasonNumber, mediaInfo.EpisodeNumber) = ParseSeasonEpisode(normalizedName, isOva);

            // Parse year
            var yearMatch = Regex.Match(normalizedName, @"(?:19\d{2}|20\d{2})(?=\s|$|[^\d(])");
            if (yearMatch.Success)
                mediaInfo.Year = int.TryParse(yearMatch.Groups[0].Value, out var year) ? year : null;

            // Get quality tags pattern
            string qualityTags = QualityParser.GetQualityTagsPattern();

            // Parse title
            string titlePattern = GetTitlePattern(mediaInfo.SeasonNumber, mediaInfo.EpisodeNumber, qualityTags);
            var titleMatch = Regex.Match(normalizedName, titlePattern, RegexOptions.IgnoreCase);
            if (titleMatch.Success)
                mediaInfo.Title = CleanText(titleMatch.Groups[1].Value);

            // Parse episode name (skip for OVAs)
            if (!isOva && mediaInfo.EpisodeNumber.HasValue)
            {
                string episodeNamePattern = GetEpisodeNamePattern(mediaInfo.SeasonNumber, mediaInfo.EpisodeNumber, qualityTags);
                var episodeNameMatch = Regex.Match(normalizedName, episodeNamePattern, RegexOptions.IgnoreCase);
                if (episodeNameMatch.Success)
                    mediaInfo.EpisodeName = CleanText(Regex.Replace(episodeNameMatch.Groups[1].Value, @"\s*\([^)]+\)$", ""));
            }

            // Fallback: Split episode name if title is missing
            if (mediaInfo.EpisodeName != null && mediaInfo.Title == null)
            {
                var episodeParts = mediaInfo.EpisodeName.Split(new[] { " - ", ": " }, StringSplitOptions.None);
                if (episodeParts.Length > 1)
                {
                    mediaInfo.Title = CleanText(episodeParts[0]);
                    mediaInfo.EpisodeName = CleanText(episodeParts[1]);
                }
            }

            // Fallback for title if still missing
            if (string.IsNullOrEmpty(mediaInfo.Title))
            {
                var fallbackMatch = Regex.Match(normalizedName, GetTitlePattern(null, null, qualityTags), RegexOptions.IgnoreCase);
                if (fallbackMatch.Success)
                    mediaInfo.Title = CleanText(fallbackMatch.Groups[1].Value);
            }

            return mediaInfo;
        }
    }
}
