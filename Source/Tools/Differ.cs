using System.Collections.Generic;

namespace RimGPT
{
	public static class Differ
	{
		static readonly Dictionary<string, string> previousValues = new();

		public static bool Changed(string key, string currentValue)
		{
			currentValue ??= "";
			if (previousValues.TryGetValue(key, out var previousValue) == false)
				previousValue = currentValue;
			var result = previousValue != currentValue && currentValue != "";
			previousValues[key] = currentValue;
			return result;
		}

		public static void IfChangedPersonasAdd(string key, string currentValue, string text, int priority, bool useFirstValue = false)
		{
			currentValue ??= "";
			if (previousValues.TryGetValue(key, out var previousValue) == false)
				previousValue = useFirstValue ? null : currentValue;
			if (previousValue != currentValue && currentValue != "")
				Personas.Add(text.Replace("{VALUE}", currentValue), priority);
			previousValues[key] = currentValue;
		}
	}
}