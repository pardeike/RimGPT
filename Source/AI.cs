using HarmonyLib;
using Newtonsoft.Json;
using OpenAI;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
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
			public List<string> PreviousHistoricalKeyEvents { get; set; }
			public string LastSpokenText { get; set; }
			public List<string> ActivityFeed { get; set; }
			public string[] ColonyRoster { get; set; }
			public string ColonySetting { get; set; }
			public string ResearchSummary { get; set; }
			public string ResourceData { get; set; }
			public string EnergyStatus { get; set; }
			public string EnergySummary { get; set; }
			public string RoomsSummary { get; set; }
		}

		private float FrequencyPenalty { get; set; } = 0.5f;
		private int maxRetries = 3;
		struct Output
		{
			public string ResponseText { get; set; }
			public string[] NewHistoricalKeyEvents { get; set; }
		}
#pragma warning restore CS0649

		// OpenAIApi is now a static object, the ApiConfig details are added by ReloadGPTModels.
		//public OpenAIApi OpenAI => new(RimGPTMod.Settings.chatGPTKey);
		private List<string> history = new List<string>();

		public const string defaultPersonality = "You are a commentator watching the player play the popular game, Rimworld.";

		public string SystemPrompt(Persona currentPersona)
		{
			var playerName = Tools.PlayerName();
			var player = playerName == null ? "the player" : $"the player named '{playerName}'";
			var otherObservers = RimGPTMod.Settings.personas.Where(p => p != currentPersona).Join(persona => $"'{persona.name}'");
			var exampleInput = JsonConvert.SerializeObject(new Input
			{
				CurrentWindow = "<Info about currently open window>",
				ActivityFeed = ["Event1", "Event2", "Event3"],
				PreviousHistoricalKeyEvents = ["OldEvent1", "OldEvent2", "OldEvent3"],
				LastSpokenText = "<Previous ResponseText, which is the last thing YOU said.>",
				ColonyRoster = ["Colonist 1", "Colonist 2", "Colonist 3"],
				ColonySetting = "<A description about the colony and setting>",
				ResourceData = "<A periodically updated summary of some resources>",
				RoomsSummary = "<A periodically updated summary, which may never be updated if a setting is disabled by the player, of notable rooms in the colony>",
				ResearchSummary = "<A periodically updated summary, which may never be updated if a setting is disabled by the player, what's already been researched, what is currently researched, and what is available for research>",
				EnergySummary = "<A periodically updated summary, which may never be updated if a setting is disabled by the player,A possible report of the colony's power generation and consumption needs>"

			}, settings);
			var exampleOutput = JsonConvert.SerializeObject(new Output
			{
				ResponseText = "<New Output>",
				NewHistoricalKeyEvents = ["OldEventsSummary", "Event 1 and 2", "Event3"]
			}, settings);


			return new List<string>
				{
						$"You are {currentPersona.name}.\n",
						// Adds weight to using its the personality with its responses: as a chronicler, focusing on balanced storytelling, or as an interactor, focusing on personality-driven improvisation.						
						currentPersona.isChronicler ? "Unless otherwise specified, balance major events and subtle details, and express them in your unique style."
																				: "Unless otherwise specified, interact reflecting your unique personality, embracing an improvisational approach based on your background, the current situation, and others' actions",
						$"Unless otherwise specified, ", otherObservers.Any() ? $"your fellow observers are {otherObservers}. " : "",
						$"Unless otherwise specified, ",(otherObservers.Any() ? $"you are all watching " : "You are watching") + $"'{player}' play Rimworld.\n",
						$"Your role/personality: {currentPersona.personality}\n",
						$"Your input comes from the current game and will be json like this: {exampleInput}\n",
						$"Your output must only be in json like this: {exampleOutput}\n",
						$"Limit ResponseText to no more than {currentPersona.phraseMaxWordCount} words.\n",
						$"Limit NewHistoricalKeyEvents to no more than {currentPersona.historyMaxWordCount} words.\n",

						// Encourages the AI to consider how its responses would sound when spoken, ensuring clarity and accessibility.
						$"When constructing the 'ResponseText', consider vocal clarity and pacing so that it is easily understandable when spoken by Microsoft Azure Speech Services.\n",
						// Prioritizes sources of information.
						$"Update prioritization: 1. ActivityFeed, 2. Additional Fields (as context).\n",
						// Further reinforces the AI's specific personality by resynthesizing different pieces of information and storing it in its own history
						$"Combine PreviousHistoricalKeyEvents, and each event from the 'ActivityFeed' and synthesize it into a new, concise form for 'NewHistoricalKeyEvents', make sure that the new synthesis matches your persona.\n",
						// Guides the AI in understanding the sequence of events, emphasizing the need for coherent and logical responses or interactions.
						"Items sequence in 'LastSpokenText', 'PreviousHistoricalKeyEvents', and 'ActivityFeed' reflects the event timeline; use it to form coherent responses or interactions.\n",
						$"Remember: your output MUST be valid JSON and 'NewHistoricalKeyEvents' MUST ONLY contain simple text entries, each encapsulated in quotes as string literals.\n",
						$"For example, {exampleOutput}. No nested objects, arrays, or non-string data types are allowed within 'NewHistoricalKeyEvents'.\n",
				}.Join(delimiter: "");
		}

		private string GetCurrentChatGPTModel()
		{
			if (!RimGPTMod.Settings.UseSecondaryModel) return RimGPTMod.Settings.ChatGPTModelPrimary;

			modelSwitchCounter++;

			if (modelSwitchCounter == RimGPTMod.Settings.ModelSwitchRatio)
			{
				modelSwitchCounter = 0;

                OpenAIApi.SwitchConfig(RimGPTMod.Settings.ApiProviderSecondary);
				Logger.Warning("Switching to secondary model"); // TEMP
				return RimGPTMod.Settings.ChatGPTModelSecondary;
			}
			else
			{
                OpenAIApi.SwitchConfig(RimGPTMod.Settings.ApiProviderPrimary);
                Logger.Warning("Switching to primary model"); // TEMP
                return RimGPTMod.Settings.ChatGPTModelPrimary;
			}
		}
		private float CalculateFrequencyPenaltyBasedOnLevenshteinDistance(string source, string target)
		{
			// Kept running into a situation where the source was null, not sure if that's due to a provider or what.
			if (source == null || target == null)
			{
				Logger.Error($"Calculate FP Error: Null source or target. Source: {source}, Target: {target}");
				return default;
			}
			int levenshteinDistance = LanguageHelper.CalculateLevenshteinDistance(source, target);

			// You can adjust these constants based on the desired sensitivity.
			const float maxPenalty = 2.0f; // Maximum penalty when there is little to no change.
			const float minPenalty = 0f; // Minimum penalty when changes are significant.
			const int threshold = 30;      // Distance threshold for maximum penalty.

			// Apply maximum penalty when distance is below or equal to threshold.
			if (levenshteinDistance <= threshold)
				return maxPenalty;

			// Apply scaled penalty for distances greater than threshold.
			float penaltyScaleFactor = (float)(levenshteinDistance - threshold) / (Math.Max(source.Length, target.Length) - threshold);
			float frequencyPenalty = maxPenalty * (1 - penaltyScaleFactor);

			return Mathf.Clamp(frequencyPenalty, minPenalty, maxPenalty);
		}


		public async Task<string> Evaluate(Persona persona, IEnumerable<Phrase> observations, int retry = 0, string retryReason = "")
		{

			var gameInput = new Input
			{
				ActivityFeed = observations.Select(o => o.text).ToList(),
				LastSpokenText = persona.lastSpokenText,
				ColonyRoster = RecordKeeper.ColonistDataSummary,
				ColonySetting = RecordKeeper.ColonySetting,
				ResearchSummary = RecordKeeper.ResearchDataSummary,
				ResourceData = RecordKeeper.ResourceData,
				RoomsSummary = RecordKeeper.RoomsDataSummary,
				EnergySummary = RecordKeeper.EnergySummary
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
				// Due to async nature of the game, a reset of history and recordkeeper
				// may have slipped through the cracks by the time this function is called.
				// this is to ensure that if all else fails, we don't include any colony data and we clear history (as reset intended)
				if (gameInput.ColonySetting != "Unknown as of now..." && gameInput.CurrentWindow == "The player is at the start screen")
				{

					// I'm not sure why, but Personas are not being reset propery, they tend to have activityfeed of old stuff
					// and recordKeeper contains colony data still.  I"m guessing the reset unloads a bunch of stuff before
					// the actual reset could finish (or something...?) 
					// this ensures the reset happens
					Personas.Reset();

					// cheap imperfect heuristic to not include activities from the previous game.
					// the start screen is not that valueable for context anyway.  its the start screen.
					if (gameInput.ActivityFeed.Count > 0) gameInput.ActivityFeed = ["The player restarted the game"];
					gameInput.ColonyRoster = [];
					gameInput.ColonySetting = "The player restarted the game";
					gameInput.ResearchSummary = "";
					gameInput.ResourceData = "";
					gameInput.RoomsSummary = "";
					gameInput.EnergySummary = "";
					gameInput.PreviousHistoricalKeyEvents = [];
					ReplaceHistory("The Player restarted the game");
				}

			}

			var systemPrompt = SystemPrompt(persona);
			if (FrequencyPenalty > 1)
			{
				systemPrompt += "\nNOTE: You're being too repetitive, you need to review the data you have and come up with something new.";
				systemPrompt += $"\nAVOID talking about anything related to this: {persona.lastSpokenText}";
				history.AddItem("I've been too repetitive lately, I need to examine the data and stray lastSpokenText");			
			}
			if (history.Count() > 5)
			{
				var newhistory = (await CondenseHistory(persona)).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
				ReplaceHistory(newhistory);
			}

			gameInput.PreviousHistoricalKeyEvents = history;
			var input = JsonConvert.SerializeObject(gameInput, settings);

			Logger.Message($"{(retry != 0 ? $"(retry:{retry} {retryReason})" : "")} prompt (FP:{FrequencyPenalty}) ({gameInput.ActivityFeed.Count()} activities) (persona:{persona.name}): {input}");

			var request = new CreateChatCompletionRequest()
			{
				Model = GetCurrentChatGPTModel(),
				ResponseFormat = GetCurrentChatGPTModel().Contains("1106") ? new ResponseFormat { Type = "json_object" } : null,
				FrequencyPenalty = FrequencyPenalty,
				PresencePenalty = FrequencyPenalty,
				Temperature = 0.5f,
				Messages =
				[
					new ChatMessage() { Role = "system", Content = systemPrompt },
					new ChatMessage() { Role = "user", Content = input }
				]
			};

			if (Tools.DEBUG)
				Log.Warning($"INPUT: {JsonConvert.SerializeObject(request, settings)}");

			var completionResponse = await OpenAIApi.CreateChatCompletion(request, error => Logger.Error(error));
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

				Output output;
				if (string.IsNullOrEmpty(response))
					throw new InvalidOperationException("Response is empty or null.");
				try
				{
					if (response.Length > 0 && response[0] != '{')
						output = new Output { ResponseText = response, NewHistoricalKeyEvents = [] };
					else
						output = JsonConvert.DeserializeObject<Output>(response);
				}
				catch (JsonException jsonEx)
				{
					if (retry < maxRetries)
					{
						Logger.Error($"(retrying) ChatGPT malformed output: {jsonEx.Message}. Response was: {response}");
						return await Evaluate(persona, observations, ++retry, "malformed output");
					}
					else
					{
						Logger.Error($"(aborted) ChatGPT malformed output: {jsonEx.Message}. Response was: {response}");
						return null;
					}
				}
				try
				{
					if (gameInput.CurrentWindow != "The player is at the start screen")
					{				
						var newhistory = output.NewHistoricalKeyEvents.ToList() ?? [];
						ReplaceHistory(newhistory);						
					}
					var responseText = output.ResponseText?.Cleanup() ?? string.Empty;

					if (string.IsNullOrEmpty(responseText))
						throw new InvalidOperationException("Response text is null or empty after cleanup.");

					// Ideally we would want the last two things and call this sooner, but MEH.  
					FrequencyPenalty = CalculateFrequencyPenaltyBasedOnLevenshteinDistance(persona.lastSpokenText, responseText);
					if (FrequencyPenalty == 2 && retry < maxRetries) return await Evaluate(persona, observations, ++retry, "repetitive");

					// we're not repeating ourselves again.
					if (FrequencyPenalty == 2)
					{
						Logger.Message($"Skipped output due to repetitiveness. Response was {response}");
					}

					return responseText;
				}
				catch (Exception exception)
				{
					Logger.Error($"Error when processing output: [{exception.Message}] {exception.StackTrace} {exception.Source}");
				}
			}
			else if (Tools.DEBUG)
				Logger.Warning($"OUTPUT: null");

			return null;
		}
		public async Task<string> CondenseHistory(Persona persona)
		{
			// force secondary (better model)
			modelSwitchCounter = RimGPTMod.Settings.ModelSwitchRatio;
			var request = new CreateChatCompletionRequest()
			{
				Model = GetCurrentChatGPTModel(),
				Messages =
				[
					new ChatMessage() { Role = "system", Content = $"You are an adversarial system, cleaning up history lists with a goal to remove repetitiveness and keep narration fresh for the following persona: {persona.personality}" },
					new ChatMessage() { Role = "user", Content =  "Summarize the following events into a succinct sentence, focusing on outliers to reduce latching on to the most pronounced theme: " + String.Join("\n ", history)}
				]
			};


			var completionResponse = await OpenAIApi.CreateChatCompletion(request, error => Logger.Error(error));
			var response = (completionResponse.Choices[0].Message.Content ?? "");
			Logger.Message("Condensed History: " + response.ToString());
			return response.ToString(); // The condensed history summary
		}
		public void ReplaceHistory(string reason)
		{
			history = [reason];
		}

		public void ReplaceHistory(string[] reason)
		{
			history = [.. reason];
		}
		public void ReplaceHistory(List<string> reason)
		{
			history = reason;
		}

		// TODO: Need to set Provider based on the settings
		public async Task<(string, string)> SimplePrompt(string input)
		{
			string requestError = null;
			var completionResponse = await OpenAIApi.CreateChatCompletion(new CreateChatCompletionRequest()
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