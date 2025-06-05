using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Shared.Model.Templates;
using SelfCDN.Models;
using SelfCDN.Templates;

namespace SelfCDN.Controllers
{
    public class SelfCDNController : BaseController
    {
        private const string CacheKeyPrefix = "SelfCDN:view:";

        private static readonly string UnknownTranslationId = string.Empty;
        private static readonly string UnknownTranslationName = string.Empty;

        private readonly FileService _fileService;

        public SelfCDNController()
        {
            _fileService = new FileService(ModInit.StoragePath, memoryCache);
        }

        [Route("selfCDN/stream")]
        public ActionResult Stream(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Forbid();
            }

            string fullPath = Path.GetFullPath(Path.Combine(ModInit.StoragePath, path));

            if (!System.IO.File.Exists(fullPath))
            {
                return Forbid();
            }

            if (!fullPath.StartsWith(Path.GetFullPath(ModInit.StoragePath)))
            {
                return Forbid();
            }

            if (!ModInit.VideoExtensions.Contains(Path.GetExtension(fullPath).ToLower()))
            {
                return Forbid();
            }

            return File(System.IO.File.OpenRead(fullPath), "application/octet-stream", true);
        }

        [HttpGet]
        [Route("selfCDN")]
        public async Task<ActionResult> Index(long id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, int year, string source, int serial, string t, int s = -1, bool rjson = false)
        {
            try
            {
                var parameters = new MediaRequestParameters
                {
                    Id = id,
                    ImdbId = imdb_id,
                    KinopoiskId = kinopoisk_id,
                    OriginalLanguage = original_language,
                    OriginalTitle = original_title,
                    ReturnJson = rjson,
                    Season = s,
                    Serial = serial,
                    Title = title,
                    Translation = t,
                    UserId = requestInfo.user_uid,
                    Year = year,
                };

                var result = await SearchMedia(parameters);

                if (result == null)
                {
                    LogSearchFailure(parameters);
                    return Ok();
                }

                return result.Match(
                    movies => RenderMovies(parameters, movies),
                    series => RenderSeries(parameters, series));
            }
            catch (Exception ex)
            {
                ConsoleLogger.Log(ex.Message + Environment.NewLine + (ex.StackTrace ?? string.Empty));
                throw;
            }
        }


        private Task<MediaSearchResult> SearchMedia(MediaRequestParameters parameters)
        {
            var cacheKey = $"{CacheKeyPrefix}{parameters.KinopoiskId}:{parameters.ImdbId}:{parameters.Title}";

            if (hybridCache.TryGetValue(cacheKey, out MediaSearchResult cachedResult))
            {
                return Task.FromResult(cachedResult);
            }

            MediaSearchResult result = new MediaSearchResult();

            var files = _fileService.GetVideoFiles();
            if (files.Count == 0)
            {
                return Task.FromResult(result);
            }

            var normalizedTitles = new NormalizedTitles(
                NormalizeTitle(parameters.Title),
                NormalizeTitle(parameters.OriginalTitle));

            if (parameters.IsMovie)
            {
                result.Movies = FindMovies(files, normalizedTitles, parameters.Year, parameters.UserId);
            }
            else
            {
                result.Series = FindSeries(files, normalizedTitles, parameters.UserId);
            }

            if (result.HasContent)
            {
                hybridCache.Set(cacheKey, result, cacheTime(60));
            }

            return Task.FromResult(result);
        }

        private List<Movie> FindMovies(List<string> files, NormalizedTitles titles, int year, string userId)
        {
            return files.Select(file => ParseMovieFile(file, titles, year, userId))
               .Where(movie => movie != null)
               .ToList();
        }

        private Dictionary<string, List<Voice>> FindSeries(List<string> files, NormalizedTitles titles, string userId)
        {
            var episodesBySeason = files.Select(file => ParseEpisodeFile(file, titles, userId))
               .Where(episode => episode != null)
               .OrderBy(episode => episode.season)
               .GroupBy(episode => NormalizeSeasonNumber(episode.season))
               .ToDictionary(
                   group => group.Key,
                   group => new List<Voice>
                   {
                       new()
                       {
                           id = UnknownTranslationId,
                           name = UnknownTranslationName,
                           episodes = group.ToList()
                       }
                   });

            return episodesBySeason;
        }

        private Movie ParseMovieFile(string filePath, NormalizedTitles titles, int queryYear, string userId)
        {
            var fileInfo = new FileInfo(filePath);
            Console.WriteLine(filePath);

            if (!IsTitleMatch(fileInfo.Name, titles) || HasYearMismatch(fileInfo.Name, queryYear))
                return null;

            var quality = QualityClassifier.Classify(fileInfo.Name, filePath);
            var streamUrl = GetStreamUrl(filePath, userId);

            return new Movie
            {
                translation = fileInfo.Name,
                links = new List<(string Link, string Quality)> { (streamUrl, quality) }
            };
        }

        private Serial ParseEpisodeFile(string filePath, NormalizedTitles titles, string userId)
        {
            var fileInfo = new FileInfo(filePath);

            if (!IsTitleMatch(fileInfo.Name, titles))
            {
                return null;
            }

            var episodeInfo = ExtractEpisodeInfo(fileInfo.Name);
            if (episodeInfo == null)
            {
                return null;
            }

            var quality = QualityClassifier.Classify(fileInfo.Name, filePath);
            var streamUrl = GetStreamUrl(filePath, userId);

            return new Serial
            {
                id = episodeInfo.EpisodeNumber,
                season = episodeInfo.SeasonNumber,
                links = new List<(string Link, string Quality)> { (streamUrl, quality) }
            };
        }

        private string GetStreamUrl(string filePath, string userId)
        {
            var relativePath = Path.GetRelativePath(ModInit.StoragePath, filePath).Replace("\\", "/");
            return $"{host}/selfCDN/stream?path={Uri.EscapeDataString(relativePath)}&uid={Uri.EscapeDataString(userId)}";
        }

        private bool IsTitleMatch(string fileName, NormalizedTitles titles)
        {
            var normalizedFileName = NormalizeTitle(Path.GetFileNameWithoutExtension(fileName));
            return titles.Matches(normalizedFileName);
        }

        private bool HasYearMismatch(string fileName, int queryYear)
        {
            if (queryYear <= 0)
            {
                return false;
            }

            var yearMatch = Regex.Match(fileName, @"\b(19\d{2}|20\d{2})\b", RegexOptions.IgnoreCase);
            if (yearMatch.Success)
            {
                int fileYear = int.Parse(yearMatch.Groups[1].Value);
                return Math.Abs(fileYear - queryYear) > 1;
            }

            return false;
        }

        private EpisodeInfo ExtractEpisodeInfo(string fileName)
        {
            var match = Regex.Match(fileName,
                @"(?:S(\d+)|Season\s*(\d+)).*?(?:E(\d+)|Episode\s*(\d+))",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return null;
            }

            return new EpisodeInfo(
                match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value,
                match.Groups[3].Success ? match.Groups[3].Value : match.Groups[4].Value
            );
        }

        private string NormalizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            if (Transliterator.HasCyrillic(title))
            {
                title = Transliterator.Transliterate(title);
            }

            return Regex.Replace(title, @"[.\-_:'`]", " ")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLower())
                .Where(s => !string.IsNullOrEmpty(s))
                .Aggregate((a, b) => $"{a} {b}");
        }

        private string NormalizeSeasonNumber(string season)
        {
            var normalized = season.TrimStart('0');
            return string.IsNullOrEmpty(normalized) ? "0" : normalized;
        }

        private ActionResult RenderMovies(MediaRequestParameters parameters, List<Movie> movies)
        {
            var template = new MovieTpl(parameters.Title, parameters.OriginalTitle);

            foreach (var movie in movies)
            {
                var streamQuality = BuildStreamQualityTemplate(movie.links);
                template.Append(
                    movie.translation,
                    streamQuality.Firts().link,
                    quality: streamQuality.Firts().quality,
                    streamquality: streamQuality
                );
            }

            return CreateResponse(new MovieTplWrapper(template), parameters.ReturnJson);
        }

        private ActionResult RenderSeries(MediaRequestParameters parameters, Dictionary<string, List<Voice>> series)
        {
            return parameters.Season == -1
                ? RenderSeasonList(parameters, series)
                : RenderEpisodes(parameters, series);
        }

        private ActionResult RenderSeasonList(MediaRequestParameters parameters, Dictionary<string, List<Voice>> series)
        {
            var template = new SeasonTpl(quality: string.Empty);
            var defaultArgs = BuildDefaultQueryArgs(parameters);

            foreach (var season in series)
            {
                template.Append(
                    $"{season.Key} сезон",
                    $"{host}/selfcdn?s={season.Key}{defaultArgs}",
                    season.Key);
            }

            return CreateResponse(new SeasonTplWrapper(template), parameters.ReturnJson);
        }

        private ActionResult RenderEpisodes(MediaRequestParameters parameters, Dictionary<string, List<Voice>> series)
        {
            if (!series.TryGetValue(parameters.Season.ToString(), out var voices))
            {
                ConsoleLogger.Log($"Season {parameters.Season} not found.");
                return Ok();
            }

            var activeTranslationId = string.IsNullOrEmpty(parameters.Translation)
                ? voices.FirstOrDefault()?.id
                : parameters.Translation;

            var voiceTemplate = BuildVoiceTemplate(voices, parameters, activeTranslationId);
            var targetVoice = voices.FirstOrDefault(v => v.id == activeTranslationId);

            if (targetVoice == null)
            {
                ConsoleLogger.Log($"Translation {activeTranslationId} not found for season {parameters.Season}.");
                return Ok();
            }

            var episodeTemplate = BuildEpisodeTemplate(parameters, targetVoice);
            return CreateResponse(
                new VoiceTplWrapper(voiceTemplate), 
                new EpisodeTplWrapper(episodeTemplate),
                parameters.ReturnJson);
        }

        private VoiceTpl BuildVoiceTemplate(List<Voice> voices, MediaRequestParameters parameters, string activeTranslationId)
        {
            var template = new VoiceTpl();
            var defaultArgs = BuildDefaultQueryArgs(parameters);

            foreach (var voice in voices)
            {
                template.Append(
                    voice.name,
                    voice.id == activeTranslationId,
                    $"{host}/selfcdn?s={parameters.Season}&t={voice.id}{defaultArgs}");
            }

            return template;
        }

        private EpisodeTpl BuildEpisodeTemplate(MediaRequestParameters parameters, Voice voice)
        {
            var template = new EpisodeTpl();
            var episodes = voice.episodes.OrderBy(e => e.id);

            foreach (var episode in episodes)
            {
                var streamQuality = BuildStreamQualityTemplate(episode.links);
                template.Append(
                    $"{episode.id} серия",
                    $"{parameters.Title ?? parameters.OriginalTitle} ({episode.id} серия)",
                    parameters.Season.ToString(),
                    episode.id,
                    streamQuality.Firts().link,
                    streamquality: streamQuality);
            }

            return template;
        }

        private StreamQualityTpl BuildStreamQualityTemplate(List<(string Link, string Quality)> links)
        {
            var streamQuality = new StreamQualityTpl();
            foreach (var (link, quality) in links)
            {
                streamQuality.Append(HostStreamProxy(ModInit.Settings, link), quality);
            }

            return streamQuality;
        }

        private string BuildDefaultQueryArgs(MediaRequestParameters parameters)
        {
            return $"&imdb_id={parameters.ImdbId}" +
                $"&kinopoisk_id={parameters.KinopoiskId}" +
                $"&title={System.Web.HttpUtility.UrlEncode(parameters.Title)}" +
                $"&original_title={System.Web.HttpUtility.UrlEncode(parameters.OriginalTitle)}" +
                $"&serial={parameters.Serial}" +
                $"&uid={System.Web.HttpUtility.UrlEncode(parameters.UserId)}";
        }

        private ActionResult CreateResponse(ITemplate template, bool returnJson)
        {
            return returnJson
                ? Content(template.ToJson(), "application/json; charset=utf-8")
                : Content(template.ToHtml(), "text/html; charset=utf-8");
        }

        private ActionResult CreateResponse(ITemplate firstTemplate, ITemplate secondTemplate, bool returnJson)
        {
            return returnJson
                ? Content(secondTemplate.ToJson(), "application/json; charset=utf-8")
                : Content(firstTemplate.ToHtml() + secondTemplate.ToHtml(), "text/html; charset=utf-8");
        }

        private void LogSearchFailure(MediaRequestParameters parameters)
        {
            ConsoleLogger.Log($"Search returned null for title='{parameters.Title}', " +
                $"original_title='{parameters.OriginalTitle}', " +
                $"serial={parameters.Serial}, " +
                $"uid={parameters.UserId}.");
        }
    }
}