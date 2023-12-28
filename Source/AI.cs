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
		public int modelSwitchCounter = 0;
		public static JsonSerializerSettings settings = new() { NullValueHandling = NullValueHandling.Ignore, MissingMemberHandling = MissingMemberHandling.Ignore };

#pragma warning disable CS0649
		public class Input
		{
			public string CurrentWindow { get; set; }
			public string[] PreviousHistoricalKeyEvents { get; set; }
			public string LastSpokenText { get; set; }
			public List<string> CurrentGameState { get; set; }
			public string[] ColonyRoster { get; set; }
			public string ColonySetting { get; set; }
		}

		struct Output
		{
			public string ResponseText { get; set; }
			public string[] NewHistoricalKeyEvents { get; set; }
		}
#pragma warning restore CS0649

		public OpenAIApi OpenAI => new(RimGPTMod.Settings.chatGPTKey);
		public string[] history = Array.Empty<string>();

		public const string defaultPersonality = "You are a commentator watching the player play the popular game, Rimworld.";

		public string SystemPrompt(Persona currentPersona)
		{
			var playerName = Tools.PlayerName();
			var player = playerName == null ? "the player" : $"the player named '{playerName}'";
			var otherObservers = RimGPTMod.Settings.personas.Where(p => p != currentPersona).Join(persona => $"'{persona.name}'");
			var exampleInput = JsonConvert.SerializeObject(new Input
			{
				CurrentWindow = "<Info about currently open window>",
				CurrentGameState = ["Event1", "Event2", "Event3"],
				PreviousHistoricalKeyEvents = ["OldEvent1", "OldEvent2", "OldEvent3"],
				LastSpokenText = "<Previous Output>",
				ColonyRoster = ["Colonist 1", "Colonist 2", "Colonist 3"],
				ColonySetting = "<A description about the colony and setting>"
			}, settings);
			var exampleOutput = JsonConvert.SerializeObject(new Output
			{
				ResponseText = "<New Output>",
				NewHistoricalKeyEvents = ["OldEventsSummary", "Event 1 and 2", "Event3"]
			}, settings);

			return new List<string>
			{  $"You are {currentPersona.name}\n",
				$"The narrative needs to fit within the context of Rimworld, your responses should reflect the game's events and setting appropriately..\n",
				$"Your input comes from the game, and will be json like this: {exampleInput}\n",
				$"Your output must only be in json like this: {exampleOutput}\n",
				$"Limit ResponseText to no more than {currentPersona.phraseMaxWordCount} words.\n",
				$"When constructing the 'ResponseText', consider vocal clarity and pacing so that it is easily understandable when spoken by Microsoft Azure Speech Services.\n",
				$"Limit NewHistoricalKeyEvents to no more than {currentPersona.historyMaxWordCount} words.\n",
				$"{currentPersona.personality}\n",
				$"Remember: your output is in the format: {{\"ResponseText\":\"Your narrative response goes here, within the word limit.\",\"NewHistoricalKeyEvents\":[\"...\",\"...\"]}}",
			}.Join(delimiter: "");
		}

		public string GetCurrentChatGPTModel()
		{
			// Always use primary if UseSecondaryModel is turned off
			if (!RimGPTMod.Settings.UseSecondaryModel)
				return RimGPTMod.Settings.ChatGPTModelPrimary;
			// Increment the model switch counter for each call to this method.
			modelSwitchCounter++;

			// Determine which model to use based on the ModelSwitchRatio.
			// If the counter value modulated by the ModelSwitchRatio equals 1, then switch to the secondary model.
			// This implies that if the counter is a multiple of the ratio, we will still return the primary model,
			// achieving the desired switching effect only after the specified number of primary model uses.
			if ((modelSwitchCounter % RimGPTMod.Settings.ModelSwitchRatio) == 1)
			{
				return RimGPTMod.Settings.ChatGPTModelSecondary;
			}
			else
			{
				return RimGPTMod.Settings.ChatGPTModelPrimary;
			}
		}

		public async Task<string> Evaluate(Persona persona, IEnumerable<Phrase> observations)
		{

			var gameInput = new Input
			{
				CurrentGameState = observations.Select(o => o.text).ToList(),
				PreviousHistoricalKeyEvents = history,
				LastSpokenText = persona.lastSpokenText,
				ColonyRoster = RecordKeeper.FetchColonistData(),
				ColonySetting = RecordKeeper.FetchColonySetting()
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

			var input = JsonConvert.SerializeObject(gameInput, settings);

			var systemPrompt = SystemPrompt(persona);
			var request = new CreateChatCompletionRequest()
			{
				Model = GetCurrentChatGPTModel(),
				ResponseFormat = GetCurrentChatGPTModel().Contains("1106") ? new ResponseFormat { Type = "json_object" } : null,
				// FrequencyPenalty = 1.0f,
				// PresencePenalty = 1.0f,
				// Temperature = 1.5f,
				Messages =
				[
					new ChatMessage() { Role = "system", Content = systemPrompt },
					new ChatMessage() { Role = "user", Content = input }
				]
			};

			if (Tools.DEBUG)
				Log.Warning($"INPUT: {JsonConvert.SerializeObject(request, settings)}");

			var completionResponse = await OpenAI.CreateChatCompletion(request, error => Logger.Error(error));
			RimGPTMod.Settings.charactersSentOpenAI += systemPrompt.Length + input.Length;

			if (completionResponse.Choices?.Count > 0)
			{
				var response = (completionResponse.Choices[0].Message.Content ?? "");
				RimGPTMod.Settings.charactersSentOpenAI += response.Length;
				response = response.Trim();
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
				Model = GetCurrentChatGPTModel(),
				Messages =
				[
					new ChatMessage() { Role = "system", Content = "You are a creative poet answering in 12 words or less." },
					new ChatMessage() { Role = "user", Content = input }
				]
			}, e => requestError = e);
			RimGPTMod.Settings.charactersSentOpenAI += input.Length;

			if (completionResponse.Choices?.Count > 0)
			{
				var response = (completionResponse.Choices[0].Message.Content ?? "");
				RimGPTMod.Settings.charactersSentOpenAI += response.Length;
				return (response, null);
			}

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