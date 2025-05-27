using Lampac;
using Lampac.Models.LITE;
using Shared.Models.Module;

namespace SelfCDN
{
    public class ModInit
    {
        public static bool IsLogEnabled { get; set; } = false;
        public static OnlinesSettings Settings;
        public static AppInit AppInit { get; set; } = AppInit.conf;

        public static string StoragePath { get; set; } = AppInit.dlna.path;
        public static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".avi" };

        public static void loaded(InitspaceModel initspace)
        {
            AppInit.online.with_search.Add("selfcdn");

            Settings = new OnlinesSettings("SelfCDN", string.Empty)
            {
                displayname = "SelfCDN",
            };
        }
    }
}