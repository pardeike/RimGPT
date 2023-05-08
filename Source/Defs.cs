using RimWorld;
using UnityEngine;
using Verse;

namespace RimGPT
{
	[DefOf]
	public static class Defs
	{
		public static ThingDef Wall;
		public static WorkTypeDef Cleaning;
		public static KeyBindingDef Command_OpenRimGPT;
	}

	[StaticConstructorOnStartup]
	public static class Graphics
	{
		public static readonly Texture2D[] ButtonAdd = new[] { ContentFinder<Texture2D>.Get("ButtonAdd0", true), ContentFinder<Texture2D>.Get("ButtonAdd1", true) };
		public static readonly Texture2D[] ButtonDel = new[] { ContentFinder<Texture2D>.Get("ButtonDel0", true), ContentFinder<Texture2D>.Get("ButtonDel1", true) };
		public static readonly Texture2D[] ButtonDup = new[] { ContentFinder<Texture2D>.Get("ButtonDup0", true), ContentFinder<Texture2D>.Get("ButtonDup1", true) };
	}
}