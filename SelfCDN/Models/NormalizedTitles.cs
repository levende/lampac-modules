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
            ConsoleLogger.Log($"Normalized query title: {normalizedQueryTitle}");

            // Split and filter file words, excluding irrelevant terms
            List<string> fileWords = normalizedFileName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !Regex.IsMatch(w, @"^(bdrip|avc|h264|h265|x264|x265|web|webrip|dl|by|bluray|1080p|720p|rip|4k|uhd|dalemake)$", RegexOptions.IgnoreCase))
                .ToList();

            // Split and filter query words, excluding common words like "the"
            List<string> queryWords = normalizedQueryTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !string.Equals(w, "the", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int matchedWords = 0;

            // For each query word, find a matching file word (non-sequential)
            foreach (var queryWord in queryWords)
            {
                bool foundMatch = false;
                int maxDistance = Math.Max(1, (int)Math.Ceiling(queryWord.Length * 0.3)); // Increase tolerance to 30%

                foreach (var fileWord in fileWords)
                {
                    if (string.Equals(fileWord, queryWord, StringComparison.OrdinalIgnoreCase) ||
                        LevenshteinDistance(queryWord, fileWord, 0.3) <= maxDistance)
                    {
                        double distance = string.Equals(fileWord, queryWord, StringComparison.OrdinalIgnoreCase)
                            ? 0
                            : LevenshteinDistance(queryWord, fileWord, 0.3);
                        ConsoleLogger.Log($"Matched '{queryWord}' to '{fileWord}' (Levenshtein distance: {distance})");
                        matchedWords++;
                        fileWords.Remove(fileWord); // Prevent re-matching the same word
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    ConsoleLogger.Log($"No match for '{queryWord}'");
                }
            }

            ConsoleLogger.Log($"Matched words: {matchedWords}/{queryWords.Count}");

            // Require 75% of query words to match, or at least 1 for single-word queries
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
