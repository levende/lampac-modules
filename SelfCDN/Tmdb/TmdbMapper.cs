using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SelfCdn.Models;
using SelfCdn.Registry.Models;
using SelfCDN.Tmdb;

internal class TmdbMapper
{
    private readonly string _apiKey;
    private readonly HttpClient _client;
    private readonly string _language;

    private readonly ITmdbFinder _tmdbFinder;

    public TmdbMapper(string apiKey, string language)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _language = language ?? "en-US";
        _tmdbFinder = new TmdbFinderApi(apiKey, language);
        _client = new HttpClient
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };
    }

    public async Task<FileTmdbInfo> MapToTmdbAsync(MediaMetadata mediaMetadata)
    {
        if (string.IsNullOrWhiteSpace(mediaMetadata.Title))
            throw new ArgumentException("Title is required for TMDB mapping.");

        bool isSeries = mediaMetadata.SeasonNumber.HasValue && mediaMetadata.EpisodeNumber.HasValue;

        var result = isSeries
            ? await MapSeriesToTmdbAsync(mediaMetadata)
            : await MapMovieToTmdbAsync(mediaMetadata);

        if (result == null)
        {
            return new FileTmdbInfo
            {
                FileName = mediaMetadata.FilePath,
                Title = mediaMetadata.Title,
                ReleaseYear = mediaMetadata.Year,
                MediaType = isSeries ? "tv" : "movie",
                SeasonNumber = mediaMetadata.SeasonNumber,
                EpisodeNumber = mediaMetadata.EpisodeNumber,
                EpisodeName = mediaMetadata.EpisodeName
            };
        }

        result.FileName = mediaMetadata.FilePath;
        return result;
    }

    private async Task<FileTmdbInfo> MapMovieToTmdbAsync(MediaMetadata mediaMetadata)
    {
        var searchResults = await _tmdbFinder.SearchAsync(mediaMetadata);
        var bestMatch = FindBestMovieMatch(searchResults, mediaMetadata);

        if (bestMatch == null)
            return null;

        var movieDetails = await GetMovieDetailsAsync(bestMatch.Id);
        return new FileTmdbInfo
        {
            TmdbId = movieDetails.id,
            Title = movieDetails.title,
            OriginalTitle = movieDetails.original_title,
            ReleaseYear = movieDetails.release_date?.Year,
            MediaType = "movie",
            FileName = mediaMetadata.FilePath,
        };
    }

    private async Task<FileTmdbInfo> MapSeriesToTmdbAsync(MediaMetadata mediaMetadata)
    {
        var searchResults = await SearchSeriesAsync(mediaMetadata.Title, mediaMetadata.Year);
        var bestMatch = FindBestSeriesMatch(searchResults, mediaMetadata);

        if (bestMatch == null)
        {
            return null;
        }

        var seriesDetails = await GetSeriesDetailsAsync(bestMatch.id);

        var result = new FileTmdbInfo
        {
            TmdbId = seriesDetails.id,
            Title = seriesDetails.name,
            OriginalTitle = seriesDetails.original_name,
            ReleaseYear = seriesDetails.first_air_date?.Year,
            MediaType = "tv",
            FileName = mediaMetadata.FilePath,
        };

        if (!mediaMetadata.SeasonNumber.HasValue || !mediaMetadata.EpisodeNumber.HasValue) return result;

        var episodeDetails = await GetEpisodeDetailsAsync(
            bestMatch.id,
            mediaMetadata.SeasonNumber.Value,
            mediaMetadata.EpisodeNumber.Value
        );

        if (episodeDetails != null)
        {
            result.SeasonNumber = episodeDetails.season_number;
            result.EpisodeNumber = episodeDetails.episode_number;
            result.EpisodeName = episodeDetails.name;
        }
        else
        {
            result.SeasonNumber = mediaMetadata.SeasonNumber;
            result.EpisodeNumber = mediaMetadata.EpisodeNumber;
            result.EpisodeName = mediaMetadata.EpisodeName;
        }

        return result;
    }

    private async Task<List<TmdbSearchResult>> SearchMoviesAsync(string title, string year)
    {
        // Primary search with year and language
        var queryParams = $"search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(title)}&language={_language}";
        if (!string.IsNullOrEmpty(year) && int.TryParse(year, out _))
        {
            queryParams += $"&year={year}";
        }

        var response = await _client.GetAsync(queryParams);
        if (!response.IsSuccessStatusCode)
        {
            return new List<TmdbSearchResult>();
        }

        var content = await response.Content.ReadAsStringAsync();
        var searchResponse = JsonSerializer.Deserialize<TmdbSearchResponse>(content);
        var results = searchResponse?.results ?? new List<TmdbSearchResult>();

        // Fallback search without year and language if no results
        if (!results.Any())
        {
            queryParams = $"search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(title)}";
            response = await _client.GetAsync(queryParams);
            if (response.IsSuccessStatusCode)
            {
                content = await response.Content.ReadAsStringAsync();
                searchResponse = JsonSerializer.Deserialize<TmdbSearchResponse>(content);
                results = searchResponse?.results ?? new List<TmdbSearchResult>();
            }
        }

        return results;
    }

    private async Task<List<TmdbSearchResult>> SearchSeriesAsync(string title, int? year)
    {
        // Primary search with year and language
        var queryParams = $"search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(title)}&language={_language}";
        if (year.HasValue)
        {
            queryParams += $"&first_air_date_year={year}";
        }

        var response = await _client.GetAsync(queryParams);
        if (!response.IsSuccessStatusCode)
        {
            return new List<TmdbSearchResult>();
        }

        var content = await response.Content.ReadAsStringAsync();
        var searchResponse = JsonSerializer.Deserialize<TmdbSearchResponse>(content);
        var results = searchResponse?.results ?? new List<TmdbSearchResult>();

        // Fallback search without year and language if no results
        if (!results.Any())
        {
            queryParams = $"search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(title)}";
            response = await _client.GetAsync(queryParams);
            if (response.IsSuccessStatusCode)
            {
                content = await response.Content.ReadAsStringAsync();
                searchResponse = JsonSerializer.Deserialize<TmdbSearchResponse>(content);
                results = searchResponse?.results ?? new List<TmdbSearchResult>();
            }
        }

        return results;
    }

    private TmdbFindResult FindBestMovieMatch(IEnumerable<TmdbFindResult> results, MediaMetadata mediaMetadata)
    {
        if (!results.Any())
        {
            return null;
        }

        if (mediaMetadata.Year.HasValue)
        {
            var filteredResults = results
                .Where(r => r.Year.HasValue)
                .Where(r =>  Math.Abs(r.Year.Value - mediaMetadata.Year.Value) <= 1)
                .ToList();

            if (filteredResults.Any())
            {
                return filteredResults.First();
            }
        }

        return null;
    }

    private TmdbSearchResult FindBestSeriesMatch(List<TmdbSearchResult> results, MediaMetadata mediaMetadata)
    {
        if (!results.Any())
            return null;

        if (mediaMetadata.Year.HasValue)
        {
            var filteredResults = results.Where(r =>
                r.first_air_date.HasValue &&
                Math.Abs(r.first_air_date.Value.Year - mediaMetadata.Year.Value) <= 1
            ).ToList();

            if (filteredResults.Any())
                return filteredResults.MaxBy(r => r.popularity);
        }

        return results.MaxBy(r => r.popularity);
    }

    private async Task<TmdbMovieDetails> GetMovieDetailsAsync(long movieId)
    {
        var response = await _client.GetAsync($"movie/{movieId}?api_key={_apiKey}&language={_language}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TmdbMovieDetails>(content);
    }

    private async Task<TmdbSeriesDetails> GetSeriesDetailsAsync(long seriesId)
    {
        var response = await _client.GetAsync($"tv/{seriesId}?api_key={_apiKey}&language={_language}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TmdbSeriesDetails>(content);
    }

    private async Task<TmdbEpisodeDetails> GetEpisodeDetailsAsync(long seriesId, int seasonNumber, int episodeNumber)
    {
        try
        {
            var response = await _client.GetAsync($"tv/{seriesId}/season/{seasonNumber}/episode/{episodeNumber}?api_key={_apiKey}&language={_language}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var episodeDetails = JsonSerializer.Deserialize<TmdbEpisodeDetails>(content);
            return episodeDetails;
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}