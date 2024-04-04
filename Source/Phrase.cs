using System;

namespace RimGPT
{
	public struct Phrase(Persona persona, string text, int priority = 0) : IEquatable<Phrase>
	{
		public Persona persona = persona;
		public string text = Tools.tagRemover.Replace(text, "$1");
		public int priority = priority;

		public override readonly int GetHashCode() => text.GetHashCode();
		public readonly bool Equals(Phrase other) => text == other.text;

		public override readonly string ToString() => $"PRIO-{priority} {text}{(persona != null ? $" [by {persona.name}]" : "")}";
	}
}