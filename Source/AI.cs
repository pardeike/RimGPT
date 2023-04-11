using HarmonyLib;
using Newtonsoft.Json;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimGPT
{
	public static class AI
	{
		public static bool debug = false;

#pragma warning disable CS0649
		struct Output
		{
			public string comment;
			public string history;
		}
#pragma warning restore CS0649

		private static OpenAIApi OpenAI => new(RimGPTMod.Settings.chatGPTKey);
		private static readonly string commentName = "comment";
		private static readonly string historyName = "history";
		private static string history = "Nothing yet";

		public const string defaultPersonality = @"You are an expert player of the game Rimworld. You are always {VOICESTYLE}.
You will repeatedly receive input from an ongoing Rimworld game.

Typical things you say:
- Now that I have seen ... I am pretty sure that ...
- This might have been a mistake/a good idea/a bad idea/a strange thing to do
- If I would play I would ...
- Did you miss ...?
- I think you should plan for ...
- In this situation it is best to ...";

		private static string SystemPrompt => (RimGPTMod.Settings.personality + @$"

Here are more rules you must follow:

Rule: Your output is in json that matches this model:
```cs
struct Output {{
  public string {commentName};
  public string {historyName};
}}
```

Rule: Your input is machine generated from typical game output

Rule: '{commentName}' must not be longer than {RimGPTMod.Settings.phraseMaxWordCount} words

Rule: '{commentName}' should be {{VOICESTYLE}}

Rule: '{historyName}' should be the summary over the past things that happened in the game so far

Rule: '{historyName}' must be written in past tense

Rule: '{historyName}' must not be longer than {RimGPTMod.Settings.historyMaxWordCount} words

Important rule: '{commentName}' MUST be in {Tools.Language} translated form!
Important rule: you ONLY answer in json as defined in the rules!").ApplyVoiceStyle();

		public static async Task<string> Evaluate(string[] observations)
		{
			var input = new StringBuilder();
			_ = input.AppendLine($"What has happened in the past in the game:");
			_ = input.AppendLine(history);
			_ = input.AppendLine("What has happened just now:");
			for (var i = 0; i < observations.Length; i++)
				_ = input.AppendLine($"- {observations[i]}");

			if (debug)
				Log.Warning($"INPUT: {input}");

			var observationString = observations.Join(o => $"- {o}", "\n");
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

			if (completionResponse.Choices?.Count > 0)
			{
				var response = completionResponse.Choices[0].Message.Content;
				if (debug)
					Log.Warning($"OUTPUT: {response}");
				try
				{
					var output = JsonConvert.DeserializeObject<Output>(response);
					history = output.history;
					return output.comment.Cleanup();
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

			if (completionResponse.Choices?.Count > 0)
				return (completionResponse.Choices[0].Message.Content, null);

			return (null, requestError);
		}

		public static void TestKey(Action<string> callback)
		{
			_ = Task.Run(async () =>
			{
				var prompt = "The player in Rimworld has just configured your API key in the mod " +
					 "RimGPT that makes you do commentary on their gameplay. Greet them with a short response!";
				var output = await SimplePrompt(prompt);
				callback(output.Item1 ?? output.Item2);
			});
		}
	}
}