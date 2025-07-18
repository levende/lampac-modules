using SelfCdn.Registry.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System;

namespace SelfCDN.Tmdb
{
    internal class TmdbSearchResponse
    {
        public List<TmdbSearchResult> results { get; set; }
    }

    internal class TmdbSearchResult
    {
        public long id { get; set; }
        public string title { get; set; }
        public string name { get; set; }
        public string original_title { get; set; }
        public string original_name { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? release_date { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? first_air_date { get; set; }
        public float popularity { get; set; }
    }

    internal class TmdbMovieDetails
    {
        public int id { get; set; }
        public string title { get; set; }
        public string original_title { get; set; }
        public DateTime? release_date { get; set; }
    }

    internal class TmdbSeriesDetails
    {
        public int id { get; set; }
        public string name { get; set; }
        public string original_name { get; set; }
        public DateTime? first_air_date { get; set; }
    }

    internal class TmdbEpisodeDetails
    {
        public int season_number { get; set; }
        public int episode_number { get; set; }
        public string name { get; set; }
    }
}
