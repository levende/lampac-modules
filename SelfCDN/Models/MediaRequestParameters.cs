namespace SelfCDN.Models
{
    public class MediaRequestParameters
    {
        public long Id { get; set; }
        public string ImdbId { get; set; }
        public long KinopoiskId { get; set; }
        public string Title { get; set; }
        public string OriginalTitle { get; set; }
        public string OriginalLanguage { get; set; }
        public int Year { get; set; }
        public int Serial { get; set; }
        public string Translation { get; set; }
        public int Season { get; set; } = -1;
        public bool ReturnJson { get; set; }
        public string UserId { get; set; }

        public bool IsMovie => Serial == 0;
    }
}
