using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimGPT
{
	public static class RecordKeeper
	{
		public static List<ColonistData> colonistRecords = [];
		public static string colonySetting = "Unknown as of now...";

		public static void CollectColonistData(List<ColonistData> colonists)
		{
			colonistRecords = colonists;
		}

		public static string[] FetchColonistData()
		{
			return colonistRecords.Select(colonist =>
			{
				var dataBuilder = new StringBuilder();

				AddBasicInformation(dataBuilder, colonist);
				AddSkillsInformation(dataBuilder, colonist);
				AddTraitsInformation(dataBuilder, colonist);
				AddHealthInformation(dataBuilder, colonist);

				return dataBuilder.ToString();

			}).ToArray();
		}

		public static string FetchColonySetting() => colonySetting;

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
				}).ToList();

				string hediffsText;
				if (hediffDescriptions.Count == 1)
				{
					hediffsText = $"a {hediffDescriptions.Single()}";
				}
				else
				{
					var lastElementIndex = hediffDescriptions.Count - 1;
					hediffDescriptions[lastElementIndex] = "and " + hediffDescriptions[lastElementIndex];
					hediffsText = hediffDescriptions.Join();
				}

				builder.Append($" {colonist.Name} has {hediffsText}.");
			}
		}
	}
}
