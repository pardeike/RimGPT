using HarmonyLib;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimGPT
{
	public static class RecordKeeper
	{
		 private static ConcurrentBag<ColonistData> colonistRecords = new ConcurrentBag<ColonistData>();
		private static ResearchData researchData = new ResearchData();
		public static string ColonySetting = "Unknown as of now...";
		private static string resourceData = "";
		public static EnergyData EnergyStatus { get; set; }
		private static ConcurrentBag<string> roomsData = new ConcurrentBag<string>();
		
		public static IEnumerable<ColonistData> ColonistRecords
		{
			get => colonistRecords;
        set
        {
            colonistRecords = new ConcurrentBag<ColonistData>(value);
        }
		}

		public static ResearchData ResearchData
		{
				get => researchData;
				set => researchData = value;
		}
		public static string ResourceData
		{
			get => resourceData;
			set => resourceData = value;
		}

		public static IEnumerable<string> RoomsData
		{
			get => roomsData;
			set => roomsData =  new ConcurrentBag<string>(value);
		}

		public static List<ResearchProjectDef> CompletedResearch
		{
			get => researchData.Completed;
			set => researchData.Completed = value;
		}

		public static ResearchProjectDef CurrentResearch
		{
			get => researchData.Current;
			set => researchData.Current = value;
		}

		public static List<ResearchProjectDef> AvailableResearch
		{
			get => researchData.Available;
			set => researchData.Available = value;
		}

    public static string EnergySummary
    {
        get
        {
            if (EnergyStatus != null)
            {
                return EnergyStatus.ToString();
            }
            else
            {
                return "";
            }
        }
    }
		public static string RoomsDataSummary => string.Join(", ", RoomsData);
 	 	public static string ResearchDataSummary => researchData != null ? researchData.ToString() : "";
		
		public static string[] ColonistDataSummary => ColonistRecords.Select(colonist =>
		{
			var dataBuilder = new StringBuilder();
			AddBasicInformation(dataBuilder, colonist);
			AddSkillsInformation(dataBuilder, colonist);
			AddTraitsInformation(dataBuilder, colonist);
			AddHealthInformation(dataBuilder, colonist);
			AddWorkInformation(dataBuilder, colonist);
			return dataBuilder.ToString();
		}).ToArray();

		private static void AddBasicInformation(StringBuilder builder, ColonistData colonist)
		{
			builder.Append($"Colonist {colonist.Name}, a {colonist.Age} year old {colonist.Gender} with a mood of {colonist.Mood}.");
		}

		private static void AddSkillsInformation(StringBuilder builder, ColonistData colonist)
		{
			var skillGroups = colonist.Skills
				.GroupBy(skill => skill.Value.Level)
				.OrderBy(group => group.Key);

			var skillDescriptions = skillGroups.Select(group => $"{group.Select(skill => skill.Key).Join()} are at skill level {group.Key}");
			builder.Append($" {colonist.Name}'s skill levels are: {skillDescriptions.Join(delimiter: "; ")}.");
		}

		private static void AddTraitsInformation(StringBuilder builder, ColonistData colonist)
		{
			if (colonist.Traits?.Any() == true)
				builder.Append($" {colonist.Name}'s notable traits are: {colonist.Traits.Join()}.");
		}

		private static void AddHealthInformation(StringBuilder builder, ColonistData colonist)
		{
			var health = colonist.Health;
			if (health != null && health.Any())
			{
				var hediffDescriptions = health.Select(hediff =>
				{
					var locationInfo = !string.IsNullOrWhiteSpace(hediff.Location) ? $" on {hediff.Location}" : "";
					var immunityInfo = hediff.Immunity.HasValue ? $" with an immunity of {hediff.Immunity:P1}" : "";
					var bleedingInfo = hediff.Bleeding ? ", currently bleeding" : "";
					return $"{hediff.Label}{locationInfo}: Severity: {hediff.Severity:F2}{immunityInfo}{bleedingInfo}";
				})
				.ToList();

				string hediffsText;
				if (hediffDescriptions.Count == 1)
					hediffsText = $"a {hediffDescriptions.Single()}";
				else
				{
					var lastElementIndex = hediffDescriptions.Count - 1;
					hediffDescriptions[lastElementIndex] = "and " + hediffDescriptions[lastElementIndex];
					hediffsText = hediffDescriptions.Join();
				}

				builder.Append($" {colonist.Name} has {hediffsText}.");
			}
		}

		private static void AddWorkInformation(StringBuilder builder, ColonistData colonist)
		{
			if (colonist.AllowedWorkTypes?.Any() == true)
			{
				string workTypes = string.Join(", ", colonist.AllowedWorkTypes.Select(wt => wt.ToString()).ToArray());
				builder.Append($"{colonist.Name}'s allowed work types are: {workTypes}.");
			}
		}



	}
}
