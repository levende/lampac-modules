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

namespace SelfCDN.OpenAi
{
    internal class OpenAiMediaMetadataExtractor : IMediaMetadataExtractor, IDisposable
    {
        private static readonly Regex JsonContentRegex = new(
            @"```json\s*(\[\s*\{.*?\}\s*\])[\s\S]*?```",
            RegexOptions.Singleline);

        private readonly HttpClient _httpClient;

        private readonly string _url;
        private readonly string _model;
        private readonly string _apiKey;

        private string _systemPromt;

        private bool _disposed;

        public OpenAiMediaMetadataExtractor(string url, string model, string apiKey = "")
        {
            _url = url;
            _apiKey = apiKey;
            _model = model;
            _httpClient = InitializeHttpClient();
        }

        private HttpClient InitializeHttpClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(_url)
            };

            if (!string.IsNullOrEmpty(_apiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            return client;
        }

        public async Task<IReadOnlyCollection<MediaMetadata>> ExtractAsync(IEnumerable<string> filePaths)
        {
            ArgumentNullException.ThrowIfNull(filePaths, nameof(filePaths));

            try
            {
                var requestBody = CreateRequestBody(filePaths);
                var response = await SendApiRequestAsync(requestBody);
                Logger.Log($"Response: {response}");

                var rawContent = ExtractResponseContent(response);
                var mediaInfo = DeserializeMediaInfo(rawContent);
                return mediaInfo;
            }
            catch (Exception ex)
            {
                throw new OpenAiException($"Failed to parse media info: {ex.Message}", ex);
            }
        }

        private object CreateRequestBody(IEnumerable<string> filePaths)
        {
            _systemPromt ??= FileCache.ReadAllText(ModInit.Initspace.path + "/Resources/system.promt.txt");

            var inputJson = JsonSerializer.Serialize(
                filePaths,
                new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = false,
                });

            return new
            {
                model = _model,
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

            if (!response.IsSuccessStatusCode)
            {
                throw new OpenAiException($"Groq API error: {response.StatusCode}");
            }

            return await response.Content.ReadAsStringAsync();
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