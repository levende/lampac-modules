using Lampac;

namespace SelfCDN.Models
{
    public class SelfCdnSettings
    {
        public bool? IsLogEnabled { get; set; } = false;
        public int? TimeoutMinutes { get; set; } = 60;
        public int? SkipModificationMinutes { get; set; } = 60;
        public string StoragePath { get; set; } = AppInit.conf.dlna.path;
        public string TmdbApiKey { get; set; } = "4ef0d7355d9ffb5151e987764708ce96";
        public string LlmApiKey { get; set; } = "";
        public string LlmModel { get; set; } = "qwen/qwen3-32b";
        public string TmdbLang { get; set; } = "en-US";
        public string DisplayName { get; set; } = "SelfCDN";
    }
}
