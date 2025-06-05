using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SelfCDN.Models
{
    public class NormalizedTitles
    {
        public string Title { get; }
        public string OriginalTitle { get; }

        public NormalizedTitles(string title, string originalTitle)
        {
            Title = title;
            OriginalTitle = originalTitle;
        }

        public bool Matches(string normalizedFileName)
        {
            return (!string.IsNullOrEmpty(Title) && IsTitleMatch(normalizedFileName, Title)) ||
                (!string.IsNullOrEmpty(OriginalTitle) && IsTitleMatch(normalizedFileName, OriginalTitle));
        }

        private bool IsTitleMatch(string normalizedFileName, string normalizedQueryTitle)
        {
            ConsoleLogger.Log("IsTitleMatch:");
            ConsoleLogger.Log($"Normalized file name: {normalizedFileName}");
            ConsoleLogger.Log($"Normalized query title: {normalizedFileName}");

            // Filter out media-related terms and uploader names
            List<string> fileWords = normalizedFileName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !Regex.IsMatch(w, @"^(bdrip|avc|h264|h265|x264|x265|web|webrip|dl|by|bluray|1080p|720p|rip|4k|uhd|dalemake)$", RegexOptions.IgnoreCase))
                .ToList();

            // Keep all query words, exclude stop words
            List<string> queryWords = normalizedQueryTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !string.Equals(w, "the", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int matchedWords = 0;
            int fileIndex = 0;

            foreach (var queryWord in queryWords)
            {
                if (fileIndex >= fileWords.Count)
                    break;

                int maxDistance = Math.Max(1, (int)Math.Ceiling(Math.Max(queryWord.Length, fileWords.Any() ? fileWords.Min(w => w.Length) : 1) * 0.25));
                if (string.Equals(fileWords[fileIndex], queryWord, StringComparison.OrdinalIgnoreCase) ||
                    LevenshteinDistance(queryWord, fileWords[fileIndex], 0.25) <= maxDistance)
                {
                    double distance = string.Equals(fileWords[fileIndex], queryWord, StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : LevenshteinDistance(queryWord, fileWords[fileIndex], 0.25);
                    ConsoleLogger.Log($"Matched '{queryWord}' to '{fileWords[fileIndex]}' at index {fileIndex} (Levenshtein distance: {distance})");
                    matchedWords++;
                    fileIndex++;
                }
                else
                {
                    ConsoleLogger.Log($"No match for '{queryWord}' at index {fileIndex}");
                    fileIndex++;
                }
            }

            ConsoleLogger.Log($"Matched words: {matchedWords}/{queryWords.Count}");

            // For single-word queries, require exact match or distance <= 1
            if (queryWords.Count == 1)
            {
                return matchedWords == 1 &&
                    (string.Equals(queryWords[0], fileWords[0], StringComparison.OrdinalIgnoreCase)
                    || LevenshteinDistance(queryWords[0], fileWords[0], 0.25) <= 1);
            }

            return matchedWords >= Math.Max(1, (int)Math.Ceiling(queryWords.Count * 0.75));
        }

        private double LevenshteinDistance(string s, string t, double maxDistanceRatio = 0.25)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            // Convert to lowercase for case-insensitive comparison
            s = s.ToLower();
            t = t.ToLower();

            int[,] d = new int[s.Length + 1, t.Length + 1];
            for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= t.Length; j++) d[0, j] = j;

            int maxDistance = (int)Math.Ceiling(Math.Max(s.Length, t.Length) * maxDistanceRatio);

            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1; // Case-insensitive comparison handled by ToLower
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);

                    // Early termination if distance exceeds threshold
                    if (d[i, j] > maxDistance)
                    {
                        return d[i, j]; // Return early if distance is too large
                    }
                }
            }

            return d[s.Length, t.Length];
        }
    }
}
