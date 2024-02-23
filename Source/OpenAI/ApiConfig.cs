using System;
using System.Collections.Generic;
using System.Linq;
using static OpenAI.ApiConfig;

namespace OpenAI
{
    /// <summary> Enumerates available API providers. </summary>
    public enum ApiProvider
    {
        OpenAI,
        OpenRouter,
        Ollama,
        LocalAI
    }

    /// <summary> Configuration settings for API connections. </summary>
    public class ApiConfig
    {
        public ApiProvider Provider { get; set; }
        public string Key { get; set; }
        public string Organization { get; set; }
        public string BaseUrl { get; set; }
        public int? Port { get; set; }
        public bool IsLocal { get; set; }
        public List<OpenAIModel> Models { get; set; }
        public ApiEndpoints Endpoints { get; set; }

        /// <summary> Initializes API configuration with specified settings. </summary>
        /// <param name="apiProvider"> API provider to configure. </param>
        /// <param name="apiKey"> API key for authentication, if required by the provider. </param>
        /// <param name="organization"> Organization associated with the API key. </param>
        /// <param name="port"> The port number for local API providers, if null uses defaults. </param>
        public ApiConfig(ApiProvider apiProvider, string apiKey = null, string organization = null, int? port = null)
        {
            Provider = apiProvider;
            Key = apiKey;
            Organization = organization;
            BaseUrl = new ApiBaseUrl().GetBaseUrl(apiProvider, port);
            Endpoints = new ApiEndpoints(this);
            IsLocal = apiProvider.IsLocal();
        }

        /// <summary> Base URLs of each API Provider. </summary>
        public class ApiBaseUrl
        {
            private int? Port { set; get; }

            public string GetBaseUrl(ApiConfig apiConfig)
            {
                Port = apiConfig.Port;
                return apiConfig.Provider switch
                {
                    ApiProvider.OpenAI => OpenAI,
                    ApiProvider.OpenRouter => OpenRouter,
                    ApiProvider.Ollama => Ollama,
                    ApiProvider.LocalAI => LocalAI,
                    _ => OpenAI
                };
            }

            /// <summary> Gets base URLs for each API provider. </summary>
            public string GetBaseUrl(ApiProvider apiProvider, int? port)
            {
                Port = port;
                return apiProvider switch
                {
                    ApiProvider.OpenAI => OpenAI,
                    ApiProvider.OpenRouter => OpenRouter,
                    ApiProvider.Ollama => Ollama,
                    ApiProvider.LocalAI => LocalAI,
                    _ => OpenAI
                };
            }

            /// <summary> OpenAI Base URL: <see href="https://api.openai.com/v1"/></summary>
            public string OpenAI => "https://api.openai.com/v1";

            /// <summary> OpenRouter Base URL: <see href="https://openrouter.ai/api/v1"/></summary>
            public string OpenRouter => "https://openrouter.ai/api/v1";

            /// <summary> Ollama Default Base URL: <see href="http://localhost:11434/api"/></summary>
            public string Ollama => Port.HasValue ?
                $"http://localhost:{Port}/api" : "http://localhost:11434/api";

            /// <summary> Ollama Default Base URL: <see href="http://localhost:8080/v1"/></summary>
            public string LocalAI => Port.HasValue ?
                $"http://localhost:{Port}/v1" : "http://localhost:8080/v1";
        }
    }

    /// <summary> Utility methods for API configuration. </summary>
    public static class ApiTools
    {
        /// <summary> Gets an array of Api Providers. </summary>
        public static ApiProvider[] GetApiProviders()
        {
            return Enum.GetValues(typeof(ApiProvider)).Cast<ApiProvider>().ToArray();
        }

        /// <summary> Returns string names of all API providers. </summary>
        public static string[] GetApiProviderNames()
        {
            return Enum.GetNames(typeof(ApiProvider));
        }

        /// <summary> Returns a list of ApiConfigs using the Provider enum </summary>
        public static List<ApiConfig> GetApiConfigs()
        {
            List<ApiConfig> apiConfigs = [];
            foreach (var provider in GetApiProviders())
            {
                apiConfigs.Add(new(provider));
            }
            return apiConfigs;
        }
    }

    /// <summary> Extensions for ApiConfig and Provider </summary>
    public static class ApiExtensions
    {
        /// <summary> Updates an ApiConfig with the additional configuration info. </summary>
        /// <param name="apiKey"> Only necessary for online APIs. </param>
        /// <param name="organization">The organization associated with the API key, never necessary.</param>
        /// <param name="port"> Only necessary for Local APIs, will use default if null. </param>
        public static void Update(this ApiConfig apiConfig, string apiKey = null, string organization = null, int? port = null)
        {
            var portChanged = apiConfig.Port != port;
            apiConfig.Key = apiKey;
            apiConfig.Organization = organization;
            apiConfig.Port = port;

            if (portChanged || string.IsNullOrEmpty(apiConfig.BaseUrl))
            {
                apiConfig.BaseUrl = new ApiBaseUrl().GetBaseUrl(apiConfig.Provider, port);
                apiConfig.Endpoints = portChanged ? new ApiEndpoints(apiConfig) : apiConfig.Endpoints;
            }
        }

        /// <summary> Updates a single ApiConfig in list. </summary>
        /// <param name="apiConfigs"></param>
        /// <param name="apiProvider"> The Api Provider to update. </param>
        /// <param name="apiKey"> Only necessary for online APIs. </param>
        /// <param name="organization">The organization associated with the API key, never necessary.</param>
        /// <param name="port"> Only necessary for Local APIs, will use default if null. </param>
        public static void UpdateConfig(this List<ApiConfig> apiConfigs, ApiProvider apiProvider, string apiKey = null, string organization = null, int? port = null)
        {
            apiConfigs.Find(c => c.Provider == apiProvider).Update(apiKey, organization, port);
        }

        /// <summary> Gets an ApiConfig from a list by using the Provider enum. </summary>
        public static ApiConfig GetConfig(this List<ApiConfig> apiConfigs, ApiProvider apiProvider)
        {
            return apiConfigs.Find(c => c.Provider == apiProvider);
        }

        /// <summary> Gets an ApiConfig from a list by using the Provider as a string. </summary>
        public static ApiConfig GetConfig(this List<ApiConfig> apiConfigs, string apiProviderString)
        {
            if (Enum.TryParse(apiProviderString, out ApiProvider apiProvider))
            {
                return apiConfigs.Find(c => c.Provider == apiProvider);
            }
            else
            {
                return null;
            }
        }

        /// <summary> Determines if an API key is needed for the specified provider. </summary>
        public static bool NeedsApiKey(this ApiProvider apiProvider)
        {
            return apiProvider switch
            {
                ApiProvider.Ollama => false,
                ApiProvider.LocalAI => false,
                _ => true
            };
        }

        /// <summary> Determines if a port is used for the specified provider. </summary>
        public static bool UsesPort(this ApiProvider apiProvider)
        {
            return apiProvider switch
            {
                ApiProvider.Ollama => true,
                ApiProvider.LocalAI => true,
                _ => false
            };
        }

        /// <summary> Determines if the provider is a local API. </summary>
        public static bool IsLocal(this ApiProvider apiProvider)
        {
            return apiProvider switch
            {
                ApiProvider.Ollama => true,
                ApiProvider.LocalAI => true,
                _ => false
            };
        }
    }

    /// <summary> Manages API endpoints for different providers. </summary>
    public class ApiEndpoints(ApiConfig apiConfig)
    {
        private string Combine(string endpoint)
        {
            var baseUrl = apiConfig.BaseUrl;
            return $"{baseUrl}{endpoint}";
        }

        /// <summary> OpenAI: "/models"<br/> Ollama: "/tags" </summary>
        public string ListModels => apiConfig.Provider switch
        {
            ApiProvider.Ollama => Combine("/tags"),
            _ => Combine("/models")
        };

        /// <summary> OpenAI: "/models/{id}" </summary>
        /// <param name="id">Model ID</param>
        public string RetrieveModel(string id) => Combine($"/models/{id}");

        /// <summary> OpenAI: "/completions" </summary>
        public string CreateCompletion => Combine("/completions");

        /// <summary> OpenAI: "/chat/completions"<br/> Ollama: "/chat" </summary>
        public string CreateChatCompletion => apiConfig.Provider switch
        {
            ApiProvider.Ollama => Combine("/chat"),
            _ => Combine("/chat/completions")
        };

        /// <summary> OpenAI: "/edits" </summary>
        public string CreateEdit => Combine("/edits");

        /// <summary> OpenAI: "/images/generations" </summary>
        public string CreateImage => Combine("/images/generations");

        /// <summary> OpenAI: "/images/edits" </summary>
        public string CreateImageEdit => Combine("/images/edits");

        /// <summary> OpenAI: "/images/variations" </summary>
        public string CreateImageVariation => Combine("/images/variations");

        /// <summary> OpenAI: "/embeddings" </summary>
        public string CreateEmbeddings => Combine("/embeddings");

        /// <summary> OpenAI: "/audio/transcriptions" </summary>
        public string CreateAudioTranscription => Combine("/audio/transcriptions");

        /// <summary> OpenAI: "/audio/translations" </summary>
        public string CreateAudioTranslation => Combine("/audio/translations");

        /// <summary> OpenAI: "/files" </summary>
        public string ListFiles => Combine("/files");

        /// <summary> OpenAI: "/files" </summary>
        public string CreateFile => Combine("/files");

        /// <summary> OpenAI: "/files/{id}" </summary>
        /// <param name="id">File ID</param>
        public string DeleteFile(string id) => Combine($"/files/{id}");

        /// <summary> RetrieveFile Endpoint: "/files/{id}" </summary>
        /// <param name="id">File ID</param>
        public string RetrieveFile(string id) => Combine($"/files/{id}");

        /// <summary> OpenAI: "/files/{id}/content" </summary>
        /// <param name="id">File ID</param>
        public string DownloadFile(string id) => Combine($"/files/{id}/content");

        /// <summary> OpenAI: "/fine-tunes" </summary>
        public string CreateFineTune => Combine("/fine-tunes");

        /// <summary> OpenAI: "/fine-tunes" </summary>
        public string ListFineTunes => Combine("/fine-tunes");

        /// <summary> OpenAI: "/fine-tunes/{id}" </summary>
        /// <param name="id">Fine-tune job ID</param>
        public string RetrieveFineTune(string id) => Combine($"/fine-tunes/{id}");

        /// <summary> OpenAI: "/fine-tunes/{id}/cancel" </summary>
        /// <param name="id">Fine-tune job ID</param>
        public string CancelFineTune(string id) => Combine($"/fine-tunes/{id}/cancel");

        /// <summary> OpenAI: "/fine-tunes/{id}/events?stream={stream}" </summary>
        /// <param name="id">Fine-tune job ID</param>
        /// <param name="stream">Stream flag</param>
        public string ListFineTuneEvents(string id, bool stream) => Combine($"/fine-tunes/{id}/events?stream={stream}");

        /// <summary> OpenAI: "/models/{model}" </summary>
        /// <param name="model">Model ID</param>
        public string DeleteFineTunedModel(string model) => Combine($"/models/{model}");

        /// <summary> OpenAI: "/moderations" </summary>
        public string CreateModeration => Combine("/moderations");
    }
}