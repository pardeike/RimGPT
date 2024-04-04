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
	public static class UpdateResources
	{
		public static int lastTotal = -1;
		public static readonly HashSet<ThingCategoryDef> interestingThingCategories;

		static UpdateResources()
		{
			interestingThingCategories = new[] { "Foods", "FoodMeals", "Drugs", "Medicine", "Weapons", "StoneBlocks", "Manufactured", "ResourcesRaw" }
				.Select(name => DefDatabase<ThingCategoryDef>.GetNamed(name, false)).ToHashSet();
		}

		public static bool Reportable(KeyValuePair<ThingDef, int> pair)
		{
			if (pair.Value == 0)
				return false;
			var hashSet = pair.Key.thingCategories?.ToHashSet() ?? [];
			return hashSet.Intersect(interestingThingCategories).Any();
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
				RecordKeeper.ResourceData = $"total {colonistCount} colonist(s), {amountList}";
				//Logger.Message($"RecordKeeper.ResourceData: {RecordKeeper.ResourceData}");
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
	public static class UpdateEnergyStatus
	{
		public static void Task(Map map)
		{
			// Early exit if energy status reporting is disabled
			if (!RimGPTMod.Settings.reportEnergyStatus)
				return;

			// Initialize lists for producers and consumers
			var producers = new List<EnergyProducer>();
			var consumers = new List<EnergyConsumer>();

			// Calculate Total Power Generated and populate producers list
			float totalPowerGenerated = 0f;
			foreach (var powerPlant in map.listerBuildings.allBuildingsColonist.Where(building => building.GetComp<CompPowerPlant>() != null))
			{
				var compPowerPlant = powerPlant.GetComp<CompPowerPlant>();
				if (compPowerPlant.PowerOn)
				{
					totalPowerGenerated += compPowerPlant.PowerOutput;
					producers.Add(new EnergyProducer { Label = powerPlant.Label, PowerOutput = compPowerPlant.PowerOutput });
				}
			}

			// Calculate Total Power Needs and populate consumers list
			float totalPowerNeeded = 0f;
			foreach (var consumerBuilding in map.listerBuildings.allBuildingsColonist.Select(building => building.GetComp<CompPowerTrader>()).Where(comp => comp != null && comp.PowerOutput < 0))
			{
				if (consumerBuilding.PowerOn)
				{
					float powerConsumed = consumerBuilding.Props.basePowerConsumption;
					totalPowerNeeded += powerConsumed;
					consumers.Add(new EnergyConsumer { Label = consumerBuilding.parent.Label, PowerConsumed = powerConsumed });
				}
			}

			// Determine the overall power status
			var powerDelta = totalPowerGenerated - totalPowerNeeded;
			string powerStatus;
			if (powerDelta < 0)
				powerStatus = "Failure";
			else if (powerDelta < 200)
				powerStatus = "Unstable (Brownouts possible)";
			else if (powerDelta > 700)
				powerStatus = "Surplus";
			else
				powerStatus = "Stable";


			EnergyData energyData = new EnergyData
			{
				Producers = producers,
				Consumers = consumers,
				TotalPowerGenerated = totalPowerGenerated,
				TotalPowerNeeded = totalPowerNeeded,
				PowerStatus = powerStatus
			};

			RecordKeeper.EnergyStatus = energyData;
			//Logger.Message($"RecordKeeper.EnergyStatus: {RecordKeeper.EnergySummary}");
		}

	}

	// reports on notable rooms, avoiding unlabeled or undefined rooms, or rooms simply defined as "room"
	//
	public static class UpdateRoomStatus
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
				RecordKeeper.RoomsData = roomsList;
			//Logger.Message($"RecordKeeper.RoomsDataSummary: {RecordKeeper.RoomsDataSummary}");
		}
	}

	// reports on current Research, helping AI understand research context in the game
	//
	public static class UpdateResearchStatus
	{
		public static void Task(Map map)
		{
			if (RimGPTMod.Settings.reportResearchStatus == false)
				return;

			if (map.IsPlayerHome == false)
				return;

			var currentResearchDef = Find.ResearchManager.currentProj; // could be null if no current research

			var completedResearchDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading
																			.Where(r => r.IsFinished)
																			.ToList();

			var availableResearchDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading
																			.Where(r => !r.IsFinished && r.PrerequisitesCompleted)
																			.ToList();

			RecordKeeper.CurrentResearch = currentResearchDef;
			RecordKeeper.CompletedResearch = completedResearchDefs;
			RecordKeeper.AvailableResearch = availableResearchDefs;
			//Logger.Message($"RecordKeeper.ResearchDataSummary: {RecordKeeper.ResearchDataSummary}");
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
			Personas.Add(message, 1);
			RecordKeeper.ColonySetting = message;
			//Logger.Message($"RecordKeeper.ColonySetting: {RecordKeeper.ColonySetting}");			
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
					Health = [],
					AllowedWorkTypes = [],
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

				foreach (var workTypeDef in DefDatabase<WorkTypeDef>.AllDefsListForReading)
				{
					if (!colonist.WorkTypeIsDisabled(workTypeDef))
					{
						colonistData.AllowedWorkTypes.Add(workTypeDef.labelShort);
					}
				}

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

			RecordKeeper.ColonistRecords = colonists;
			//Logger.Message($"RecordKeeper.ColonistDataSummary: {RecordKeeper.ColonistDataSummary.Join()}");
		}
	}
}