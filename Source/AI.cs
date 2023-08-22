using HarmonyLib;
using Newtonsoft.Json;
using OpenAI;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace RimGPT
{
	public class AI
	{
#pragma warning disable CS0649
		public class Input
		{
			public string CurrentWindow { get; set; }
			public string[] PreviousHistoricalKeyEvents { get; set; }
			public string LastSpokenText { get; set; }
			public List<string> CurrentGameState { get; set; }
		}

		struct Output
		{
			public string ResponseText { get; set; }
			public string[] NewHistoricalKeyEvents { get; set; }
		}
#pragma warning restore CS0649

		private OpenAIApi OpenAI => new(RimGPTMod.Settings.chatGPTKey);
		private string[] history = Array.Empty<string>();

		public const string defaultPersonality = "You are a {VOICESTYLE} e-sports commentator. You address everyone directly.";

		public string SystemPrompt(Persona currentPersona)
		{
			var playerName = Tools.PlayerName();
			var player = playerName == null ? "the player" : $"the player named '{playerName}'";
			var otherObservers = RimGPTMod.Settings.personas.Where(p => p != currentPersona).Join(persona => $"'{persona.name}'");
			return new List<string>
			{
				$"System instruction: Begin{(otherObservers.Any() ? " multi-instance" : "")} role-playing simulation.\n",
				$"You are '{currentPersona.name}', an observer.\n",
				$"Act as the following role and personality: {currentPersona.personality}\n",
				otherObservers.Any() ? $"Along with you, other observers named {otherObservers}, are watching {player} play the game Rimworld.\n" : $"You are watching {player} play the game Rimworld.\n",
				$"Important rules to follow strictly:\n",
				$"- Your input comes from the game and will be in JSON format. It has the following keys: 'CurrentWindow', 'PreviousHistoricalKeyEvents', 'LastSpokenText', and 'CurrentGameState', which is a list of recent events.",
				$"- You also get what other observers say in form of 'X said: ...'.",
				$"- Return your output in JSON format with keys 'ResponseText' and 'NewHistoricalKeyEvents'.",
				$"- Your response called 'ResponseText' must be in {Tools.PersonalityLanguage(currentPersona)}.",
				$"- Add what you want to remember as list of historical key facts called 'NewHistoricalKeyEvents'.",
				$"- Limit 'ResponseText' to no more than {currentPersona.phraseMaxWordCount} words.",
				$"- Limit 'NewHistoricalKeyEvents' to no more than {currentPersona.historyMaxWordCount} words."
			}.Join(delimiter: "").ApplyVoiceStyle(currentPersona);
		}

		public async Task<string> Evaluate(Persona persona, IEnumerable<Phrase> observations)
		{
			var gameInput = new Input
			{
				CurrentGameState = observations.Select(o => o.text).ToList(),
				PreviousHistoricalKeyEvents = history,
				LastSpokenText = persona.lastSpokenText
			};

			var windowStack = Find.WindowStack;
			if (Current.Game == null && windowStack != null)
			{
				if (windowStack.focusedWindow is not Page page || page == null)
				{
					if (WorldRendererUtility.WorldRenderedNow)
						gameInput.CurrentWindow = "The player is selecting the start site";
					else
						gameInput.CurrentWindow = "The player is at the start screen";
				}
				else
				{
					var dialogType = page.GetType().Name.Replace("Page_", "");
					gameInput.CurrentWindow = $"The player is at the dialog {dialogType}";
				}
			}

			var input = JsonConvert.SerializeObject(gameInput);

			if (Tools.DEBUG)
				Logger.Warning($"INPUT: {input}");

			var systemPrompt = SystemPrompt(persona);
			var completionResponse = await OpenAI.CreateChatCompletion(new CreateChatCompletionRequest()
			{
				Model = RimGPTMod.Settings.chatGPTModel,
				Messages = new()
				{
					new ChatMessage() { Role = "system", Content = systemPrompt },
					new ChatMessage() { Role = "user", Content = input }
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
				response = response.Replace("ResponseText:", "");

				if (Tools.DEBUG)
					Logger.Warning($"OUTPUT: {response}");
				try
				{
					Output output;
					if (response.Length > 0 && response[0] != '{')
						output = new Output { ResponseText = response, NewHistoricalKeyEvents = new string[0] };
					else
						output = JsonConvert.DeserializeObject<Output>(response);
					history = output.NewHistoricalKeyEvents;
					return output.ResponseText.Cleanup();
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

		public void ReplaceHistory(string[] reason)
		{
			history = reason;
		}

		public async Task<(string, string)> SimplePrompt(string input)
		{
			string requestError = null;
			var completionResponse = await OpenAI.CreateChatCompletion(new CreateChatCompletionRequest()
			{
				Model = RimGPTMod.Settings.chatGPTModel,
				Messages = new()
				{
					new ChatMessage() { Role = "system", Content = "You are a creative poet answering in 12 words or less." },
					new ChatMessage() { Role = "user", Content = input }
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