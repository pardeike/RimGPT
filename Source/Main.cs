using Brrainz;
using HarmonyLib;
using System.Collections.Concurrent;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using System.Collections;

namespace RimGPT
{
	[HarmonyPatch(typeof(Current), nameof(Current.Notify_LoadedSceneChanged))]
	[StaticConstructorOnStartup]
	public static class Main
	{
		static readonly ConcurrentQueue<Action> actions = new();

		static Main()
		{
			Postfix();
		}

		public static void Postfix()
		{
			if (GenScene.InEntryScene)
				_ = Current.Root_Entry.StartCoroutine(Process());
			if (GenScene.InPlayScene)
				_ = Current.Root_Play.StartCoroutine(Process());
		}

		static IEnumerator Process()
		{
			while (true)
			{
				yield return null;
				if (actions.TryDequeue(out var action) == false)
					continue;
				action();
			}
		}

		public static async Task Perform(Action action)
		{
			var working = true;
			actions.Enqueue(() => { action(); working = false; });
			while (working)
				await Task.Yield();
		}

		public static async Task<T> Perform<T>(Func<T> action)
		{
			T result = default;
			var working = true;
			actions.Enqueue(() => { result = action(); working = false; });
			while (working)
				await Task.Yield();
			return result;
		}
	}

	public class RimGPTMod : Mod
	{
		public static CancellationTokenSource onQuit = new();
		public static RimGPTSettings Settings;
		public static Mod self;

		public RimGPTMod(ModContentPack content) : base(content)
		{
			self = this;
			Settings = GetSettings<RimGPTSettings>();

			var harmony = new Harmony("net.pardeike.rimworld.mod.RimGPT");
			harmony.PatchAll();
			CrossPromotion.Install(76561197973010050);

			LongEventHandler.ExecuteWhenFinished(() =>
			{
				Personas.UpdateVoiceInformation();
				if (Settings.IsConfigured)
					Personas.Add("Player has launched Rimworld and is on the start screen", 0);
			});

			Application.wantsToQuit += () =>
			{
				onQuit.Cancel();
				return true;
			};
		}

		public static bool Running => onQuit.IsCancellationRequested == false;

		public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
		public override string SettingsCategory() => "RimGPT";
	}
}