using System.Collections.Generic;

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
	}

	public class SkillData
	{
		public int Level { get; set; }
		public float XpSinceLastLevel { get; set; }
		public float XpRequiredForLevelUp { get; set; }
	}
}
