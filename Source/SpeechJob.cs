using HarmonyLib;
using System;
using System.Threading.Tasks;
using UnityEngine;
using Verse.AI;

namespace RimGPT
{
	public class SpeechJob
	{
		public Persona persona = null;
		public bool waitForAudio;
		public string spokenText = null;
		public AudioClip audioClip = null;
		public Action doneCallback = null;
		public bool completed = false;

		public SpeechJob(Persona persona, Phrase[] phrases, Action<string> errorCallback, Action doneCallback)
		{
			this.persona = persona;
			waitForAudio = true;
			this.doneCallback = doneCallback;

			Tools.SafeAsync(async () =>
			{
				//FileLog.Log($"{persona?.name ?? "null"} ai request: {phrases.Join(p => p.text, "|")}");
				spokenText = await persona.ai.Evaluate(persona, phrases);
				//FileLog.Log($"{persona?.name ?? "null"} ai reponse: {spokenText}");
				if (spokenText == null || spokenText == "")
				{
					doneCallback();
					completed = true;
					return;
				}
				audioClip = await TTS.AudioClipFromAzure(persona, TTS.APIURL, spokenText, errorCallback);
				doneCallback();
				completed = true;
			});
		}

		public SpeechJob(Persona persona, string text, Action<string> errorCallback)
		{
			this.persona = persona;
			waitForAudio = false;
			doneCallback = null;

			if (RimGPTMod.Settings.azureSpeechKey == "" || RimGPTMod.Settings.azureSpeechRegion == "")
				return;

			Tools.SafeAsync(async () =>
			{
				spokenText = text;
				audioClip = await TTS.AudioClipFromAzure(persona, TTS.APIURL, spokenText, errorCallback);
				if (audioClip == null)
				{
					doneCallback();
					completed = true;
				}
			});
		}

		public async Task Play()
		{
			if (persona != null)
			{
				Personas.Add($"{persona.name} said: {spokenText}", 1, persona);
				persona.lastSpokenText = spokenText;
			}

			var showText = RimGPTMod.Settings.showAsText || RimGPTMod.Settings.azureSpeechRegion == "" || RimGPTMod.Settings.azureSpeechKey == "";
			if (showText)
				Personas.currentText = persona == null ? spokenText : $"{persona.name}: {spokenText}";

			if (audioClip != null)
			{
				var length = await Main.Perform(() =>
				{
					TTS.GetAudioSource().PlayOneShot(audioClip, RimGPTMod.Settings.speechVolume);
					return audioClip.length;
				});
				if (waitForAudio)
					await Tools.SafeWait((int)(length * 1000));
				//FileLog.Log($"{persona.name}: play done");
			}
			else if (showText)
			{
				var ms = 1000 * (int)(spokenText.Length / 20f);
				await Tools.SafeWait(ms);
			}

			doneCallback?.Invoke();
			completed = true;

			Personas.currentText = "";
		}
	}
}