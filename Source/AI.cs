using HarmonyLib;
using Newtonsoft.Json;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static RimGPT.PhraseManager;

namespace RimGPT
{
	public static class AI
	{
		public static bool debug = true;

#pragma warning disable CS0649
		struct Output
		{
			public string response;
			public string history;
		}
#pragma warning restore CS0649

		private static OpenAIApi OpenAI => new(RimGPTMod.Settings.chatGPTKey);
		private static readonly string responseName = "response";
		private static readonly string historyName = "history";
		private static string history = "Nothing yet";

		public const string defaultPersonality = @"An experienced companion assisting the player. You advice is aways {VOICESTYLE}.";

		private static string SystemPrompt => @$"Your input will be generated from in-game content of the game Rimworld. That includes a summary of the past.
Create responses in two steps:
1) Your response called '{responseName}' must be in {Tools.PersonalityLanguage}
2) A summary of what happened in the game so far called '{historyName}'
Limit that to:
1) {responseName} - never have more than {RimGPTMod.Settings.phraseMaxWordCount} words
2) {historyName} - never have more than {RimGPTMod.Settings.historyMaxWordCount} words
Your output must only consist of json like {{""{responseName}"": ""..."", ""{historyName}"": ""...""}}.
You play the following role: {RimGPTMod.Settings.personality}".ApplyVoiceStyle();

		public static async Task<string> Evaluate(Phrase[] observations)
		{
			var input = new StringBuilder();
			_ = input.AppendLine($"Summary of the past:");
			_ = input.AppendLine(history);
			_ = input.AppendLine("Just happened:");
			for (var i = 0; i < observations.Length; i++)
				_ = input.AppendLine($"- {observations[i].text}");

			if (debug)
				Log.Warning($"INPUT: {input}");

			var observationString = observations.Join(o => $"- {o.text}", "\n");
			var completionResponse = await OpenAI.CreateChatCompletion(new CreateChatCompletionRequest()
			{
				Model = "gpt-3.5-turbo",
				Messages = new List<ChatMessage>()
					 {
						  new ChatMessage()
						  {
								Role = "system",
								Content = SystemPrompt
						  },
						  new ChatMessage()
						  {
								Role = "user",
								Content = input.ToString()
						  }
					 }
			}, error => Log.Error(error));
			RimGPTMod.Settings.charactersSentOpenAI += SystemPrompt.Length + input.Length;

			if (completionResponse.Choices?.Count > 0)
			{
				var response = (completionResponse.Choices[0].Message.Content ?? "").Trim();
				var firstIdx = response.IndexOf("{");
				if (firstIdx >= 0)
				{
					var lastIndex = response.LastIndexOf("}");
					if (lastIndex >= 0)
						response = response.Substring(firstIdx, lastIndex - firstIdx + 1);
				}

				if (debug)
					Log.Warning($"OUTPUT: {response}");
				try
				{
					var output = JsonConvert.DeserializeObject<Output>(response);
					history = output.history;
					return output.response.Cleanup();
				}
				catch (Exception exception)
				{
					Log.Error($"ChatGPT malformed output: {response} [{exception.Message}]");
				}
			}
			else if (debug)
				Log.Warning($"OUTPUT: null");

			return null;
		}

		public static void ResetHistory()
		{
			history = "Nothing yet";
		}

		public static async Task<(string, string)> SimplePrompt(string input)
		{
			string requestError = null;
			var completionResponse = await OpenAI.CreateChatCompletion(new CreateChatCompletionRequest()
			{
				Model = "gpt-3.5-turbo",
				Messages = new List<ChatMessage>()
						  {
								new ChatMessage()
								{
									 Role = "user",
									 Content = input
								}
						  }
			}, e => requestError = e);
			RimGPTMod.Settings.charactersSentOpenAI += input.Length;

			if (completionResponse.Choices?.Count > 0)
				return (completionResponse.Choices[0].Message.Content, null);

			return (null, requestError);
		}

		public static void TestKey(Action<string> callback)
		{
			_ = Task.Run(async () =>
			{
				var prompt = "The player has just configured your OpenAI API key in the mod " +
					 "RimGPT for Rimworld. Greet them with a short response!";
				var output = await SimplePrompt(prompt);
				callback(output.Item1 ?? output.Item2);
			});
		}
	}
}