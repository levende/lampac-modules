using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Shared.Model.Templates;
using SelfCDN.Models;
using SelfCdn.Registry.Parser;
using SelfCDN.Templates;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;

namespace SelfCDN.Controllers
{
    public class SelfCDNController : BaseController
    {
        private static readonly string UnknownTranslationId = string.Empty;
        private static readonly string UnknownTranslationName = string.Empty;

        [Route("selfCDN/stream")]
        public ActionResult Stream(string path)
        {
            ConsoleLogger.Log($"Stream request: {path}");
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return Forbid();
                }

                byte[] decodedBytes = Convert.FromBase64String(path);
                string decodedPath = Encoding.UTF8.GetString(decodedBytes);

                if (!System.IO.File.Exists(decodedPath))
                {
                    return Forbid();
                }

                var isFileIndexed = ModInit.SelfCdnRegistry.Storage
                    .Recognized
                    .SelectMany(f => f.Value)
                    .Any(i => i.FilePath.Equals(decodedPath, StringComparison.OrdinalIgnoreCase));

                if (!isFileIndexed)
                {
                    return Forbid();
                }

                return File(System.IO.File.OpenRead(decodedPath), "application/octet-stream", true);
            }
            catch (Exception ex)
            {
                ConsoleLogger.Log("err");

                ConsoleLogger.Log(ex.Message);
                ConsoleLogger.Log(ex.StackTrace);
            }

            return Forbid();
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
            
            MediaSearchResult result = new MediaSearchResult();

            if (parameters.IsMovie)
            {
                result.Movies = FindMovies((int)parameters.Id, parameters.UserId);
            }
            else
            {
                result.Series = FindSeries((int)parameters.Id, parameters.UserId);
            }

            return Task.FromResult(result);
        }

        private List<Movie> FindMovies(int tmdbId, string userId)
        {
            return ModInit.SelfCdnRegistry
                .Storage
                .Recognized
                .Where(r =>
                {
                    var key = r.Key.Split(":");
                    return key[0] == "movie" && key[1] == tmdbId.ToString();
                })
                .SelectMany(r => r.Value)
                .Select(r => new Movie
                {
                    translation = new FileInfo(r.FilePath).Name,
                    links = new List<(string Link, string Quality)> { (GetStreamUrl(r.FilePath, userId), QualityParser.Parse(r.FilePath)) }
                }).ToList();
        }

        private Dictionary<int, List<Voice>> FindSeries(long tmdbId, string userId)
        {
            return ModInit.SelfCdnRegistry
                .Storage
                .Recognized
                .Where(r =>
                {
                    var key = r.Key.Split(":");
                    return key[0] == "tv" && key[1] == tmdbId.ToString();
                })
                .SelectMany(r => r.Value)
                .Where(r => r.SeasonNumber.HasValue && r.EpisodeNumber.HasValue)
                .GroupBy(r => r.SeasonNumber)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    group => group.Key.Value,
                    group => new List<Voice>
                    {
                        new()
                        {
                            id = UnknownTranslationId,
                            name = UnknownTranslationName,
                            episodes = group
                                .GroupBy(e => e.EpisodeNumber)
                                .Select(g => g.First())
                                .OrderBy(e => e.EpisodeNumber)
                                .Select(e => new Serial
                                {
                                    id = e.EpisodeNumber.ToString(),
                                    season = e.SeasonNumber.ToString(),
                                    links = new List<(string Link, string Quality)> { (GetStreamUrl(e.FilePath, userId), QualityParser.Parse(e.FilePath)) }
                                })
                                .ToList(),
                        }
                    });
        }

        private string GetStreamUrl(string filePath, string userId)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(filePath);
            string base64 = Convert.ToBase64String(bytes);

            return $"{host}/selfCDN/stream?path={Uri.EscapeDataString(base64)}&uid={Uri.EscapeDataString(userId)}&v=3";
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

        private ActionResult RenderSeries(MediaRequestParameters parameters, Dictionary<int, List<Voice>> series)
        {
            return parameters.Season == -1
                ? RenderSeasonList(parameters, series)
                : RenderEpisodes(parameters, series);
        }

        private ActionResult RenderSeasonList(MediaRequestParameters parameters, Dictionary<int, List<Voice>> series)
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

        private ActionResult RenderEpisodes(MediaRequestParameters parameters, Dictionary<int, List<Voice>> series)
        {
            ConsoleLogger.Log(() =>
            {
                var json = JsonSerializer.Serialize(
                    series,
                    new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true,
                    });

                return $"[SelfCdnController] series: {json}";
            });

            if (!series.TryGetValue(parameters.Season, out var voices))
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
            foreach (var episode in voice.episodes)
            {
                var streamQuality = BuildStreamQualityTemplate(episode.links);
                template.Append(
                    $"S{episode.season}E{episode.id}",
                    $"{parameters.Title ?? parameters.OriginalTitle} (S{episode.season}E{episode.id})",
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
                streamQuality.Append(HostStreamProxy(ModInit.BalancerSettings, link), quality);
            }

            return streamQuality;
        }

        private string BuildDefaultQueryArgs(MediaRequestParameters parameters)
        {
            return $"&id={parameters.Id}" +
                $"&imdb_id={parameters.ImdbId}" +
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