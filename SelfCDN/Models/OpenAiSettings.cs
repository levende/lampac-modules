namespace SelfCDN.Models
{
    public class OpenAiSettings
    {
        public string ApiUrl { get; set; }
        public string ApiKey { get; set; }
        public string ModelName { get; set; }

        public int? TimeoutMinutes { get; set; } = 5;
        public int? BatchSize { get; set; } = 25;
        public int? BatchTimeoutSec { get; set; } = 3;
    }
}
