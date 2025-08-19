using Shared.Models.Module;
using System;
using Newtonsoft.Json;

namespace DlnaAuth
{
    public class ModInit
    {
        internal static DlnaAuthConfig Config { get; set; }
        
        public static void loaded(InitspaceModel conf)
        {
            var manifestPath = $"{conf.path}/manifest.json";
            Config = JsonConvert.DeserializeObject<DlnaAuthConfig>(System.IO.File.ReadAllText(manifestPath));

            if (Config?.groupsAccess is null)
            {
                Console.WriteLine("DlnaAuth: Invalid manifest.json");
            }
        }
    }
}
