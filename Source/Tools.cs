using OpenAI;
using RimWorld;
using Steamworks;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Verse;
using Verse.Steam;

namespace RimGPT
{
	public static class Tools
	{
		public static bool DEBUG = false;
		public static readonly Regex tagRemover = new("<color.+?>(.+?)</(?:color)?>", RegexOptions.Singleline);
		public static string[] chatGPTModels = new[] { "gpt-3.5-turbo", "gpt-4" };

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

		public static async void ReloadGPTModels()
		{
			var api = new OpenAIApi(RimGPTMod.Settings.chatGPTKey);
			var response = await api.ListModels();
			var error = response.Error;
			if (error != null)
				return;

			var result = response.Data
				.Select(m => m?.Id)
				.Where(id => id?.StartsWith("gpt") ?? false)
				.OrderBy(id => id)
				.ToArray();
			if (result.Length == 0)
				return;

			chatGPTModels = result;
			Logger.Message($"Loaded {chatGPTModels.Length} GPT models");
		}

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

		public static readonly string[] commonLanguages = new string[]
		{
			"Alien", "Arabic", "Bengali", "Bulgarian", "Catalan", "Chinese", "Croatian", "Czech", "Danish", "Dutch", "English",
			"Estonian", "Finnish", "French", "German", "Greek", "Hebrew", "Hindi", "Hungarian", "Icelandic", "Indonesian",
			"Italian", "Japanese", "Korean", "Latvian", "Lithuanian", "Malay", "Norwegian", "Persian", "Polish", "Portuguese",
			"Punjabi", "Romanian", "Russian", "Serbian", "Slovak", "Slovenian", "Spanish", "Swedish", "Tamil", "Telugu",
			"Thai", "Turkish", "Ukrainian", "Urdu", "Vietnamese", "Welsh", "Yiddish"
		};
	}
}