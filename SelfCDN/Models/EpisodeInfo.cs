namespace SelfCDN.Models
{
    public class EpisodeInfo
    {
        public string SeasonNumber { get; }
        public string EpisodeNumber { get; }

        public EpisodeInfo(string seasonNumber, string episodeNumber)
        {
            SeasonNumber = seasonNumber;
            EpisodeNumber = episodeNumber;
        }
    }
}