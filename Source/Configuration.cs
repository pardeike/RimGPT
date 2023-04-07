using System;
using System.IO;
using Newtonsoft.Json;

namespace RimGPT
{
	public static class Configuration
	{
        public struct Settings
        {
            public string CHATGPT_API_KEY;
            public string AZURE_SPEECH_KEY;
            public string AZURE_SPEECH_REGION;
            public int PHRASE_BATCH_SIZE;
            public int PHRASE_DELAY_MIN;
            public int PHRASE_DELAY_MAX;
            public int PHRASE_MAX_WORD_COUNT;
            public int HISTORY_MAX_WORD_COUNT;
            public int HISTORY_MAX_ITEM_COUNT;
        }

        public static string chatGPTKey => String("CHATGPT_API_KEY");
        public static string azureSpeechKey => String("AZURE_SPEECH_KEY");
        public static string azureSpeechRegion = String("AZURE_SPEECH_REGION");
        public static int phraseBatchSize = Int("PHRASE_BATCH_SIZE", 50);
        public static int phraseDelayMin = Int("PHRASE_DELAY_MIN", 5000);
        public static int phraseDelayMax = Int("PHRASE_DELAY_MAX", 10000);
        public static int phraseMaxWordCount = Int("PHRASE_MAX_WORD_COUNT", 60);
        public static int historyMaxWordCount = Int("HISTORY_MAX_WORD_COUNT", 40);
        public static int historyMaxItemCount = Int("HISTORY_MAX_ITEM_COUNT", 40);

        public static string String(string key, string @default = "")
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (value != null && value.Length > 0) return value;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var json = File.ReadAllText(Path.Combine(home, ".api.json"));
            var settings = JsonConvert.DeserializeObject<Settings>(json);
            var field = typeof(Settings).GetField(key);
            return field.GetValue(settings) as string ?? @default;
        }

        public static int Int(string key, int @default = 0) => int.Parse(String(key, @default.ToString()));

        public static bool IsConfigured =>
            chatGPTKey?.Length > 0 && azureSpeechKey?.Length > 0 && azureSpeechRegion?.Length > 0;
    }
}

