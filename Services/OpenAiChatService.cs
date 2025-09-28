using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace AudioTranscriptionApp.Services
{
    public class OpenAiChatService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _apiKey; // Store the key locally

        // Simple internal classes for request/response structure
        private class ChatRequest
        {
            [JsonProperty("model", NullValueHandling = NullValueHandling.Ignore)]
            public string Model { get; set; }

            [JsonProperty("messages")]
            public List<ChatMessage> Messages { get; set; }

            // Add other parameters like temperature if needed later
            // [JsonProperty("temperature")]
            // public float Temperature { get; set; } = 0.7f;
        }

        private class ChatMessage
        {
            [JsonProperty("role")]
            public string Role { get; set; }

            [JsonProperty("content")]
            public string Content { get; set; }
        }

        private class ChatResponse
        {
            [JsonProperty("choices")]
            public List<ChatChoice> Choices { get; set; }
            // Add other fields like 'usage' if needed
        }

        private class ChatChoice
        {
            [JsonProperty("message")]
            public ChatMessage Message { get; set; }
            // Add other fields like 'finish_reason' if needed
        }

        // Models response DTOs
        private class ModelsResponse
        {
            [JsonProperty("data")]
            public List<ModelInfo> Data { get; set; }
        }

        private class ModelInfo
        {
            [JsonProperty("id")]
            public string Id { get; set; }
        }


        public OpenAiChatService(string apiKey)
        {
            Logger.Info("OpenAiChatService initializing.");
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.openai.com/");
            UpdateApiKey(apiKey); // Use the method to set header and local field
        }

        public void UpdateApiKey(string apiKey)
        {
            _apiKey = apiKey; // Store locally
            // Avoid logging the key itself
            Logger.Info("Updating API key for OpenAiChatService.");
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _apiKey);
                Logger.Info("OpenAI Chat API key set.");
            }
            else
            {
                 _httpClient.DefaultRequestHeaders.Authorization = null;
                 Logger.Warning("OpenAI Chat API key cleared.");
            }
        }

        // Configuration for retry logic
        private const int MaxRetries = 3;
        private const int InitialDelayMs = 1000; // 1 second

        public async Task<string> GetResponseAsync(string systemPrompt, string userPrompt, string model)
        {
            // Backward-compatible method: defaults to Cloud behavior
            return await SendChatAsync(systemPrompt, userPrompt, model, useLocal: false, localHost: null, localPath: null);
        } // This closing brace belongs to GetResponseAsync

        public async Task<string> GetCleanupResponseAsync(string systemPrompt, string userPrompt, string model)
        {
            bool useLocal = AudioTranscriptionApp.Properties.Settings.Default.CleanupUseLocal;
            string host = AudioTranscriptionApp.Properties.Settings.Default.CleanupLocalHost;
            string path = AudioTranscriptionApp.Properties.Settings.Default.CleanupLocalPath;
            return await SendChatAsync(systemPrompt, userPrompt, model, useLocal, host, path);
        }

        public async Task<string> GetSummarizeResponseAsync(string systemPrompt, string userPrompt, string model)
        {
            bool useLocal = AudioTranscriptionApp.Properties.Settings.Default.SummarizeUseLocal;
            string host = AudioTranscriptionApp.Properties.Settings.Default.SummarizeLocalHost;
            string path = AudioTranscriptionApp.Properties.Settings.Default.SummarizeLocalPath;
            return await SendChatAsync(systemPrompt, userPrompt, model, useLocal, host, path);
        }

        private async Task<string> SendChatAsync(string systemPrompt, string userPrompt, string model, bool useLocal, string localHost, string localPath)
        {
            Logger.Info($"Attempting chat completion (useLocal={useLocal}), model='{(useLocal ? "<omitted>" : model)}', systemLen={systemPrompt?.Length ?? 0}, userLen={userPrompt?.Length ?? 0}, host='{(useLocal ? localHost : null)}', path='{(useLocal ? localPath : null)}'");
            if (!useLocal)
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    Logger.Error("Chat completion failed: API key is not set.");
                    throw new InvalidOperationException("OpenAI API key is not set. Please configure it in Settings.");
                }
            }

            var requestBody = new ChatRequest
            {
                // For local servers that don't support model swapping, omit the model
                Model = useLocal ? null : model,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "system", Content = systemPrompt },
                    new ChatMessage { Role = "user", Content = userPrompt }
                }
            };
            try { Logger.Info($"Chat request JSON size: {Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(requestBody))} bytes"); } catch { }

            int currentRetry = 0;
            int delayMs = InitialDelayMs;

            while (currentRetry <= MaxRetries)
            {
                try
                {
                    string jsonRequest = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                    HttpResponseMessage response;
                    if (useLocal)
                    {
                        string host = (localHost ?? "http://localhost:1234").TrimEnd('/');
                        string path = localPath ?? "/v1/chat/completions";
                        if (!path.StartsWith("/")) path = "/" + path;
                        string url = host + path;
                        var originalAuth = _httpClient.DefaultRequestHeaders.Authorization;
                        _httpClient.DefaultRequestHeaders.Authorization = null;
                        Logger.Info($"Sending local chat completion request to {url}...");
                        try
                        {
                            response = await _httpClient.PostAsync(url, content);
                        }
                        finally
                        {
                            _httpClient.DefaultRequestHeaders.Authorization = originalAuth;
                        }
                    }
                    else
                    {
                        Logger.Info("Sending chat completion request to OpenAI API...");
                        response = await _httpClient.PostAsync("/v1/chat/completions", content);
                    }
                    Logger.Info($"Received API response status: {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var result = JsonConvert.DeserializeObject<ChatResponse>(jsonResponse);
                        if (result?.Choices != null && result.Choices.Count > 0 && result.Choices[0].Message != null)
                        {
                            string assistantResponse = result.Choices[0].Message.Content;
                            Logger.Info($"Successfully received chat completion. Response length: {assistantResponse?.Length ?? 0}");
                            return assistantResponse?.Trim();
                        }
                        else
                        {
                            Logger.Error($"API returned success status but response format was unexpected: {jsonResponse}");
                            throw new Exception("API returned an unexpected response format.");
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Logger.Error($"API Error: {response.StatusCode} - {errorContent}");
                        if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                        {
                            currentRetry++;
                            if (currentRetry <= MaxRetries)
                            {
                                Logger.Warning($"Retryable API error encountered for chat completion. Retrying in {delayMs}ms... (Attempt {currentRetry}/{MaxRetries})");
                                await Task.Delay(delayMs);
                                delayMs *= 2;
                                continue;
                            }
                            else
                            {
                                Logger.Error($"Max retries ({MaxRetries}) reached for chat completion. Giving up.");
                                throw new Exception($"API Error after {MaxRetries} retries: {response.StatusCode} - {errorContent}");
                            }
                        }
                        else
                        {
                            throw new Exception($"API Error: {response.StatusCode} - {errorContent}");
                        }
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    Logger.Error($"HTTP request error during chat completion (Attempt {currentRetry + 1}): {httpEx.Message}", httpEx);
                    currentRetry++;
                    if (currentRetry <= MaxRetries)
                    {
                        Logger.Warning($"Retrying network error for chat completion in {delayMs}ms... (Attempt {currentRetry}/{MaxRetries})");
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                        continue;
                    }
                    else
                    {
                        Logger.Error($"Max retries ({MaxRetries}) reached after network error for chat completion. Giving up.");
                        throw new Exception($"Network error after {MaxRetries} retries: {httpEx.Message}", httpEx);
                    }
                }
                catch (JsonException jsonEx)
                {
                    Logger.Error($"JSON processing error during chat completion: {jsonEx.Message}", jsonEx);
                    throw new Exception($"Error processing API response: {jsonEx.Message}", jsonEx);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Generic chat completion error (Attempt {currentRetry + 1}): {ex.Message}", ex);
                    throw new Exception($"Chat completion error: {ex.Message}", ex);
                }
            }

            Logger.Error("Exited chat completion retry loop unexpectedly.");
            throw new Exception("Chat completion failed after exhausting retries or due to an unexpected loop exit.");
        }

        // Removed extra closing brace here

        // Lists available models from OpenAI's /v1/models endpoint.
        // Returns a sorted list of model IDs. Optional predicate to filter.
        public async Task<List<string>> ListModelsAsync(Func<string, bool> filter = null)
        {
            Logger.Info("Listing available OpenAI models via /v1/models");
            if (string.IsNullOrEmpty(_apiKey))
            {
                Logger.Error("ListModels failed: API key is not set.");
                throw new InvalidOperationException("OpenAI API key is not set. Please configure it in Settings.");
            }

            int currentRetry = 0;
            int delayMs = InitialDelayMs;
            while (currentRetry <= MaxRetries)
            {
                try
                {
                    var response = await _httpClient.GetAsync("/v1/models");
                    Logger.Info($"Models list response status: {response.StatusCode}");
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var models = JsonConvert.DeserializeObject<ModelsResponse>(json);
                        var ids = (models?.Data ?? new List<ModelInfo>())
                                    .Select(m => m?.Id)
                                    .Where(id => !string.IsNullOrEmpty(id))
                                    .ToList();
                        if (filter != null) ids = ids.Where(filter).ToList();
                        ids.Sort(StringComparer.OrdinalIgnoreCase);
                        Logger.Info($"Retrieved {ids.Count} models from API.");
                        return ids;
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Logger.Error($"Models API error: {response.StatusCode} - {errorContent}");
                        if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                        {
                            currentRetry++;
                            if (currentRetry <= MaxRetries)
                            {
                                Logger.Warning($"Retryable models API error. Retrying in {delayMs}ms... (Attempt {currentRetry}/{MaxRetries})");
                                await Task.Delay(delayMs);
                                delayMs *= 2;
                                continue;
                            }
                        }
                        throw new Exception($"Models API error: {response.StatusCode} - {errorContent}");
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    Logger.Error($"HTTP error listing models (Attempt {currentRetry + 1}): {httpEx.Message}", httpEx);
                    currentRetry++;
                    if (currentRetry <= MaxRetries)
                    {
                        Logger.Warning($"Retrying models list in {delayMs}ms... (Attempt {currentRetry}/{MaxRetries})");
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                        continue;
                    }
                    throw new Exception($"Network error listing models after {MaxRetries} retries: {httpEx.Message}", httpEx);
                }
                catch (JsonException jsonEx)
                {
                    Logger.Error($"JSON error parsing models list: {jsonEx.Message}", jsonEx);
                    throw new Exception($"Error processing models list: {jsonEx.Message}", jsonEx);
                }
            }

            throw new Exception("Unexpected exit from models listing loop.");
        }

        public void Dispose()
        {
            Logger.Info("Disposing OpenAiChatService resources.");
            _httpClient?.Dispose();
        }
    }
}
