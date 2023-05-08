using HarmonyLib;
using Newtonsoft.Json;
using OpenAI;
using RimWorld;
using RimWorld.Planet;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Steam;

namespace RimGPT
{
	public class AI
	{
		public const string modelVersion = "gpt-3.5-turbo";

#pragma warning disable CS0649
		struct Output
		{
			public string response;
			public string history;
		}
#pragma warning restore CS0649

		private OpenAIApi OpenAI => new(RimGPTMod.Settings.chatGPTKey);
		private readonly string responseName = "response";
		private readonly string historyName = "history";
		private string history = "Nothing yet";

		public const string defaultPersonality = "You are a {VOICESTYLE} e-sports commentator. You address everyone directly.";

		public string PlayerName
		{
			get
			{
				if (SteamManager.Initialized == false) return "a player";
				return $"the player called '{SteamFriends.GetPersonaName()}'";
			}
		}

		public string Who(Persona persona)
		{
			var n = Personas.personas.Count - 1;
			if (n <= 0)
				return $"You are role playing, watching {PlayerName} play Rimworld. Your name is '{persona.name}'";

			var s = $"You are role playing with {n} others participant{(n == 1 ? "" : "s")}, watching {PlayerName} play Rimworld. Your name is '{persona.name}'";
			if (n == 1)
				s += $" and the other participant is named {Personas.personas.First(p => p != persona).name}";
			else
				s += $" and the others participants are {Personas.personas.Where(p => p != persona).Join(p => p.name)}";

			return s;
		}

		public string SystemPrompt(Persona persona) => @$"{Who(persona)}. Your input will be generated from a game of Rimworld. You also get what the other role playing participants said (in form of 'X said: ...') and a list of past key facts.
Create responses in two steps:
1) Your response called '{responseName}' must be in {Tools.PersonalityLanguage(persona)}
2) A list of key facts of what happened in the game so far called '{historyName}'
Important limits:
1) '{responseName}' - never have more than {persona.phraseMaxWordCount} words
2) '{historyName}' - never have more than {persona.historyMaxWordCount} words
Your output must only consist of json like {{""{responseName}"": ""..."", ""{historyName}"": ""...""}}.
Your role: {persona.personality}".ApplyVoiceStyle(persona);

		public async Task<string> Evaluate(Persona persona, IEnumerable<Phrase> observations)
		{
			var input = new StringBuilder();
			var windowStack = Find.WindowStack;
			if (Current.Game == null && windowStack != null)
			{
				if (windowStack.focusedWindow is not Page page || page == null)
				{
					if (WorldRendererUtility.WorldRenderedNow)
						_ = input.AppendLine("The player is selecting the start site");
					else
						_ = input.AppendLine("The player is at the start screen");
				}
				else
				{
					var dialogType = page.GetType().Name.Replace("Page_", "");
					_ = input.AppendLine($"The player is at the dialog {dialogType}");
				}
			}
			_ = input.AppendLine($"Key facts of the past:");
			_ = input.AppendLine(history);
			if (persona.lastSpokenText != "")
				_ = input.AppendLine($"The last thing you said: {persona.lastSpokenText}");
			_ = input.AppendLine("Just happened:");
			foreach (var observation in observations)
				_ = input.AppendLine($"- {observation.text}");

			if (Tools.DEBUG)
				Logger.Warning($"INPUT: {input}");

			var systemPrompt = SystemPrompt(persona);
			var completionResponse = await OpenAI.CreateChatCompletion(new CreateChatCompletionRequest()
			{
				Model = modelVersion,
				Messages = new List<ChatMessage>()
					 {
						  new ChatMessage()
						  {
								Role = "system",
								Content = systemPrompt
						  },
						  new ChatMessage()
						  {
								Role = "user",
								Content = input.ToString()
						  }
					 }
			}, error => Logger.Error(error));
			RimGPTMod.Settings.charactersSentOpenAI += systemPrompt.Length + input.Length;

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

				if (Tools.DEBUG)
					Logger.Warning($"OUTPUT: {response}");
				try
				{
					var output = JsonConvert.DeserializeObject<Output>(response);
					history = output.history;
					return output.response.Cleanup();
				}
				catch (Exception exception)
				{
					Logger.Error($"ChatGPT malformed output: {response} [{exception.Message}]");
				}
			}
			else if (Tools.DEBUG)
				Logger.Warning($"OUTPUT: null");

			return null;
		}

		public void ReplaceHistory(string reason)
		{
			history = reason;
		}

		public async Task<(string, string)> SimplePrompt(string input)
		{
			string requestError = null;
			var completionResponse = await OpenAI.CreateChatCompletion(new CreateChatCompletionRequest()
			{
				Model = "gpt-3.5-turbo",
				Messages = new List<ChatMessage>()
						  {
								new ChatMessage()
								{
									 Role = "system",
									 Content = "You are a creative poet answering in 12 words or less."
								},
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
			Tools.SafeAsync(async () =>
			{
				var prompt = "The player has just configured your OpenAI API key in the mod " +
					 "RimGPT for Rimworld. Greet them with a short response!";
				var dummyAI = new AI();
				var output = await dummyAI.SimplePrompt(prompt);
				callback(output.Item1 ?? output.Item2);
			});
		}
	}
}