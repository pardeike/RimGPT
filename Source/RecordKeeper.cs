using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimGPT
{
    public static class RecordKeeper
    {
        public static List<ColonistData> ColonistRecords { get; set; } = new List<ColonistData>();
        public static string ColonySetting { get; set; }

        public static string FetchColonySetting()
        {
            return ColonySetting ?? "Unknown as of now...";
        }

        public static string[] FetchColonistData()
        {
            return ColonistRecords.Select(CreateColonistDataString).ToArray();
        }

        private static string CreateColonistDataString(ColonistData colonist)
        {
            var dataBuilder = new StringBuilder();

            AddBasicInformation(dataBuilder, colonist);
            AddSkillsInformation(dataBuilder, colonist);
            AddTraitsInformation(dataBuilder, colonist);
            AddHealthInformation(dataBuilder, colonist);

            return dataBuilder.ToString();
        }

        private static void AddBasicInformation(StringBuilder builder, ColonistData colonist)
        {
            builder.Append($"Colonist {colonist.Name}, a {colonist.Age} year old {colonist.Gender} with a mood of {colonist.Mood}.");
        }

        private static void AddSkillsInformation(StringBuilder builder, ColonistData colonist)
        {
            var skillGroups = colonist.Skills
                                      .GroupBy(skill => skill.Value.Level)
                                      .OrderBy(group => group.Key);

            var skillDescriptions = skillGroups.Select(group =>
                $"{string.Join(", ", group.Select(skill => skill.Key))} are at skill level {group.Key}"
            );

            builder.Append($" {colonist.Name}'s skill levels are: {string.Join("; ", skillDescriptions)}.");
        }

        private static void AddTraitsInformation(StringBuilder builder, ColonistData colonist)
        {
            if (colonist.Traits?.Any() == true)
            {
                builder.Append($" {colonist.Name}'s notable traits are: {string.Join(", ", colonist.Traits)}.");
            }
        }

        private static void AddHealthInformation(StringBuilder builder, ColonistData colonist)
        {
            if (colonist.Health?.Any() == true)
            {
                var hediffDescriptions = colonist.Health.Select(hediff =>
                {
                    string locationInfo = !string.IsNullOrWhiteSpace(hediff.Location) ? $" on {hediff.Location}" : "";
                    string immunityInfo = hediff.Immunity.HasValue ? $" with an immunity of {hediff.Immunity:P1}" : "";
                    string bleedingInfo = hediff.Bleeding ? ", currently bleeding" : "";
                    return $"{hediff.Label}{locationInfo}: Severity: {hediff.Severity:F2}{immunityInfo}{bleedingInfo}";
                }).ToList();

                string hediffsText;
                if (hediffDescriptions.Count == 1)
                {
                    hediffsText = $"a {hediffDescriptions.Single()}";
                }
                else
                {
                    int lastElementIndex = hediffDescriptions.Count - 1; // Index for the last element
                    hediffDescriptions[lastElementIndex] = "and " + hediffDescriptions[lastElementIndex]; // Insert "and" before the last hediff description
                    hediffsText = string.Join(", ", hediffDescriptions);
                }

                builder.Append($" {colonist.Name} has {hediffsText}.");
            }
        }
        public static void CollectColonistData(List<ColonistData> colonists)
        {
            ColonistRecords = colonists;
        }

        public class HediffData
        {
            public string Label { get; set; }
            public float Severity { get; set; }
            public bool IsPermanent { get; set; }
            public float? Immunity { get; set; }
            public string Location { get; set; }
            public bool Bleeding { get; set; }
        }

        public class ColonistData
        {
            public string Name { get; set; }
            public string Gender { get; set; }
            public int Age { get; set; }
            public string Mood { get; set; }
            public Dictionary<string, SkillData> Skills { get; set; }
            public List<string> Traits { get; set; }
            public List<HediffData> Health { get; set; }
        }

        public class SkillData
        {
            public int Level { get; set; }
            public float XpSinceLastLevel { get; set; }
            public float XpRequiredForLevelUp { get; set; }
        }
    }
}
