using System;
using System.Threading.Tasks;
using UnityEngine;

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
		public bool isPlaying = false;
		public bool readyForNextJob = false;
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
				audioClip = await TTS.AudioClipFromAzure(persona, $"{TTS.APIURL}/v1", spokenText, errorCallback);
				doneCallback();
				completed = true;
			});
		}

		public async Task Play(bool immediately)
		{
			if (persona != null)
			{				
				persona.lastSpokenText = spokenText;
				Personas.StartNextPersona(persona);
			}

			var showText = RimGPTMod.Settings.showAsText || RimGPTMod.Settings.azureSpeechRegion == "" || RimGPTMod.Settings.azureSpeechKey == "";
			if (showText)
				Personas.currentText = persona == null ? spokenText : $"{persona.name}: {spokenText}";

			if (audioClip != null)
			{
				isPlaying = true;
				float length = 0;
				var source = TTS.GetAudioSource();
				if (immediately)
				{
					source.Stop();
					source.clip = audioClip;
					source.volume = RimGPTMod.Settings.speechVolume;
					source.Play();
					length = audioClip.length;
				}
				else
				{
					length = await Main.Perform(() =>
					{
						source.Stop();
						source.clip = audioClip;
						source.volume = RimGPTMod.Settings.speechVolume;
						source.Play();
						return audioClip.length;
					});
				}

				if (waitForAudio)
					await Tools.SafeWait((int)(length * 1000));
				//FileLog.Log($"{persona.name}: play done");
			}
			else if (showText && spokenText != null)
			{
				var ms = 1000 * (int)(spokenText.Length / 20f);
				await Tools.SafeWait(ms);
			}

			doneCallback?.Invoke();
			completed = true;
			isPlaying = false;
			Personas.currentText = "";
		}
	}
}