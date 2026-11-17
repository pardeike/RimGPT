// Compile the actual AI.cs against local request recording and game-state doubles.
namespace HarmonyLib
{
    public static class Extensions
    {
        public static string Join<T>(this IEnumerable<T> values, Func<T, string> converter = null, string delimiter = ", ") =>
            string.Join(delimiter, values.Select(value => converter == null ? value?.ToString() : converter(value)));
    }
}

namespace OpenAI
{
    public sealed class UserApiConfig
    {
        public bool Active = true;
        public string Provider = "Offline";
        public string ModelId = "primary-plain";
        public bool UseSecondaryModel = true;
        public string SecondaryModelId = "secondary-plain";
        public int ModelSwitchRatio = 3;
        public long CharactersSent, CharactersReceived;
    }
    public sealed class ApiConfig { public string Provider = "Offline"; }
    public sealed class ResponseFormat { public string Type { get; set; } }
    public sealed class ChatMessage { public string Role { get; set; } public string Content { get; set; } }
    public sealed class CreateChatCompletionRequest
    {
        public string Model { get; set; }
        public ResponseFormat ResponseFormat { get; set; }
        public float FrequencyPenalty { get; set; }
        public float PresencePenalty { get; set; }
        public float Temperature { get; set; }
        public List<ChatMessage> Messages { get; set; }
    }
    public sealed class ChatChoice { public ChatMessage Message { get; set; } }
    public sealed class CreateChatCompletionResponse { public List<ChatChoice> Choices { get; set; } }
    public static class OpenAIApi
    {
        public static ApiConfig currentConfig = new();
        public static readonly List<CreateChatCompletionRequest> Requests = new();
        public static Func<int, CreateChatCompletionResponse> Respond;
        public static void SwitchConfig(string provider) => currentConfig = new ApiConfig { Provider = provider };
        public static Task<CreateChatCompletionResponse> CreateChatCompletion(CreateChatCompletionRequest request, Action<string> error)
        {
            Requests.Add(request);
            return Task.FromResult(Respond?.Invoke(Requests.Count) ?? Success());
        }
        public static CreateChatCompletionResponse Success() => new()
        {
            Choices = [new ChatChoice { Message = new ChatMessage { Content = "A fresh response from the offline request recorder." } }]
        };
    }
}

namespace RimWorld { public class Page { } }
namespace RimWorld.Planet { public static class WorldRendererUtility { public static bool WorldRendered => false; } }
namespace Verse
{
    public sealed class WindowStack { public object focusedWindow => null; }
    public static class Find { public static WindowStack WindowStack = new(); }
    public static class Current { public static object Game = new(); }
}
namespace UnityEngine
{
    public static class Mathf { public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max); }
}

namespace RimGPT
{
    public sealed class Persona
    {
        public string name = "Offline persona", personality = "Comment on the game", lastSpokenText = new('x', 128);
        public bool isChronicler = true;
        public int phraseMaxWordCount = 30, historyMaxWordCount = 100;
    }
    public sealed class Phrase { public string text = "An offline event"; }
    public sealed class TestSettings
    {
        public List<OpenAI.UserApiConfig> userApiConfigs = new();
        public List<Persona> personas = new();
    }
    public static class RimGPTMod { public static TestSettings Settings = new(); }
    public static class Tools
    {
        public static bool DEBUG => false;
        public static int ConfigUpdates;
        public static string PlayerName() => null;
        public static void UpdateApiConfigs() => ConfigUpdates++;
        public static void SafeAsync(Func<Task> action) => throw new InvalidOperationException("The harness does not run background or external requests.");
    }
    public static class RecordKeeper
    {
        public static string[] ColonistDataSummary => [];
        public static string ColonySetting => "Unknown as of now...";
        public static string ResearchDataSummary => "";
        public static string ResourceData => "";
        public static string RoomsDataSummary => "";
        public static string EnergySummary => "";
    }
    public static class Personas { public static void Reset() { } }
    public static class Logger
    {
        public static void Error(string message) { }
        public static void Warning(string message) { }
        public static void Message(string message) { }
    }
    public static class LanguageHelper { public static int CalculateLevenshteinDistance(string source, string target) => 128; }
    public static class TextExtensions { public static string Cleanup(this string text) => text; }
}
