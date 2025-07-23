using System.Text.Json.Serialization;

namespace SelfCdn.Models
{
    internal class MediaMetadata
    {
        [JsonPropertyName("Title")]
        public string Title { get; set; }

        [JsonPropertyName("SeasonNumber")]
        public int? SeasonNumber { get; set; }

        [JsonPropertyName("EpisodeNumber")]
        public int? EpisodeNumber { get; set; }

        [JsonPropertyName("EpisodeName")]
        public string EpisodeName { get; set; }

        [JsonPropertyName("Year")]
        public int? Year { get; set; }
        public string FilePath { get; set; }
        public string Id { get; set; }
    }
}