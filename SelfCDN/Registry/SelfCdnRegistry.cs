using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SelfCdn.Registry.Models;
using System.Collections.Generic;
using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using SelfCDN.MediaMetadataExtractor;

namespace SelfCDN.Registry
{
    public class SelfCdnRegistry
    {
        private static readonly string[] SupportedFileExtensions = { ".mp4", ".mkv", ".avi" };

        private readonly string _databaseFilePath;
        private readonly string _scanDirectoryPath;
        private readonly int _skipModificationTime;
        private readonly string _llmApiKey;
        private readonly string _llmModel;

        private readonly TmdbMapper _tmdbMapper;

        public SelfCdnRegistry(
            string databaseFilePath,
            string scanDirectoryPath,
            string tmdbApiKey,
            string llmApiKey,
            string llmModel,
            string tmdbLang = "en-US",
            int skipModificationTime = 60)
        {
            _databaseFilePath = databaseFilePath;
            _scanDirectoryPath = scanDirectoryPath;
            _skipModificationTime = skipModificationTime;
            _llmApiKey = llmApiKey;
            _llmModel = llmModel;

            _tmdbMapper = new TmdbMapper(tmdbApiKey, tmdbLang);
        }

        public RegistryStorage Storage { get; private set; } = new();

        public async Task ScanAsync()
        {
            ConsoleLogger.Log("[SelfCdnRegistry] Start scan");

            await Storage.LoadAsync(_databaseFilePath);
            //Storage.PruneMissedFiles();

            var skipFilePaths = Storage.Recognized
                .SelectMany(f => f.Value)
                .Select(f => f.FilePath)
                .Concat(Storage.Unrecognized)
                .Concat(Storage.Ignored)
                .ToList();

            var skipSet = new HashSet<string>(
                skipFilePaths.Select(path => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)),
                StringComparer.OrdinalIgnoreCase);

            ConsoleLogger.Log(() =>
            {
                var json = JsonSerializer.Serialize(
                    skipFilePaths,
                    new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true,
                    });

                return $"[SelfCdnRegistry] already processed files: {json}";
            });

            var scanFiles = SupportedFileExtensions
                .SelectMany(ext => Directory.EnumerateFiles(
                    _scanDirectoryPath,
                    $"*.{ext.TrimStart('.')}",
                    SearchOption.AllDirectories))
                .Select(file => new FileInfo(file))
                .Where(fileInfo =>
                {
                    string fullPath = Path.GetFullPath(fileInfo.FullName).TrimEnd(Path.DirectorySeparatorChar);

                    if (skipSet.Contains(fullPath))
                    {
                        return false;
                    }

                    if (fileInfo.Length == 0)
                    {
                        return false;
                    }

                    var time = fileInfo.CreationTime > fileInfo.LastWriteTime
                        ? fileInfo.CreationTime
                        : fileInfo.LastWriteTime;

                    return time.AddMinutes(_skipModificationTime) <= DateTime.Now;
                })
                .Select(fileInfo => fileInfo.FullName)
                .Distinct()
                .ToList();

            ConsoleLogger.Log(() =>
            {
                var json = JsonSerializer.Serialize(
                    scanFiles,
                    new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true,
                    });

                return $"[SelfCdnRegistry] Files for processing: {json}";
            });

            if (scanFiles.Count == 0)
            {
                await Storage.SaveAsync(_databaseFilePath);
                ConsoleLogger.Log("[SelfCdnRegistry] Stop scan");
                return;
            }

            using var llmParser = new GroqMediaMetadataExtractor(_llmApiKey, _llmModel);
            var scanFileMediaInfo = await llmParser.ExtractAsync(scanFiles);

            ConsoleLogger.Log(() =>
            {
                var json = JsonSerializer.Serialize(
                    scanFileMediaInfo,
                    new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true,
                    });

                return $"[SelfCdnRegistry] Recognized media items: {json}";
            });

            scanFileMediaInfo
                .Where(f => string.IsNullOrWhiteSpace(f.Title))
                .ToList()
                .ForEach(f => Storage.AddUnrecognized(f.FileName));

            scanFileMediaInfo = scanFileMediaInfo
                .Where(f => !string.IsNullOrWhiteSpace(f.Title))
                .ToList();

            var tmdbResults = new List<FileTmdbInfo>();

            foreach (var file in scanFileMediaInfo)
            {
                var tmdbResult = await _tmdbMapper.MapToTmdbAsync(file);
                tmdbResults.Add(tmdbResult);
            }

            ConsoleLogger.Log(() =>
            {
                var json = JsonSerializer.Serialize(
                    tmdbResults,
                    new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true,
                    });

                return $"[SelfCdnRegistry] Recognized TMDB items: {json}";
            });

            tmdbResults
                .Where(result => result.TmdbId != 0)
                .ToList()
                .ForEach(r => Storage.AddRecognized(r.TmdbId, r.MediaType, new RegistryMediaItem
                {
                    EpisodeNumber = r.EpisodeNumber,
                    SeasonNumber = r.SeasonNumber,
                    FilePath = r.FileName
                }));

            tmdbResults
                .Where(result => result.TmdbId == 0)
                .ToList()
                .ForEach(r => Storage.AddUnrecognized(r.FileName));

            await Storage.SaveAsync(_databaseFilePath);
            ConsoleLogger.Log("[SelfCdnRegistry] Stop scan");
        }
    }
}
