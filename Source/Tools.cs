using RimWorld;
using Verse;

namespace RimGPT
{
	public static class Tools
	{
		public struct Strings
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

		public static string Language
		{
			get
			{
				var language = LanguageDatabase.activeLanguage.FriendlyNameEnglish;
				var idx = language.IndexOf(" ");
				if (idx< 0) return language;
				return language.Substring(0, idx);
			}
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
			if (pawn.IsColonist)
				return Strings.colonist;
			return Strings.visitor;
		}

		public static string NameAndType(this Pawn pawn)
		{
			return $"{pawn.Type()} '{pawn.LabelShortCap}'";
		}

		public static string ApplyVoiceStyle(this string text)
		{
			var voiceStyle = "very funny";

			var value = VoiceStyle.From(RimGPTMod.Settings.azureVoiceStyle)?.Value;
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
	}
}

