using System;
using System.Collections.Generic;
using System.Net.Http;
using SelfCdn.Models;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using SelfCDN.Exceptions;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using Shared.Engine;
using SelfCDN.Models;
using System.Linq;
using System.Web;

namespace SelfCDN.OpenAi
{
    internal class OpenAiMediaMetadataExtractor : IMediaMetadataExtractor, IDisposable
    {
        private static readonly Regex JsonContentRegex = new(
            @"```json\s*(\[\s*\{.*?\}\s*\])[\s\S]*?```",
            RegexOptions.Singleline);

        private readonly OpenAiSettings _settings;
        private readonly HttpClient _httpClient;

        private string _systemPromt;

        private bool _disposed;

        public OpenAiMediaMetadataExtractor(OpenAiSettings settings)
        {
            _settings = settings;
            _httpClient = InitializeHttpClient();
        }

        private HttpClient InitializeHttpClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(_settings.ApiUrl),
                Timeout = TimeSpan.FromMinutes(_settings.TimeoutMinutes.Value),
            };

            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            }

            return client;
        }

        public async Task<IReadOnlyCollection<MediaMetadata>> ExtractAsync(IEnumerable<string> filePaths)
        {
            ArgumentNullException.ThrowIfNull(filePaths, nameof(filePaths));

            try
            {
                var encodedList = filePaths.Select(HttpUtility.UrlEncode);

                var requestBody = CreateRequestBody(encodedList);
                var response = await SendApiRequestAsync(requestBody);
                Logger.Log($"Response: {response}");

                var rawContent = ExtractResponseContent(response);
                var mediaInfo = DeserializeMediaInfo(rawContent);

                foreach (var mediaMetadata in mediaInfo)
                {
                    mediaMetadata.FilePath = HttpUtility.UrlDecode(mediaMetadata.FilePath);
                }

                return mediaInfo;
            }
            catch (Exception ex)
            {
                throw new OpenAiException($"Failed to parse media info: {ex.Message}", ex);
            }
        }

        private object CreateRequestBody(IEnumerable<string> filePaths)
        {
            _systemPromt ??= FileCache.ReadAllText(ModInit.ModulePath + "/Resources/system.promt.txt");

            var inputJson = JsonSerializer.Serialize(
                filePaths,
                new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = false,
                });

            return new
            {
                model = _settings.ModelName,
                messages = new[]
                {
                    new { role = "system", content = _systemPromt},
                    new { role = "user", content = $"```json {inputJson} ```" }
                }
            };
        }

        private async Task<string> SendApiRequestAsync(object requestBody)
        {
            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(string.Empty, content);

            var responseContent = string.Empty;

            try
            {
                responseContent = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new OpenAiException($"Invalid response. Error: {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new OpenAiException($"API error: {response.StatusCode}, Response: {responseContent}");
            }

            return responseContent;
        }

        private static string ExtractResponseContent(string responseContent)
        {
            using JsonDocument document = JsonDocument.Parse(responseContent);
            var choices = document.RootElement.GetProperty("choices");

            if (choices.GetArrayLength() == 0)
            {
                throw new OpenAiException("JSON response does not contain 'choices' array or it is empty.");
            }

            var content = choices[0].GetProperty("message").GetProperty("content").GetString()
                          ?? throw new OpenAiException("Content field is empty or missing.");

            var match = JsonContentRegex.Match(content);
            if (!match.Success)
            {
                throw new OpenAiException("JSON content is not properly wrapped in ```json code fences or no valid JSON found.");
            }

            return match.Groups[1].Value.Trim();
        }

        private static List<MediaMetadata> DeserializeMediaInfo(string jsonContent)
        {
            try
            {
                var mediaInfo = JsonSerializer.Deserialize<List<MediaMetadata>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                }) ?? throw new OpenAiException("Failed to deserialize FileMediaInfo object.");

                return mediaInfo;
            }
            catch (Exception ex)
            {
                throw new OpenAiException(ex.Message);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            };

            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}