using Lampac.Models.LITE;
using Shared.Models.Module;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SelfCDN.Models;
using System.Text.Json;
using System.Text.Encodings.Web;
using SelfCDN.Registry;

namespace SelfCDN
{
    public class ModInit
    {
        internal static InitspaceModel Initspace { get; set; }

        internal static OnlinesSettings BalancerSettings { get; set; }
        internal static SelfCdnSettings ModuleSettings { get; set; } = new();

        internal static SelfCdnRegistry SelfCdnRegistry;

        public static void loaded(InitspaceModel initspace)
        {
            try
            {
                Init(initspace);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, ex.StackTrace);
                throw;
            }
        }

        private static void Init(InitspaceModel initspace)
        {
            Initspace = initspace;

            var settingsFilePath = $"{initspace.path}/settings.json";

            Logger.Log($"Settings file path: {settingsFilePath}");

            if (File.Exists(settingsFilePath))
            {
                string settingsJson = File.ReadAllText(settingsFilePath);

                ModuleSettings = JsonSerializer.Deserialize<SelfCdnSettings>(settingsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                ModuleSettings = new SelfCdnSettings();

                var jsonOptions = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true,
                };

                using var fileStream = File.Create(settingsFilePath);
                JsonSerializer.Serialize(fileStream, ModuleSettings, jsonOptions);

                Logger.Log($"Settings file is created with default options");
            }

            BalancerSettings = new OnlinesSettings("SelfCDN", string.Empty)
            {
                displayname = ModuleSettings.DisplayName,
            };

            var dbFilePath = $"{initspace.path}/db.json";

            SelfCdnRegistry = new SelfCdnRegistry(
                dbFilePath,
                ModuleSettings.StoragePath,
                ModuleSettings.TmdbApiKey,
                ModuleSettings.OpenAi ?? new OpenAiSettings(),
                ModuleSettings.TmdbLang,
                ModuleSettings.SkipModificationMinutes ?? 60);

            var timeoutMinutes = ModuleSettings.TimeoutMinutes ?? 60;

            if (string.IsNullOrEmpty(ModuleSettings?.OpenAi?.ApiUrl))
            {
                Logger.Log("[WARNING] API key for LLM is missing. LLM functionality will be disabled.");

                Task.Run(() => SelfCdnRegistry.Storage.LoadAsync(dbFilePath))
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                Task.Run(() => SelfCdnRegistry.Storage.SaveAsync(dbFilePath))
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                return;
            }

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                while (true)
                {
                    // initial
                    if (SelfCdnRegistry.Storage.Recognized.Count == 0
                        && SelfCdnRegistry.Storage.Unrecognized.Count == 0
                        && SelfCdnRegistry.Storage.Ignored.Count == 0)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(0))
                            .ConfigureAwait(false);
                    }
                    else if (timeoutMinutes == -666)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5))
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromMinutes(timeoutMinutes > 0 ? timeoutMinutes : 1))
                            .ConfigureAwait(false);
                    }

                    try
                    {
                        await SelfCdnRegistry.ScanAsync().ConfigureAwait(false);

                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[ERROR]: SelfCDN. {ex.Message}");
                    }
                }
            });
        }
    }
}