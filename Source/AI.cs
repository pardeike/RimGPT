using HarmonyLib;
using Newtonsoft.Json;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace RimGPT
{
	public static class AI
	{
		public static bool debug = true;

#pragma warning disable CS0649
		struct Input
		{
			public string[] happenings;
			public string[] history;
		}

		struct Output
		{
			public string comment;
			public string history;
		}
#pragma warning restore CS0649

		private static OpenAIApi OpenAI => new(RimGPTMod.Settings.chatGPTKey);
		private static readonly string commentName = "comment";
		private static readonly string historyName = "history";
		private static readonly string happeningsName = "happenings";
		private static readonly List<string> history = new();

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

Rule: Your input is in json that matches this model:
```cs
struct Input {{
  public string[] {happeningsName};
  public string[] {historyName};
}}
```

Rule: Your output is in json that matches this model:
```cs
struct Output {{
  public string {commentName};
  public string {historyName};
}}
```

Rule: 'Input.{happeningsName}' are things that happened in the current game

Rule: Items in 'Input.{happeningsName}' are machine generated from typical game output

Rule: 'Output.{commentName}' must not be longer than {RimGPTMod.Settings.phraseMaxWordCount} words

Rule: 'Output.{commentName}' should be {{VOICESTYLE}}

Rule: 'Input.{historyName}' is past information about the game

Rule: Do not refer to 'Input.{historyName}' directly. It happened in the past.

Rule: 'Output.{historyName}' should be a summary of the recent things that happened

Rule: 'Output.{historyName}' must be written in past tense

Rule: 'Output.{historyName}' must not be longer than {RimGPTMod.Settings.historyMaxWordCount} words

Important rule: 'Output.{commentName}' MUST be in {Tools.Language} translated form!
Important rule: you ONLY answer in json as defined in the rules!").ApplyVoiceStyle();

		public static async Task<string> Evaluate(string[] observations)
		{
			string[] historyArray;
			lock (history)
				historyArray = history.ToArray();
			var input = new Input()
			{
				happenings = observations,
				history = historyArray
			};
			var content = JsonConvert.SerializeObject(input);
			if (debug)
				Log.Warning($"INPUT: {content}");

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
								Content = content
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
					lock (history)
					{
						history.Add(output.history);
						var oversize = history.Count - RimGPTMod.Settings.historyMaxItemCount;
						if (oversize > 0)
							history.RemoveRange(0, oversize);
					}
					return output.comment.Cleanup();
				}
				catch (Exception exception)
				{
					Log.Error($"ChatGPT malformed output: {response} [{exception.Message}]");
				}
			}
			return null;
		}

		public static void ResetHistory()
		{
			lock (history)
				history.Clear();
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