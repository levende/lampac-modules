using SelfCdn.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SelfCDN.Tmdb
{
    internal class TmdbFinderApi : ITmdbFinder
    {
        private readonly string _apiKey;
        private readonly HttpClient _client;
        private readonly string _language;

        public TmdbFinderApi(string apiKey, string language)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _language = language ?? "en-US";
            _client = new HttpClient
            {
                BaseAddress = new Uri("https://api.themoviedb.org/3/")
            };
        }

        public async Task<IReadOnlyList<TmdbFindResult>> SearchAsync(MediaMetadata mediaMetadata)
        {
            var queryParams = $"search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(mediaMetadata.Title)}&language={_language}";
            

            var response = await _client.GetAsync(queryParams);
            if (!response.IsSuccessStatusCode)
            {
                return new List<TmdbFindResult>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonSerializer.Deserialize<TmdbSearchResponse>(content);
            var results = searchResponse?.results ?? new List<TmdbSearchResult>();

            return results.Select(r => new TmdbFindResult(
                r.id,
                (r.release_date ?? r.first_air_date)?.Year)).ToList();
        }
    }
}
