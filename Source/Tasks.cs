using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimGPT
{
	// reports on colony resources
	//
	public static class ReportResources
	{
		public static int lastTotal = -1;
		public static readonly HashSet<ThingCategoryDef> thingCategories =
		[
			ThingCategoryDefOf.Foods,
			ThingCategoryDefOf.FoodMeals,
			ThingCategoryDefOf.Medicine,
			ThingCategoryDefOf.StoneBlocks,
			ThingCategoryDefOf.Manufactured,
			ThingCategoryDefOf.ResourcesRaw
		];

		public static bool Reportable(KeyValuePair<ThingDef, int> pair)
		{
			if (pair.Value == 0)
				return false;
			var hashSet = pair.Key.thingCategories?.ToHashSet() ?? [];
			return hashSet.Intersect(thingCategories).Any();
		}

		public static void Task(Map map)
		{
			var amounts = map.resourceCounter.AllCountedAmounts.Where(Reportable).ToArray();
			var total = amounts.Sum(pair => pair.Value);

			if (amounts.Any() && total != lastTotal)
			{
				lastTotal = total;
				var colonistCount = map.mapPawns.FreeColonistsCount;
				var amountList = amounts.Select(pair => $"{pair.Value} {pair.Key.label.CapitalizeFirst()}").Join();
				Personas.Add($"Minor update: total {colonistCount} colonist(s), {amountList}", 2);
			}
		}
	}

	// reports on colonist thoughts and mood, used as a periodic report to keep AI informed
	//
	public static class ReportColonistThoughts
	{
		public static void Task(Map map)
		{
			if (RimGPTMod.Settings.reportColonistThoughts == false)
				return;

			foreach (var colonist in map.mapPawns.FreeColonists)
			{
				Dictionary<string, (int Count, float MoodEffect)> thoughtCounts = [];

				foreach (var thought in colonist.needs.mood.thoughts.memories.Memories)
				{
					var label = thought.LabelCap;
					var moodEffect = thought.MoodOffset();

					if (thoughtCounts.ContainsKey(label))
						thoughtCounts[label] = (thoughtCounts[label].Count + 1, thoughtCounts[label].MoodEffect + moodEffect);
					else
						thoughtCounts.Add(label, (1, moodEffect));
				}

				// create a single line summary of thoughts for each colonist, mentioning mood effects only if they are non-zero
				var formattedThoughts = thoughtCounts.Select(kvp =>
				{
					var plural = kvp.Value.Count > 1 ? $"(x{kvp.Value.Count})" : "";
					var moodEffectDescription = "";
					if (kvp.Value.MoodEffect != 0.0f)
						moodEffectDescription = $"{(kvp.Value.MoodEffect > 0 ? " (improving mood by " : " (decreasing mood by ")}{Math.Abs(kvp.Value.MoodEffect)})";
					return $"{kvp.Key}{plural}{moodEffectDescription}";
				})
				.Where(s => string.IsNullOrEmpty(s) == false);

				var thoughtsMessage = $"{colonist.Name.ToStringShort}'s recent thoughts: {formattedThoughts.Join()}";
				Personas.Add(thoughtsMessage, 2);
			}
		}
	}

	// report colonists opinions of each other, used as a periodic report of colonists opinions of eachother
	//
	public static class ReportColonistOpinions
	{
		public static Dictionary<(Pawn, Pawn), int> lastOpinions = [];

		public static void Task(Map map)
		{
			if (RimGPTMod.Settings.reportColonistOpinions == false)
				return;

			var opinionMessages = new List<string>();
			var freeColonistsCopy = map.mapPawns.FreeColonists.ToList();

			foreach (var colonist in freeColonistsCopy)
				foreach (var otherColonist in freeColonistsCopy)
				{
					if (colonist == otherColonist)
						continue;

					var currentOpinion = colonist.relations.OpinionOf(otherColonist);

					if (lastOpinions.TryGetValue((colonist, otherColonist), out var previousOpinion))
					{
						var trend = currentOpinion == previousOpinion ? "stayed the same" : currentOpinion > previousOpinion ? "improved" : "worsened";
						opinionMessages.Add($"{colonist.Name} current opinion of {otherColonist.Name}: {currentOpinion} (has {trend} since yesterday)");
					}
					else
						opinionMessages.Add($"{colonist.Name} current opinion of {otherColonist.Name}: {currentOpinion}");

					lastOpinions[(colonist, otherColonist)] = currentOpinion;
				}

			if (opinionMessages.Any())
			{
				var consolidatedMessage = opinionMessages.Join(delimiter: "\n");
				Personas.Add(consolidatedMessage, 2);
			}
		}
	}

	// reports on colony Energy status, is skipped if the colony does not have any energy buildings or needs
	//
	public static class ReportEnergyStatus
	{
		public static void Task(Map map)
		{
			if (RimGPTMod.Settings.reportEnergyStatus == false)
				return;

			var messages = new List<string>();

			var totalPowerGenerated = 0f;

			var powerGeneratorBuildings = new List<string>();
			var allPowerGeneratingBuildings = map.listerBuildings.allBuildingsColonist
				.Where(building => building.GetComp<CompPowerPlant>() != null);

			foreach (var powerPlant in allPowerGeneratingBuildings)
			{
				var compPowerPlant = powerPlant.GetComp<CompPowerPlant>();
				totalPowerGenerated += compPowerPlant.PowerOn ? compPowerPlant.PowerOutput : 0f;
				var message = $"{powerPlant.Label} (Power Output: {compPowerPlant.PowerOutput})";
				powerGeneratorBuildings.Add(message);
			}

			if (allPowerGeneratingBuildings.Any() == false)
				messages.Add("Power Generators: None");
			else
				messages.Add("Power Generators: " + powerGeneratorBuildings.Join());

			var totalPowerNeeds = CalculateTotalPowerNeeds(map, messages);

			var powerDelta = totalPowerGenerated - totalPowerNeeds;
			(var powerStatus, var priority) = DeterminePowerStatus(powerDelta);

			var allBuildingsWithPowerThatUseFuelComp = map.listerBuildings.allBuildingsColonist
				.Where(building =>
				{
					var compRefuelable = building.GetComp<CompRefuelable>();
					var compPowerPlant = building.GetComp<CompPowerPlant>();
					return compRefuelable != null && compPowerPlant != null && compPowerPlant.PowerOn;
				}).ToList();


			var totalPowerNeedsMessage = $"Total Power needs: {totalPowerNeeds}, Total Power Generated: {totalPowerGenerated}";
			messages.Add(totalPowerNeedsMessage);
			if (totalPowerNeeds > 0 || totalPowerGenerated > 0)
				// dont talk about power if there is no power
				Personas.Add("Energy Analysis: " + powerStatus + "\n" + messages.Join(), priority);

		}

		public static float CalculateTotalPowerNeeds(Map map, List<string> messages)
		{
			var totalPowerNeeds = 0f;

			var allCompPowerTraders = map.listerBuildings.allBuildingsColonist
				.Select(building =>
				{
					var compPowerTrader = building.GetComp<CompPowerTrader>();
					if (compPowerTrader?.PowerOutput < 0)
						return (null, null);
					return (label: building.Label, comp: compPowerTrader);
				})
				.Where(pair => pair.comp != null);

			var powerConsumptionMessages = new List<string>();
			foreach (var (label, compPowerTrader) in allCompPowerTraders)
			{

				var powerConsumed = compPowerTrader.PowerOn ? compPowerTrader.Props.basePowerConsumption : 0f;
				if (powerConsumed <= 0) break; // dont add power generators, they get added here too because they're power trader
				totalPowerNeeds += powerConsumed;

				// add each building's power consumption details to the list
				var message = $"{label} (Power Consumption: {powerConsumed})";
				powerConsumptionMessages.Add(message);
			}

			if (powerConsumptionMessages.Any())
				messages.Add("Power Consumption: " + powerConsumptionMessages.Join());

			return totalPowerNeeds;
		}

		public static (string, int) DeterminePowerStatus(float powerDelta)
		{
			if (powerDelta < 0) return ("Failure", 3);
			if (powerDelta < 200) return ("Unstable (Brownouts possible)", 3);
			if (powerDelta > 700) return ("Surplus", 4);
			return ("Stable", 4);
		}
	}

	// reports on notable rooms, avoiding unlabeled or undefined rooms, or rooms simply defined as "room"
	//
	public static class ReportRoomStatus
	{
		public static void Task(Map map)
		{
			if (RimGPTMod.Settings.reportRoomStatus == false)
				return;

			if (map.areaManager == null)
				return;

			if (map.ParentFaction != Faction.OfPlayer)
				return;

			var homeArea = map.areaManager.Home;
			var roomsList = new List<string>();

			foreach (var room in map.regionGrid.allRooms)
			{
				var roleLabel = room.Role?.label ?? "Undefined";

				// prevent reporting any room that is not properly named - such as single doors or hallways
				if (roleLabel == "Undefined" || roleLabel == "none" || roleLabel == "room")
					continue;

				foreach (var cell in room.Cells)
				{
					if (homeArea[cell])
					{
						var statsStringBuilder = new System.Text.StringBuilder();
						if (room.stats != null)
							foreach (var statPair in room.stats)
								if (statPair.Key != null)
									statsStringBuilder.Append($"{statPair.Key.label} is {statPair.Value}, ");

						var statsString = statsStringBuilder.ToString().TrimEnd(',', ' ');

						var article = roleLabel.IndexOf("'s", StringComparison.OrdinalIgnoreCase) != -1 ? "" : "A ";
						var sentenceEnding = ", and ";
						roomsList.Add($"{article}{roleLabel}, its " + statsString.TrimEnd(sentenceEnding.ToCharArray()) + ".");

						break; // we have our information, so exit
					}
				}
			}

			if (roomsList.Count > 0)
				Personas.Add("Notable Rooms in the Colony: " + roomsList.Join(delimiter: "\n"), 1);
		}
	}

	// reports on current Research, helping AI understand research context in the game
	//
	public static class ReportResearchStatus
	{
		public static void Task(Map map)
		{
			if (RimGPTMod.Settings.reportResearchStatus == false)
				return;

			if (map.IsPlayerHome == false)
				return;

			// already researched projects
			var completedResearch = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(research => research.IsFinished);
			var completedResearchNames = completedResearch.Select(r => r.label).Join();
			var completedMessage = $"Already Known: {completedResearchNames}";

			// Now do current research
			var currentResearch = Find.ResearchManager.currentProj;
			var researchInProgress = currentResearch != null ? currentResearch.label : "None";
			var currentMessage = $"Current Research: {researchInProgress}";

			// Now do available research that is not locked
			var availableResearch = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(research => !research.IsFinished && research.PrerequisitesCompleted);
			var availableResearchNames = availableResearch.Select(r => r.label).Join();
			var availableMessage = $"Available Research: {availableResearchNames}";

			Personas.Add($"Research Update: {completedMessage}\n{currentMessage}\n{availableMessage}", 1);
		}
	}

	// updates the Colony Setting, including weather, date, name of colony, etc
	// used to update the record keeper, which in turn is used by the AI as part of game state
	//
	public static class UpdateColonySetting
	{
		public static void Task(Map map)
		{
			var currentWeather = map.weatherManager.curWeather;
			var currentSeason = GenLocalDate.Season(map);
			var seasonName = currentSeason.LabelCap();
			var tileIndex = map.Tile;
			var tileLatLong = Find.WorldGrid.LongLatOf(tileIndex);
			long currentTicks = Find.TickManager.TicksAbs;
			var fullDateString = GenDate.DateFullStringAt(currentTicks, tileLatLong);
			var totalDays = GenDate.DaysPassed;
			var years = totalDays / GenDate.DaysPerYear;
			var quadrums = (totalDays % GenDate.DaysPerYear) / GenDate.DaysPerQuadrum;
			var days = (totalDays % GenDate.DaysPerYear) % GenDate.DaysPerQuadrum;

			var settlementName = map.Parent.LabelCap;
			var biome = map.Biome;
			string biomeName = biome.LabelCap;
			var biomeDescription = biome.description;

			var quadrumsMonthsSeasons = new List<string>();
			for (var quadrumIndex = 0; quadrumIndex < 4; quadrumIndex++)
			{
				var quadrum = (Quadrum)quadrumIndex;
				var season = GenDate.Season((quadrumIndex * GenDate.DaysPerQuadrum + 5) * GenDate.TicksPerDay, tileLatLong);
				quadrumsMonthsSeasons.Add($"{quadrum.Label()} is {season}");
			}
			var quadrumsMonthsSeasonsString = quadrumsMonthsSeasons.Join();

			var message = $"Current Season: {seasonName}, Yearly Seasons Overview: {quadrumsMonthsSeasonsString}\n " +
							  $"Each Quadrum lasts 15 days, and there are 4 Quadrums per year\n" +
							  $"Today is: {fullDateString}, The current Settlement name is: {settlementName}\n " +
							  $"Our colony is {years} years {quadrums} quadrums {days} days old\n " +
							  $"Current weather: {currentWeather.LabelCap}\n " +
							  $"Temperature: {map.mapTemperature.OutdoorTemp:0.#}°C\n " +
							  $"Area: {biomeName}, {biomeDescription}";
			Personas.Add(message, 3);
		}
	}

	// updates the List of all colonists and their skills and does not send a message to AI, rather it updates
	// the record keeper, which in turn is used by the AI as part of the game state
	//
	public static class UpdateColonistRoster
	{
		public static void Task(Map map)
		{
			if (RimGPTMod.Settings.reportColonistRoster == false)
				return;

			var colonists = new List<ColonistData>();

			foreach (var colonist in map.mapPawns.FreeColonists)
			{
				var colonistData = new ColonistData
				{
					Name = colonist.Name.ToStringFull,
					Gender = colonist.gender.ToString(),
					Age = colonist.ageTracker.AgeBiologicalYears,
					Mood = colonist.needs.mood?.CurLevelPercentage.ToStringPercent() ?? "Unknown",
					Skills = [],
					Traits = [],
					Health = []
				};

				foreach (var skill in colonist.skills.skills)
					colonistData.Skills.Add(skill.def.LabelCap, new SkillData
					{
						Level = skill.Level,
						XpSinceLastLevel = skill.xpSinceLastLevel,
						XpRequiredForLevelUp = skill.XpRequiredForLevelUp
					});

				foreach (var trait in colonist.story.traits.allTraits)
					colonistData.Traits.Add(trait.Label);

				foreach (var hediff in colonist.health.hediffSet.hediffs)
				{
					var immunityComp = (hediff as HediffWithComps)?.TryGetComp<HediffComp_Immunizable>();
					colonistData.Health.Add(new HediffData
					{
						Label = hediff.Label,
						Severity = hediff.Severity,
						IsPermanent = hediff.IsPermanent(),
						Immunity = immunityComp?.Immunity,
						Location = hediff.Part?.Label,
						Bleeding = hediff.Bleeding
					});
				}

				colonists.Add(colonistData);
			}

			RecordKeeper.CollectColonistData(colonists);
			Log.Message(RecordKeeper.FetchColonistData().Join(delimiter: "\n\n"));
		}
	}
}