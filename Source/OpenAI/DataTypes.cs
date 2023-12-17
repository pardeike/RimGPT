using Newtonsoft.Json;
using System.Collections.Generic;

namespace OpenAI
{
    #region Common Data Types
    public struct Choice
    {
        public string Text { get; set; }
        public int? Index { get; set; }
        public int? Logprobs { get; set; }
        public string FinishReason { get; set; }
    }

    public struct Usage
    {
        public string PromptTokens { get; set; }
        public string CompletionTokens { get; set; }
        public string TotalTokens { get; set; }
    }

    public class OpenAIFile
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public long Bytes { get; set; }
        public long CreatedAt { get; set; }
        public string Filename { get; set; }
        public string Purpose { get; set; }
        public object StatusDetails { get; set; }
        public string Status { get; set; }
    }

    public class OpenAIFileResponse : OpenAIFile, IResponse
    {
        public ApiError Error { get; set; }
    }

    public class ApiError
    {
        public string Message;
        public string Type;
        public object Param;
        public object Code;
    }

    public struct Auth
    {
        [JsonRequired]
        public string ApiKey { get; set; }
        public string Organization { get; set; }
    }
    #endregion

    #region Models API Data Types
    public struct ListModelsResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Object { get; set; }
        public List<OpenAIModel> Data { get; set; }
    }

    public class OpenAIModel
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public string OwnedBy { get; set; }
        public long Created { get; set; }
        public string Root { get; set; }
        public string Parent { get; set; }
        public List<Dictionary<string, object>> Permission { get; set; }
    }

    public class OpenAIModelResponse : OpenAIModel, IResponse
    {
        public ApiError Error { get; set; }
    }
    #endregion

    #region Chat API Data Types
    /// <summary>
    /// Setting to "json_object" enables JSON mode.
    /// This guarantees that the message the model generates is valid JSON.
    /// Note that the message content may be partial if finish_reason="length",
    /// which indicates the generation exceeded max_tokens or the conversation exceeded the max context length.
    /// </summary>
	public sealed class ResponseFormat
    {
        [JsonProperty("type")] public string Type { get; set; }
    }

    public sealed class CreateChatCompletionRequest
    {
        /// <summary>
        /// A list of ChatMessage objects that contain the messages to be sent to the model.
        /// </summary>
        [JsonProperty("messages")] public List<ChatMessage> Messages { get; set; }

        /// <summary>
        /// The name of the model to use, list can be pulled using the API.
        /// <see href="https://platform.openai.com/docs/api-reference/models/list">List Models API</see>
        /// </summary>
        [JsonProperty("model")] public string Model { get; set; }

        /// <summary>
        /// Number between <b>-2.0 and 2.0</b>. Positive values penalize new tokens based on their existing frequency in the text so far, decreasing the model's likelihood to repeat the same line verbatim.
        /// </summary>
        [JsonProperty("frequency_penalty")] public float? FrequencyPenalty { get; set; } = 0;

        /// <summary>
        /// Modify the likelihood of specified tokens appearing in the completion.<br/>
        /// Accepts a JSON object that maps tokens (specified by their token ID in the GPT tokenizer) to an associated bias value from -100 to 100. 
        /// <see href="https://platform.openai.com/docs/api-reference/completions/create#completions-create-logit_bias">API Reference</see>
        /// </summary>
        [JsonProperty("logit_bias")] public Dictionary<string, string> LogitBias { get; set; }

        /// <summary>
        /// The maximum number of tokens to generate in the completion.
        /// Defaults to 16 if not specified.
        /// </summary>
        [JsonProperty("max_tokens")] public int? MaxTokens { get; set; }

        /// <summary>
        /// The number of responses to generate.
        /// </summary>
        [JsonProperty("n")] public int? N { get; set; } = 1;

        /// <summary>
        /// Increases (+) or decreases (-) the model's likelihood to talk about new topics.
        /// Number between <b>-2.0 and 2.0</b>. Positive values penalize new tokens based on whether they appear in the text so far.
        /// </summary>
        [JsonProperty("presence_penalty")] public float? PresencePenalty { get; set; } = 0;

        /// <summary>
        /// An object specifying the format that the model must output.<br/>
        /// Setting to { "type": "json_object" } enables JSON mode, which guarantees the message the model generates is valid JSON.<br/><br/>
        /// <b>Important: when using JSON mode, you must also instruct the model to produce JSON yourself via a system or user message. </b>
        /// </summary>
        [JsonProperty("response_format")] public ResponseFormat ResponseFormat { get; set; }

        /// <summary>
        /// If specified, OpenAI will make a best effort to sample deterministically, such that repeated requests with the same seed and parameters should return the same result.
        /// </summary>
        [JsonProperty("seed")] public int? Seed { get; set; }

        /// <summary>
        /// Up to 4 sequences where the API will stop generating further tokens.
        /// </summary>
        [JsonProperty("stop")] public string Stop { get; set; }

        /// <summary>
        /// A boolean that indicates whether the model should stream the response back to the user.
        /// </summary>
        [JsonProperty("stream")] public bool? Stream { get; set; } = false;

        /// <summary>
        /// A value between <b>0 and 2</b> that controls the randomness of the model.<br/>
        /// Higher values like 0.8 will make the output more random, while lower values like 0.2 will make it more focused and deterministic.<br/>
        /// It's recommended to alter this or <b><see cref="TopP"/></b> but not both.
        /// </summary>
        [JsonProperty("temperature")] public float? Temperature { get; set; } = 1;

        /// <summary>
        /// A value between <b>0 and 1</b> that controls the diversity of the model's output.<br/>
        /// An alternative to sampling with temperature, called nucleus sampling, where the model considers the results of the tokens with top_p probability mass. <br/>
        /// So 0.1 means only the tokens comprising the top 10% probability mass are considered.<br/><br/>
        /// It's recommended to alter this or <b><see cref="Temperature"/></b> but not both.
        /// </summary>
        [JsonProperty("top_p")] public int? TopP { get; set; }

        /// <summary>
        /// The name of the user who is sending the request.
        /// </summary>
        [JsonProperty("user")] public string User { get; set; }

        /// <summary>
        /// Optional, defaults to false. Whether to return log probabilities of the output tokens or not. 
        /// If true, returns the log probabilities of each output token returned in the content of message.
        /// </summary>
        [JsonProperty("logprobs")] public bool? Logprobs { get; set; }

        /// <summary>
        /// Optional. An integer between 0 and 5 specifying the number of most likely tokens to return at each token position, each with an associated log probability. logprobs must be set to true if this parameter is used.
        /// </summary>
        [JsonProperty("top_logprobs")] public int? TopLogprobs { get; set; }

        // Missing tools: https://platform.openai.com/docs/api-reference/chat/create#chat-create-tools
        // Missing tool_choice: https://platform.openai.com/docs/api-reference/chat/create#chat-create-tool_choice
    }

    public struct CreateChatCompletionResponse : IResponse
    {
        public ApiError Error { get; set; }

        /// <summary>
        /// The model used for the chat completion.
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// A unique identifier for the chat completion.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The object type, which is always chat.completion.
        /// </summary>
        public string Object { get; set; }

        /// <summary>
        /// The Unix timestamp (in seconds) of when the chat completion was created.
        /// </summary>
        public long Created { get; set; }

        /// <summary>
        /// A list of chat completion choices. Can be more than one if <see cref="CreateChatCompletionRequest.N"/> is greater than 1.
        /// </summary>
        public List<ChatChoice> Choices { get; set; }

        /// <summary>
        /// Usage statistics for the completion request.
        /// </summary>
        public Usage Usage { get; set; }

        /// <summary>
        /// This fingerprint represents the backend configuration that the model runs with.
        /// Can be used in conjunction with the seed request parameter to understand when backend 
        /// changes have been made that might impact determinism.
        /// </summary>
        public string SystemFingerprint { get; set; }
    }

    public struct ChatChoice
    {
        /// <summary>
        /// A chat completion message generated by the model.
        /// </summary>
        public ChatMessage Message { get; set; }

        /// <summary>
        /// The index of the choice in the list of choices.
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// The reason the model stopped generating tokens. This will be stop if the model hit a natural stop point or a provided stop sequence, length if the maximum number of tokens specified in the request was reached, content_filter if content was omitted due to a flag from our content filters, tool_calls if the model called a tool, or function_call (deprecated) if the model called a function.
        /// </summary>
        
        public string FinishReason { get; set; }
        /// <summary>
        /// The log probabilities of each output token returned in the content of message.
        /// </summary>
        public bool? Logprobs { get; set; }
    }

    public struct ChatMessage
    {
        /// <summary>
        /// The role of the author of this message. Must be one of "system", "user" or "assistant"
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// The contents of the message.
        /// </summary>
        public string Content { get; set; }

        // Missing: tool_calls[] https://platform.openai.com/docs/api-reference/chat/object#chat/object-choices
    }
    #endregion

    #region Audio Transcriptions Data Types

    public struct FileData
    {
        public byte[] Data;
        public string Name;
    }

    public class CreateAudioRequestBase
    {
        public string File { get; set; }
        public FileData FileData { get; set; }
        public string Model { get; set; }
        public string Prompt { get; set; }
        public string ResponseFormat { get; set; } = AudioResponseFormat.Json;
        public float? Temperature { get; set; } = 0;
    }

    public class CreateAudioTranscriptionsRequest : CreateAudioRequestBase
    {
        public string Language { get; set; }
    }

    public class CreateAudioTranslationRequest : CreateAudioRequestBase { }

    public struct CreateAudioResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Text { get; set; }
    }
    #endregion

    #region Completions API Data Types
    public sealed class CreateCompletionRequest
    {
        public string Model { get; set; }
        public string Prompt { get; set; } = "<|endoftext|>";
        public string Suffix { get; set; }
        public int? MaxTokens { get; set; } = 16;
        public float? Temperature { get; set; } = 1;
        public float? TopP { get; set; } = 1;
        public int? N { get; set; } = 1;
        public bool? Stream { get; set; } = false;
        public int? Logpropbs { get; set; }
        public bool? Echo { get; set; } = false;
        public string Stop { get; set; }
        public float? PresencePenalty { get; set; } = 0;
        public float? FrequencyPenalty { get; set; } = 0;
        public int? BestOf { get; set; } = 1;
        public Dictionary<string, string> LogitBias { get; set; }
        public string User { get; set; }
    }

    public struct CreateCompletionResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Id { get; set; }
        public string Object { get; set; }
        public long Created { get; set; }
        public string Model { get; set; }
        public List<Choice> Choices { get; set; }
        public Usage Usage { get; set; }
    }

    #endregion

    #region Edits API Data Types
    public sealed class CreateEditRequest
    {
        public string Model { get; set; }
        public string Input { get; set; } = "";
        public string Instruction { get; set; }
        public float? Temperature { get; set; } = 1;
        public float? TopP { get; set; } = 1;
        public int? N { get; set; } = 1;
    }

    public struct CreateEditResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Object { get; set; }
        public long Created { get; set; }
        public List<Choice> Choices { get; set; }
        public Usage Usage { get; set; }
    }
    #endregion

    #region Images API Data Types
    public class CreateImageRequestBase
    {
        public int? N { get; set; } = 1;
        public string Size { get; set; } = ImageSize.Size1024;
        public string ResponseFormat { get; set; } = ImageResponseFormat.Url;
        public string User { get; set; }
    }

    public sealed class CreateImageRequest : CreateImageRequestBase
    {
        public string Prompt { get; set; }
    }

    public sealed class CreateImageEditRequest : CreateImageRequestBase
    {
        public string Image { get; set; }
        public string Mask { get; set; }
        public string Prompt { get; set; }
    }

    public sealed class CreateImageVariationRequest : CreateImageRequestBase
    {
        public string Image { get; set; }
    }

    public struct CreateImageResponse : IResponse
    {
        public ApiError Error { get; set; }
        public long Created { get; set; }
        public List<ImageData> Data { get; set; }
    }

    public struct ImageData
    {
        public string Url { get; set; }
        public string B64Json { get; set; }
    }
    #endregion

    #region Embeddins API Data Types
    public struct CreateEmbeddingsRequest
    {
        public string Model { get; set; }
        public string Input { get; set; }
        public string User { get; set; }
    }

    public struct CreateEmbeddingsResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Object { get; set; }
        public List<EmbeddingData> Data;
        public string Model { get; set; }
        public Usage Usage { get; set; }
    }

    public struct EmbeddingData
    {
        public string Object { get; set; }
        public List<float> Embedding { get; set; }
        public int Index { get; set; }
    }
    #endregion

    #region Files API Data Types
    public struct ListFilesResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Object { get; set; }
        public List<OpenAIFile> Data { get; set; }
    }

    public struct DeleteResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Id { get; set; }
        public string Object { get; set; }
        public bool Deleted { get; set; }
    }

    public struct CreateFileRequest
    {
        public string File { get; set; }
        public string Purpose { get; set; }
    }
    #endregion

    #region FineTunes API Data Types
    public class CreateFineTuneRequest
    {
        public string TrainingFile { get; set; }
        public string ValidationFile { get; set; }
        public string Model { get; set; }
        public int NEpochs { get; set; } = 4;
        public int? BatchSize { get; set; } = null;
        public float? LearningRateMultiplier { get; set; } = null;
        public float PromptLossWeight { get; set; } = 0.01f;
        public bool ComputeClassificationMetrics { get; set; } = false;
        public int? ClassificationNClasses { get; set; } = null;
        public string ClassificationPositiveClass { get; set; }
        public List<float> ClassificationBetas { get; set; }
        public string Suffix { get; set; }
    }

    public struct ListFineTunesResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Object { get; set; }
        public List<FineTune> Data { get; set; }
    }

    public struct ListFineTuneEventsResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Object { get; set; }
        public List<FineTuneEvent> Data { get; set; }
    }

    public class FineTune
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
        public string Model { get; set; }
        public string FineTunedModel { get; set; }
        public string OrganizationId { get; set; }
        public string Status { get; set; }
        public Dictionary<string, object> Hyperparams { get; set; }
        public List<OpenAIFile> TrainingFiles { get; set; }
        public List<OpenAIFile> ValidationFiles { get; set; }
        public List<OpenAIFile> ResultFiles { get; set; }
        public List<FineTuneEvent> Events { get; set; }
    }

    public class FineTuneResponse : FineTune, IResponse
    {
        public ApiError Error { get; set; }
    }

    public struct FineTuneEvent
    {
        public string Object { get; set; }
        public long CreatedAt { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
    }
    #endregion

    #region Moderations API Data Types
    public class CreateModerationRequest
    {
        public string Input { get; set; }
        public string Model { get; set; } = ModerationModel.Latest;
    }

    public struct CreateModerationResponse : IResponse
    {
        public ApiError Error { get; set; }
        public string Id { get; set; }
        public string Model { get; set; }
        public List<ModerationResult> Results { get; set; }
    }

    public struct ModerationResult
    {
        public bool Flagged { get; set; }
        public Dictionary<string, bool> Categories { get; set; }
        public Dictionary<string, float> CategoryScores { get; set; }
    }
    #endregion

    #region Static String Types
    public static class ContentType
    {
        public const string MultipartFormData = "multipart/form-data";
        public const string ApplicationJson = "application/json";
    }

    public static class ImageSize
    {
        public const string Size256 = "256x256";
        public const string Size512 = "512x512";
        public const string Size1024 = "1024x1024";
    }

    public static class ImageResponseFormat
    {
        public const string Url = "url";
        public const string Base64Json = "b64_json";
    }

    public static class AudioResponseFormat
    {
        public const string Json = "json";
        public const string Text = "text";
        public const string Srt = "srt";
        public const string VerboseJson = "verbose_json";
        public const string Vtt = "vtt";
    }

    public static class ModerationModel
    {
        public const string Stable = "text-moderation-stable";
        public const string Latest = "text-moderation-latest";
    }
    #endregion
}
