using System;
using System.Collections.Concurrent;
using System.Linq;
using Verse;

namespace RimGPT
{
	public static class Personas
	{
		public static readonly int maxQueueSize = 3;
		public static bool IsAudioQueueFull => speechQueue.Count >= maxQueueSize;
		public static ConcurrentQueue<SpeechJob> speechQueue = new();
		public static string currentText = "";

		static Personas()
		{
			Tools.SafeLoop(() =>
			{
				foreach (var persona in RimGPTMod.Settings.personas)
					persona.Periodic();
			},
			1000);

			Tools.SafeLoop(async () =>
			{
				if (speechQueue.TryPeek(out var job) == false || job.completed == false)
				{
					//FileLog.Log($"...{(job == null ? "empty" : "job")}");
					await Tools.SafeWait(200);
					return true;
				}
				_ = speechQueue.TryDequeue(out job);
				//FileLog.Log($"SPEECH QUEUE -1 => {speechQueue.Count} -> play {job.persona?.name ?? "null"}: {job.audioClip != null}/{job.completed}");
				await job.Play(false);
				return false;
			});
		}

		public static void Add(string text, int priority, Persona speaker = null)
		{
			var phrase = new Phrase(speaker, text, priority);
			Logger.Message(phrase.ToString());

			foreach (var persona in RimGPTMod.Settings.personas.Where(p => p != speaker))
				persona.AddPhrase(phrase);
		}

		public static void RemoveSpeechDelayForPersona(Persona persona)
		{
			lock (speechQueue)
			{
				foreach (var job in speechQueue)
					if (job.persona == persona && job.doneCallback != null)
					{
						var callback = job.doneCallback;
						job.doneCallback = null;
						callback();
					}
			}
		}

		public static void Reset(string reason)
		{
			lock (speechQueue)
			{
				speechQueue.Clear();
				foreach (var persona in RimGPTMod.Settings.personas)
					persona.Reset(reason);
			}
		}

		public static void CreateSpeechJob(Persona persona, Phrase[] phrases, Action<string> errorCallback, Action doneCallback)
		{
			lock (speechQueue)
			{
				if (IsAudioQueueFull == false && RimGPTMod.Settings.azureSpeechKey != "" && RimGPTMod.Settings.azureSpeechRegion != "")
				{
					var filteredPhrases = phrases.Where(obs => obs.persona?.name != persona.name).ToArray();
					var job = new SpeechJob(persona, filteredPhrases, errorCallback, doneCallback);
					speechQueue.Enqueue(job);
					//FileLog.Log($"SPEECH QUEUE +1 => {speechQueue.Count}");
				}
				else
					doneCallback();
			}
		}

		public static void UpdateVoiceInformation()
		{
			TTS.voices = new Voice[0];
			if (RimGPTMod.Settings.azureSpeechKey == "" || RimGPTMod.Settings.azureSpeechRegion == "")
				return;
			
			Tools.SafeAsync(async () =>
			{
				TTS.voices = await TTS.DispatchFormPost<Voice[]>($"{TTS.APIURL}/voices/list", null, true, null);
				foreach (var persona in RimGPTMod.Settings.personas)
				{
					var voiceLanguage = Tools.VoiceLanguage(persona);
					var currentVoice = Voice.From(persona.azureVoice);
					if (currentVoice != null && currentVoice.LocaleName.Contains(voiceLanguage) == false)
					{
						currentVoice = TTS.voices
							.Where(voice => voice.LocaleName.Contains(voiceLanguage))
							.OrderBy(voice => voice.DisplayName)
							.FirstOrDefault();
						persona.azureVoice = currentVoice?.ShortName ?? "";
						persona.azureVoiceStyle = "default";
					}
				}
			});
		}
	}
}