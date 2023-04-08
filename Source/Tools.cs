using System;
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

        public static string Language => LanguageDatabase.activeLanguage.FriendlyNameNative;

        public static string Type(this Pawn pawn)
        {
            if (pawn.HostileTo(Faction.OfPlayer))
            {
                if (pawn.RaceProps.IsMechanoid) return Strings.mechanoid;
                else return Strings.enemy;
            }
            if (pawn.IsColonist) return Strings.colonist;
            return Strings.visitor;
        }

        public static string NameAndType(this Pawn pawn)
        {
            return $"{pawn.Type()} '{pawn.LabelShortCap}'";
        }

        public static string ToGameStringFromPOVWithType(this LogEntry entry, Pawn from, Pawn to)
        {
            var result = entry.ToGameStringFromPOV(from, false);
            var pawns = from.Map.mapPawns.AllPawnsSpawned.ToArray();
            for (var i = 0; i < pawns.Length; i++)
            {
                var pawn = pawns[i];
                if (pawn.RaceProps.Humanlike)
                    result = result.Replace(pawn.LabelShortCap, pawn.NameAndType());
            }
            //if (from != null) result = result.Replace(from.LabelShortCap, from.NameAndType());
            //if (to != null) result = result.Replace(to.LabelShortCap, to.NameAndType());
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

