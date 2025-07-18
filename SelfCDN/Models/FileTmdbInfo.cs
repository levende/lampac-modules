namespace SelfCdn.Registry.Models
{
    internal class FileTmdbInfo
    {
        public long TmdbId { get; set; }
        public string Title { get; set; }
        public string OriginalTitle { get; set; }
        public int? ReleaseYear { get; set; }
        public string MediaType { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public string EpisodeName { get; set; }
        public string FileName { get; set; }
    }
}
