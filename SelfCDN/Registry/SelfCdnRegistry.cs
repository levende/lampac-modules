using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SelfCdn.Registry.Models;
using System.Collections.Generic;
using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using SelfCdn.Models;
using SelfCDN.OpenAi;
using SelfCDN.Models;

namespace SelfCDN.Registry
{
    public class SelfCdnRegistry
    {
        private static readonly string[] SupportedFileExtensions = { ".mp4", ".mkv", ".avi" };

        private readonly string _databaseFilePath;
        private readonly string _scanDirectoryPath;
        private readonly int _skipModificationTime;
        private readonly OpenAiSettings _openAiSettings;

        private readonly TmdbMapper _tmdbMapper;

        public SelfCdnRegistry(
            string databaseFilePath,
            string scanDirectoryPath,
            string tmdbApiKey,
            OpenAiSettings openAiSettings,
            string tmdbLang = "en-US",
            int skipModificationTime = 60)
        {
            _openAiSettings = openAiSettings;
            _databaseFilePath = databaseFilePath;
            _scanDirectoryPath = scanDirectoryPath;
            _skipModificationTime = skipModificationTime;

            _tmdbMapper = new TmdbMapper(tmdbApiKey, tmdbLang);
        }

        public RegistryStorage Storage { get; private set; } = new();

        public async Task ScanAsync()
        {
            if (string.IsNullOrEmpty(_openAiSettings.ApiUrl))
            {
                return;
            }

            Logger.Log("[SelfCdnRegistry] Start scan");

            await Storage.LoadAsync(_databaseFilePath);
            Storage.PruneMissedFiles();

            var skipFilePaths = Storage.Recognized
                .SelectMany(f => f.Value)
                .Select(f => f.FilePath)
                .Concat(Storage.Unrecognized)
                .Concat(Storage.Ignored)
                .ToList();

            var skipSet = new HashSet<string>(
                skipFilePaths.Select(path => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)),
                StringComparer.OrdinalIgnoreCase);

            Logger.Log(() =>
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
                    string fullPath = Path.GetFullPath(fileInfo.FullName)
                        .TrimEnd(Path.DirectorySeparatorChar);

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

            Logger.Log(() =>
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
                Logger.Log("[SelfCdnRegistry] Stop scan");
                return;
            }

            using var llmParser = new OpenAiMediaMetadataExtractor(_openAiSettings);

            for (var i = 0; i < scanFiles.Count; i += _openAiSettings.BatchSize.Value)
            {
                var batch = scanFiles
                    .Skip(i)
                    .Take(_openAiSettings.BatchSize.Value)
                    .ToList();

                Logger.Log(() =>
                {
                    var json = JsonSerializer.Serialize(
                        batch,
                        new JsonSerializerOptions
                        {
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                            WriteIndented = true,
                        });

                    return $"[SelfCdnRegistry] LLM Processing batch #{(i / _openAiSettings.BatchSize) + 1}: {json}";
                });

                try
                {
                    await ProcessFilesAsync(llmParser, batch);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ERROR] {ex.Message}, {ex.StackTrace}");
                    break;
                }

                if (i + _openAiSettings.BatchSize < scanFiles.Count)
                {
                    Logger.Log($"[SelfCdnRegistry] Batch delay: {_openAiSettings.BatchTimeoutSec}");
                    await Task.Delay(TimeSpan.FromSeconds(_openAiSettings.BatchTimeoutSec.Value));
                }
            }

            Logger.Log("[SelfCdnRegistry] Stop scan");
        }

        private async Task<IReadOnlyCollection<MediaMetadata>> ExtractRetryAsync(
            IMediaMetadataExtractor mediaMetadataExtractor,
            List<string> filePaths,
            int allowedRetries,
            int retryTimeoutSec)
        {
            int currentAttempt = 0;

            while (true)
            {
                try
                {
                    currentAttempt++;
                    return await mediaMetadataExtractor.ExtractAsync(filePaths);
                }
                catch (Exception ex) when (currentAttempt <= allowedRetries)
                {
                    Logger.Log(($"Attempt {currentAttempt} failed. Error: {ex.Message}"));

                    if (currentAttempt >= allowedRetries)
                    {
                        throw;
                    }

                    Logger.Log(($"Attempt timeout: {retryTimeoutSec}s"));
                    await Task.Delay(TimeSpan.FromSeconds(retryTimeoutSec));
                }
            }
        }

        private async Task ProcessFilesAsync(
            IMediaMetadataExtractor mediaMetadataExtractor,
            List<string> filePaths)
        {
            var mediaMetadataItems = await ExtractRetryAsync(
                mediaMetadataExtractor,
                filePaths,
                3,
                120);

            Logger.Log(() =>
            {
                var json = JsonSerializer.Serialize(
                    mediaMetadataItems,
                    new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true,
                    });

                return $"[SelfCdnRegistry] LLM Processing batch result: {json}";
            });

            mediaMetadataItems
                .Where(f => string.IsNullOrWhiteSpace(f.Title))
                .ToList()
                .ForEach(f => Storage.AddUnrecognized(f.FilePath));

            mediaMetadataItems = mediaMetadataItems
                .Where(f => !string.IsNullOrWhiteSpace(f.Title))
                .ToList();

            var tmdbResults = new List<FileTmdbInfo>();

            foreach (var file in mediaMetadataItems)
            {
                var tmdbResult = await _tmdbMapper.MapToTmdbAsync(file);
                tmdbResults.Add(tmdbResult);
            }

            Logger.Log(() =>
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
        }
    }
}