using System;
using System.Collections.Generic;
using System.Text;
using Verse;
namespace RimGPT
{
    public static class RecordKeeper
    {
        private static Dictionary<(Pawn, Pawn), int> dailyOpinions = new Dictionary<(Pawn, Pawn), int>();
        public static List<ColonistData> ColonistRecords { get; set; } = new List<ColonistData>();
        public static String ColonySetting { get; set; }

        public class HediffData
        {
            public string Label { get; set; }
            public float Severity { get; set; }
            public bool IsPermanent { get; set; }
            public float? Immunity { get; set; }
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
        public static string FetchColonySetting()
        {
            if (ColonySetting != null) return ColonySetting;
            return "Unknown as of now...";
        }
        public static string[] FetchColonistData()
        {
            List<string> colonistDataStrings = new List<string>();

            foreach (var colonist in ColonistRecords)
            {
                StringBuilder dataBuilder = new StringBuilder();

                // Add basic information
                dataBuilder.AppendLine($"Name: {colonist.Name}");
                dataBuilder.AppendLine($"Gender: {colonist.Gender}");
                dataBuilder.AppendLine($"Age: {colonist.Age}");
                dataBuilder.AppendLine($"Mood: {colonist.Mood}");

                // Add skills information
                dataBuilder.AppendLine("Skills:");
                foreach (var skill in colonist.Skills)
                {
                    dataBuilder.AppendLine($"- {skill.Key}: Level {skill.Value.Level}, XP Since Last Level {skill.Value.XpSinceLastLevel}, XP Required for Level Up {skill.Value.XpRequiredForLevelUp}");
                }

                // Add traits information
                dataBuilder.AppendLine("Traits:");
                foreach (var trait in colonist.Traits)
                {
                    dataBuilder.AppendLine($"- {trait}");
                }

                // Add health (hediffs) information, including immunity where applicable
                if(colonist.Health != null && colonist.Health.Count > 0)
                {
                    dataBuilder.AppendLine("Health Issues:");
                    foreach (var hediff in colonist.Health)
                    {
                        string immunityInfo = hediff.Immunity.HasValue ? $", Immunity: {hediff.Immunity.Value:P1}" : "";
                        dataBuilder.AppendLine($"- Ailment: {hediff.Label}, Severity: {hediff.Severity:F2}{immunityInfo}");
                    }
                }

                // Add this colonist's data string to the list
                colonistDataStrings.Add(dataBuilder.ToString());
            }

            return colonistDataStrings.ToArray();
        }

        public class SkillData
        {
            public int Level { get; set; }
            public float XpSinceLastLevel { get; set; }
            public float XpRequiredForLevelUp { get; set; }
        }

        public static void CollectColonistData(List<ColonistData> colonists)
        {
            // Here we simply replace the current records with the new data.
            // Depending on the requirements, you might want to merge or update this data instead.
            ColonistRecords = colonists;
        }

    }
}