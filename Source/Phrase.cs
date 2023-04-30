using HarmonyLib;
using System;

namespace RimGPT
{
	public struct Phrase : IEquatable<Phrase>
	{
		public Persona persona;
		public string text;
		public int priority;

		public Phrase(Persona persona, string text, int priority = 0)
		{
			this.persona = persona;
			this.text = Tools.tagRemover.Replace(text, "$1");
			this.priority = priority;
		}

		public bool Equals(Phrase other) => text == other.text;

		public override string ToString() => $"PRIO-{priority} {text}{(persona != null ? $" [by {persona.name}]" : "")}";
	}
}