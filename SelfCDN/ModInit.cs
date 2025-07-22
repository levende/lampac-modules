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
        private static readonly SemaphoreSlim ScanSemaphore = new(1, 1);
        private static Timer? _scanTimer;

        internal static string ModulePath { get; set; }
        internal static OnlinesSettings BalancerSettings { get; set; }
        internal static SelfCdnSettings ModuleSettings { get; set; } = new();
        internal static SelfCdnRegistry SelfCdnRegistry;

        public static void loaded(InitspaceModel initspace)
        {
            try
            {
                Init(initspace.path);
            }
            catch (Exception ex)
            {
                Logger.Log($"[ERROR] ModInit.loaded: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public static void Init(string modulePath)
        {
            ModulePath = modulePath;

            var settingsFilePath = Path.Combine(ModulePath, "settings.json");
            Logger.Log($"Settings file path: {settingsFilePath}");

            if (File.Exists(settingsFilePath))
            {
                string settingsJson = File.ReadAllText(settingsFilePath);
                ModuleSettings = JsonSerializer.Deserialize<SelfCdnSettings>(settingsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new SelfCdnSettings();
                ModuleSettings.ApplyDefaults();
                ModuleSettings.OpenAi?.ApplyDefaults();
            }
            else
            {
                ModuleSettings = new SelfCdnSettings();
                ModuleSettings.ApplyDefaults();

                var jsonOptions = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true,
                };

                using var fileStream = File.Create(settingsFilePath);
                JsonSerializer.Serialize(fileStream, ModuleSettings, jsonOptions);

                Logger.Log("Settings file created with default options.");
            }

            BalancerSettings = new OnlinesSettings("SelfCDN", string.Empty)
            {
                displayname = ModuleSettings.DisplayName,
            };

            var dbFilePath = Path.Combine(ModulePath, "db.json");

            SelfCdnRegistry = new SelfCdnRegistry(
                dbFilePath,
                ModuleSettings.StoragePath,
                ModuleSettings.TmdbApiKey,
                ModuleSettings.OpenAi ?? new OpenAiSettings(),
                ModuleSettings.TmdbLang,
                ModuleSettings.SkipModificationMinutes ?? 60);

            if (string.IsNullOrEmpty(ModuleSettings.OpenAi?.ApiUrl))
            {
                Logger.Log("[WARNING] API key for LLM is missing. LLM functionality will be disabled.");

                _ = Task.Run(async () =>
                {
                    await SelfCdnRegistry.Storage.LoadAsync(dbFilePath);
                    await SelfCdnRegistry.Storage.SaveAsync(dbFilePath);
                });

                return;
            }

            StartScanningTimer(ModuleSettings.TimeoutMinutes.Value);
        }

        public static void StartScanningTimer(int timeoutMinutes)
        {
            StopScanningTimer();

            TimeSpan period = timeoutMinutes == -666
                ? TimeSpan.FromSeconds(5)
                : TimeSpan.FromMinutes(timeoutMinutes > 0 ? timeoutMinutes : 1);

            _scanTimer = new Timer(
                callback: OnTimerElapsed,
                state: null,
                dueTime: TimeSpan.Zero,
                period: period
            );
        }

        public static void StopScanningTimer()
        {
            if (_scanTimer != null)
            {
                _scanTimer.Dispose();
                _scanTimer = null;
            }
        }

        private static void OnTimerElapsed(object? state)
        {
            Task.Run(async () =>
            {
                try
                {
                    await StartScanningAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ERROR] Unhandled exception in timer callback: {ex}");
                }
            });
        }

        private static async Task StartScanningAsync()
        {
            if (!await ScanSemaphore.WaitAsync(0))
            {
                Logger.Log("[INFO] Scan is already running (possibly for hours). Skipping this scheduled tick to avoid overlap.");
                return;
            }

            try
            {
                await SelfCdnRegistry.ScanAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Logger.Log("[INFO] Scan was cancelled.");
            }
            catch (Exception ex)
            {
                Logger.Log($"[ERROR] SelfCDN scan failed: {ex.Message}");
            }
            finally
            {
                ScanSemaphore.Release();
            }
        }
    }
}