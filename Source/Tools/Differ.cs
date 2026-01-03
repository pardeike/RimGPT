using System.Collections.Concurrent;

namespace RimGPT
{
	public static class Differ
	{
		static readonly ConcurrentDictionary<string, string> previousValues = new();

		public static bool Changed(string key, string currentValue)
		{
			currentValue ??= "";
			var previousValue = previousValues.GetOrAdd(key, currentValue);
			var result = previousValue != currentValue && currentValue != "";
			previousValues[key] = currentValue;
			return result;
		}

		public static void IfChangedPersonasAdd(string key, string currentValue, string text, int priority, bool useFirstValue = false)
		{
			currentValue ??= "";
			var previousValue = previousValues.GetOrAdd(key, useFirstValue ? null : currentValue);
			if (previousValue != currentValue && currentValue != "")
				Personas.Add(text.Replace("{VALUE}", currentValue), priority);
			previousValues[key] = currentValue;
		}
	}
}
