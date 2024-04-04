using Newtonsoft.Json;
using OpenAI;
using RimWorld;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.Steam;

namespace RimGPT
{
	public static class Tools
	{
		public static bool DEBUG = true;
		public static readonly Regex tagRemover = new("<color.+?>(.+?)</(?:color)?>", RegexOptions.Singleline);
		//public static List<string> chatGPTModels = ["gpt-3.5-turbo-0125", "gpt-4-0125-preview"];

		public readonly struct Strings
		{
			public static readonly string colonist = "Colonist".TranslateSimple();
			public static readonly string enemy = "Enemy".TranslateSimple().CapitalizeFirst();
			public static readonly string visitor = "LetterLabelSingleVisitorArrives".TranslateSimple();
			public static readonly string mechanoid = "Mechanoid";

			public static readonly string information = "ThingInfo".TranslateSimple();
			public static readonly string completed = "StudyCompleted".TranslateSimple().CapitalizeFirst();
			public static readonly string finished = "Finished".TranslateSimple();
			public static readonly string dismiss = "CommandShuttleDismiss".TranslateSimple();
			public static readonly string priority = "Priority".TranslateSimple();
		}

		public static string PlayerName()
		{
			if (SteamManager.Initialized == false)
				return null;
			var name = SteamFriends.GetPersonaName();
			if (name == "Brrainz")
				name = "Andreas"; // for testing
			return name;
		}

		public static void UpdateApiConfigs()
		{
			// Checks if any userApiConfigs are set, if not, return.
			if (RimGPTMod.Settings.userApiConfigs == null || RimGPTMod.Settings.userApiConfigs.Any(a => a.Provider.Length > 0) == false) return;

			// If the apiConfigs are null, get the default configs.
			OpenAIApi.apiConfigs ??= ApiTools.GetApiConfigs();

			// If there's no active config, it defaults to the currentConfig.
			var activeConfig = OpenAIApi.currentConfig;

			foreach (var userConfig in RimGPTMod.Settings.userApiConfigs.Where(a => a.Provider.Length > 0))
			{
				// Update apiConfigs with each userConfig that has a provider chosen.
				var apiConfig = OpenAIApi.apiConfigs.FirstOrDefault(a => a.Provider.ToString() == userConfig.Provider);
				apiConfig.Update(userConfig);

				// Set the activeConfig to the userConfig if it's active.
				activeConfig = userConfig.Active ? apiConfig : activeConfig;
			}
			if (activeConfig != null) OpenAIApi.currentConfig = activeConfig; // Sets the OpenAIApi currentConfig to the activeConfig.
		}

		public static async void ReloadGPTModels()
		{
			UpdateApiConfigs();

			var modelCount = 0;

			// Stores the currentConfig to reset it after the loop.
			var currentConfig = OpenAIApi.currentConfig;

			foreach (var apiConfig in OpenAIApi.apiConfigs.Where(a => a.Configured))
			{
				OpenAIApi.currentConfig = apiConfig;
				ListModelsResponse response;
				try
				{
					if (DEBUG) Logger.Message($"Getting List of Models for {apiConfig.Provider} with {JsonConvert.SerializeObject(apiConfig)}");
					response = await OpenAIApi.ListModels();
				}
				catch (Exception ex)
				{
					Logger.Error($"Error getting List of Models: {ex.Message}");
					continue;
				}
				if (DEBUG) Logger.Message($"{apiConfig.Provider} - baseURL: {apiConfig.BaseUrl} Models: {JsonConvert.SerializeObject(response, Configuration.JsonSerializerSettings)}"); // TEMP

				var error = response.Error;
				if (error != null || response.Data == null) continue;

				// Sets the list of OpenAIModels to this ApiConfig.
				apiConfig.Models = response.Data
					.Where(m => apiConfig.Provider != ApiProvider.OpenAI || (m?.Id.StartsWith("gpt") ?? false))
					.OrderBy(m => m?.Id)
					.ToList();

				if (apiConfig.Models == null) continue;

				modelCount += apiConfig.Models.Count;
			}
			OpenAIApi.currentConfig = currentConfig;
			Logger.Message($"Loaded {modelCount} GPT models");
		}

		public static bool NonEmpty(this string str) => string.IsNullOrEmpty(str) == false;
		public static bool NonEmpty(this TaggedString str) => string.IsNullOrEmpty(str) == false;

		public static string OrderString(this Designation des)
		{
			return des.def.label.NonEmpty() ? des.def.label :
				des.def.LabelCap.NonEmpty() ? des.def.LabelCap :
				des.def.description.NonEmpty() ? des.def.description :
				des.def.defName;
		}

		public static string FormatNumber(long num) => num switch
		{
			>= 1000000000 => (num / 1000000000D).ToString("0.#") + "B",
			>= 1000000 => (num / 1000000D).ToString("0.#") + "M",
			>= 100000 => (num / 1000D).ToString("0") + "K",
			>= 1000 => (num / 1000D).ToString("0.#") + "K",
			_ => num.ToString()
		};


		public static void SafeAsync(Func<Task> function)
		{
			_ = Task.Run(async () =>
			{
				try
				{
					await function();
				}
				catch (Exception ex)
				{
					Logger.Error(ex.ToString());
				}
			});
		}

		public static async Task SafeWait(int milliseconds)
		{
			if (milliseconds == 0)
				return;
			try
			{
				await Task.Delay(milliseconds, RimGPTMod.onQuit.Token);
			}
			catch (TaskCanceledException)
			{
			}
		}

		public static void SafeLoop(Action action, int loopDelay = 0)
		{
			_ = Task.Run(async () =>
			{
				while (RimGPTMod.Running)
				{
					await SafeWait(loopDelay);
					try
					{
						action();
					}
					catch (Exception ex)
					{
						Logger.Error(ex.ToString());
						await SafeWait(1000);
					}
				}
			});
		}

		public static void SafeLoop(Func<Task> function, int loopDelay = 0)
		{
			_ = Task.Run(async () =>
			{
				while (RimGPTMod.Running)
				{
					await SafeWait(loopDelay);
					try
					{
						await function();
					}
					catch (Exception ex)
					{
						Logger.Error(ex.ToString());
						await SafeWait(1000);
					}
				}
			});
		}

		public static void SafeLoop(Func<Task<bool>> function, int loopDelay = 0)
		{
			_ = Task.Run(async () =>
			{
				while (RimGPTMod.Running)
				{
					await SafeWait(loopDelay);
					try
					{
						if (await function())
							continue;
					}
					catch (Exception ex)
					{
						Logger.Error(ex.ToString());
						await SafeWait(1000);
					}
				}
			});
		}

		public static string VoiceLanguage(Persona persona)
		{
			var language = persona.azureVoiceLanguage;
			if (language == "-")
				language = LanguageDatabase.activeLanguage.FriendlyNameEnglish;
			var idx = language.IndexOf(" ");
			if (idx < 0)
				return language;
			return language.Substring(0, idx);
		}

		public static string PersonalityLanguage(Persona persona)
		{
			var language = persona.personalityLanguage;
			if (language == "-")
				language = LanguageDatabase.activeLanguage.FriendlyNameEnglish;
			var idx = language.IndexOf(" ");
			if (idx < 0)
				return language;
			return language.Substring(0, idx);
		}

		public static string Type(this Pawn pawn)
		{
			if (pawn.HostileTo(Faction.OfPlayer))
			{
				if (pawn.RaceProps.IsMechanoid)
					return Strings.mechanoid;
				else
					return Strings.enemy;
			}
			if (pawn.IsColonist || pawn.IsColonyMech)
				return Strings.colonist;
			return Strings.visitor;
		}

		public static string NameAndType(this Pawn pawn)
		{
			return $"{pawn.Type()} '{pawn.LabelShortCap}'";
		}

		public static string ApplyVoiceStyle(this string text, Persona persona)
		{
			var voiceStyle = "funny";

			var value = VoiceStyle.From(persona.azureVoiceStyle)?.Value;
			if (value != null && value != "default" && value != "chat" && value.Contains("-") == false && value.Contains("_") == false)
				voiceStyle = $"very {value}";

			return text.Replace("{VOICESTYLE}", voiceStyle);
		}

		public static string RemovePrefix(this string text, string prefix)
		{
			prefix = prefix.ToLower();
			if (text.ToLower().StartsWith(prefix))
				text = text.Substring(prefix.Length).CapitalizeFirst();
			return text;
		}

		public static string Cleanup(this string text)
		{
			text = text.RemovePrefix("Looks like ");
			text = text.RemovePrefix("Well, well, well ");
			return text;
		}

		public static string ToGameStringFromPOVWithType(this LogEntry entry, Pawn pawn)
		{
			if (pawn == null)
				return null;
			if (pawn.IsColonist == false)
				return null;
			var result = entry.ToGameStringFromPOV(pawn, false);
			var pawns = pawn.Map.mapPawns.AllPawnsSpawned.ToArray();
			for (var i = 0; i < pawns.Length; i++)
			{
				var p = pawns[i];
				if (p.RaceProps.Humanlike)
					result = result.Replace(p.LabelShortCap, p.NameAndType());
			}
			return result;
		}

		public static void ExtractPawnsFromLog(LogEntry entry, out Pawn from, out Pawn to)
		{
			from = null;
			to = null;

			if (entry is BattleLogEntry_Event @event)
			{
				from = @event.initiatorPawn;
				to = @event.subjectPawn;
			}
			else if (entry is BattleLogEntry_DamageTaken damage)
			{
				from = damage.initiatorPawn;
				to = damage.recipientPawn;
			}
			else if (entry is BattleLogEntry_ExplosionImpact explosion)
			{
				from = explosion.initiatorPawn;
				to = explosion.recipientPawn;
			}
			else if (entry is BattleLogEntry_MeleeCombat melee)
			{
				from = melee.initiator;
				to = melee.recipientPawn;
			}
			else if (entry is BattleLogEntry_RangedFire fire)
			{
				from = fire.initiatorPawn;
				to = fire.recipientPawn;
			}
			else if (entry is BattleLogEntry_RangedImpact impact)
			{
				from = impact.initiatorPawn;
				to = impact.recipientPawn;
			}
			else if (entry is BattleLogEntry_StateTransition transition)
				from = transition.subjectPawn;
		}


		// gets the job label from a specific pawn
		public static string GetJobLabelFromPawn(Job job, Pawn driverPawn)
		{
			if (job == null)
			{
				// Returns "Working" as a safe default if job is null.
				return "Working";
			}

			try
			{
				var report = job.GetReport(driverPawn);
				// Ensures report is not null or empty before trying to capitalize.
				if (!string.IsNullOrEmpty(report))
				{
					return report.CapitalizeFirst();
				}
			}
			catch
			{
				// In case of an exception, return "Working" as a safe default.
				return "Working";
			}

			// If GetReport returned null or an empty string, return "Working".
			return "Working";
		}
		// simple pluralization tool, not exhaustive and doesnt cover all cases.
		public static string SimplePluralize(string noun)
		{
			// Basic pluralization rule: add 's' or 'es'
			// Note: This does not cover all English language special cases.
			if (noun.EndsWith("s") || noun.EndsWith("sh") || noun.EndsWith("ch") || noun.EndsWith("x") || noun.EndsWith("z"))
			{
				return $"{noun}es";
			}
			else if (noun.EndsWith("y") && noun.Length > 1 && !"aeiou".Contains(noun[noun.Length - 2]))
			{
				// Words ending in 'y' following a consonant should change the 'y' to 'ies'
				return $"{noun.Substring(0, noun.Length - 1)}ies";
			}
			else if (noun.EndsWith("f") || noun.EndsWith("fe"))
			{
				// Words ending in 'f' or 'fe' may change to "ves" in the plural form
				if (noun.EndsWith("fe"))
				{
					return $"{noun.Substring(0, noun.Length - 2)}ves";
				}
				else
				{
					return $"{noun.Substring(0, noun.Length - 1)}ves";
				}
			}
			// Default pluralization
			else
			{
				return $"{noun}s";
			}
		}

		// Helper method to find a valid translation key.
		public static string FindValidTranslationKey(params string[] keys)
		{
			// Check if the input array is null or empty
			if (keys == null || keys.Length == 0)
				return null;

			foreach (var key in keys)
			{
				if (!string.IsNullOrEmpty(key) && LanguageDatabase.activeLanguage.HaveTextForKey(key))
					return key;
			}
			return null; // No valid key found
		}
		public static string GetIndefiniteArticleFor(string noun)
		{
			// Check for null, empty string, or white space string.
			if (string.IsNullOrWhiteSpace(noun))
			{
				return string.Empty; // Return an empty string if there's an issue.
			}

			try
			{
				char firstLetter = noun.TrimStart()[0];
				bool isVowel = "aeiouAEIOU".IndexOf(firstLetter) >= 0;
				return isVowel ? "an" : "a";
			}
			catch
			{
				// If an exception occurs (e.g., the string is empty after TrimStart), return an empty string.
				return string.Empty;
			}
		}
		public static readonly string[] commonLanguages =
		[
			"Alien",
			"Arabic",
			"Bengali",
			"Bulgarian",
			"Catalan",
			"Chinese",
			"Croatian",
			"Czech",
			"Danish",
			"Dutch",
			"English",
			"Estonian",
			"Finnish",
			"French",
			"German",
			"Greek",
			"Hebrew",
			"Hindi",
			"Hungarian",
			"Icelandic",
			"Indonesian",
			"Italian",
			"Japanese",
			"Korean",
			"Latvian",
			"Lithuanian",
			"Malay",
			"Norwegian",
			"Persian",
			"Polish",
			"Portuguese",
			"Punjabi",
			"Romanian",
			"Russian",
			"Serbian",
			"Slovak",
			"Slovenian",
			"Spanish",
			"Swedish",
			"Tamil",
			"Telugu",
			"Thai",
			"Turkish",
			"Ukrainian",
			"Urdu",
			"Vietnamese",
			"Welsh",
			"Yiddish"
		];
	}
}