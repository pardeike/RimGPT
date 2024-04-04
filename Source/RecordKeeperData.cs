using System.Collections.Generic;
using Verse;
using System.Linq;

namespace RimGPT
{
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
		public List<string> AllowedWorkTypes { get; set; }
	}

	public class SkillData
	{
		public int Level { get; set; }
		public float XpSinceLastLevel { get; set; }
		public float XpRequiredForLevelUp { get; set; }
	}

	public class ResearchData
	{
		public List<ResearchProjectDef> Completed { get; set; }
		public ResearchProjectDef Current { get; set; } // Can be null if there is no current research
		public List<ResearchProjectDef> Available { get; set; }

		public override string ToString()
		{
			if (Completed == null) return "";
			if (Available == null) return "";
			// Join function must be provided or use String.Join for handling arrays
			string completedResearchString = string.Join(", ", Completed.Select(r => r.label).ToArray());
			string currentResearchString = Current?.label ?? "None";
			string availableResearchString = string.Join(", ", Available.Select(r => r.label).ToArray());

			return $"Already Known: {completedResearchString}\n" +
						 $"Current Research: {currentResearchString}\n" +
						 $"Available Research: {availableResearchString}";
		}
	}




}
