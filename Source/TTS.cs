using Kevsoft.Ssml;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Verse.Sound;

namespace RimGPT
{
	public struct TTSResponse
	{
		public int Error;
		public string Speaker;
		public int Cached;
		public string Text;
		public string tasktype;
		public string URL;
		public string MP3;
	}

	public class Voice
	{
		public string Name;
		public string DisplayName;
		public string LocalName;
		public string ShortName;
		public string Gender;
		public string Locale;
		public string LocaleName;
		public string[] StyleList;
		public string SampleRateHertz;
		public string VoiceType;
		public string Status;
		public string WordsPerMinute;

		public static Voice From(string shortName)
		{
			if (shortName == null || shortName == "") return null;
			return TTS.voices?.FirstOrDefault(v => v.ShortName == shortName);
		}
	}

	public class VoiceStyle
	{
		private VoiceStyle(string name, string value, string tooltip) { Name = name; Value = value; Tooltip = tooltip; }

		public string Name { get; private set; }
		public string Value { get; private set; }
		public string Tooltip { get; private set; }

		public static readonly VoiceStyle[] Values = new VoiceStyle[]
		{
			new VoiceStyle("Default", "default", null),
			new VoiceStyle("Advertisement Upbeat", "advertisement_upbeat", "Expresses an excited and high-energy tone for promoting a product or service"),
			new VoiceStyle("Affectionate", "affectionate", "Expresses a warm and affectionate tone, with higher pitch and vocal energy. The speaker is in a state of attracting the attention of the listener. The personality of the speaker is often endearing in nature"),
			new VoiceStyle("Angry", "angry", "Expresses an angry and annoyed tone"),
			new VoiceStyle("Assistant", "assistant", "Expresses a warm and relaxed tone for digital assistants"),
			new VoiceStyle("Calm", "calm", "Expresses a cool, collected, and composed attitude when speaking. Tone, pitch, and prosody are more uniform compared to other types of speech"),
			new VoiceStyle("Chat", "chat", "Expresses a casual and relaxed tone"),
			new VoiceStyle("Cheerful", "cheerful", "Expresses a positive and happy tone"),
			new VoiceStyle("Customer Service", "customerservice", "Expresses a friendly and helpful tone for customer support"),
			new VoiceStyle("Depressed", "depressed", "Expresses a melancholic and despondent tone with lower pitch and energy"),
			new VoiceStyle("Disgruntled", "disgruntled", "Expresses a disdainful and complaining tone. Speech of this emotion displays displeasure and contempt"),
			new VoiceStyle("Documentary Narration", "documentary-narration", "Narrates documentaries in a relaxed, interested, and informative style suitable for dubbing documentaries, expert commentary, and similar content"),
			new VoiceStyle("Embarrassed", "embarrassed", "Expresses an uncertain and hesitant tone when the speaker is feeling uncomfortable"),
			new VoiceStyle("Empathetic", "empathetic", "Expresses a sense of caring and understanding"),
			new VoiceStyle("Envious", "envious", "Expresses a tone of admiration when you desire something that someone else has"),
			new VoiceStyle("Excited", "excited", "Expresses an upbeat and hopeful tone. It sounds like something great is happening and the speaker is really happy about that"),
			new VoiceStyle("Fearful", "fearful", "Expresses a scared and nervous tone, with higher pitch, higher vocal energy, and faster rate. The speaker is in a state of tension and unease"),
			new VoiceStyle("Friendly", "friendly", "Expresses a pleasant, inviting, and warm tone. It sounds sincere and caring"),
			new VoiceStyle("Gentle", "gentle", "Expresses a mild, polite, and pleasant tone, with lower pitch and vocal energy"),
			new VoiceStyle("Hopeful", "hopeful", "Expresses a warm and yearning tone. It sounds like something good will happen to the speaker"),
			new VoiceStyle("Lyrical", "lyrical", "Expresses emotions in a melodic and sentimental way"),
			new VoiceStyle("Narration Professional", "narration-professional", "Expresses a professional, objective tone for content reading"),
			new VoiceStyle("Narration Relaxed", "narration-relaxed", "Express a soothing and melodious tone for content reading"),
			new VoiceStyle("Newscast", "newscast", "Expresses a formal and professional tone for narrating news"),
			new VoiceStyle("Newscast Casual", "newscast-casual", "Expresses a versatile and casual tone for general news delivery"),
			new VoiceStyle("Newscast Formal", "newscast-formal", "Expresses a formal, confident, and authoritative tone for news delivery"),
			new VoiceStyle("Poetry Reading", "poetry-reading", "Expresses an emotional and rhythmic tone while reading a poem"),
			new VoiceStyle("Sad", "sad", "Expresses a sorrowful tone"),
			new VoiceStyle("Serious", "serious", "Expresses a strict and commanding tone. Speaker often sounds stiffer and much less relaxed with firm cadence"),
			new VoiceStyle("Shouting", "shouting", "Speaks like from a far distant or outside and to make self be clearly heard"),
			new VoiceStyle("Sports Commentary", "sports_commentary", "Expresses a relaxed and interesting tone for broadcasting a sports event"),
			new VoiceStyle("Sports Commentary Excited", "sports_commentary_excited", "Expresses an intensive and energetic tone for broadcasting exciting moments in a sports event"),
			new VoiceStyle("Whispering", "whispering", "Speaks very softly and make a quiet and gentle sound"),
			new VoiceStyle("Terrified", "terrified", "Expresses a very scared tone, with faster pace and a shakier voice. It sounds like the speaker is in an unsteady and frantic status"),
			new VoiceStyle("Unfriendly", "unfriendly", "Expresses a cold and indifferent tone")
		};

		public static VoiceStyle From(string shortName) => Values.FirstOrDefault(s => s.Value == shortName);
	}

	public static class TTS
	{
		public static bool debug = false;
		public static AudioSource audioSource = null;
		public static Voice[] voices = new Voice[0];

		public static AudioSource GetAudioSource()
		{
			if (audioSource == null)
			{
				var gameObject = new GameObject("HarmonyOneShotSourcesWorldContainer");
				UnityEngine.Object.DontDestroyOnLoad(gameObject);
				gameObject.transform.position = Vector3.zero;
				var gameObject2 = new GameObject("HarmonyOneShotSource");
				gameObject2.transform.parent = gameObject.transform;
				gameObject2.transform.localPosition = Vector3.zero;
				audioSource = AudioSourceMaker.NewAudioSourceOn(gameObject2);
				audioSource.spatialBlend = 0f;
				audioSource.rolloffMode = AudioRolloffMode.Linear;
				audioSource.minDistance = 100000;
				audioSource.bypassEffects = true;
				audioSource.bypassListenerEffects = true;
				audioSource.bypassReverbZones = true;
				audioSource.ignoreListenerPause = true;
				audioSource.ignoreListenerVolume = true;
				audioSource.volume = 1;
			}
			return audioSource;
		}

		public static async Task<T> DispatchFormPost<T>(string path, WWWForm form, bool addSubscriptionKey, Action<string> errorCallback)
		{
			using var request = form != null ? UnityWebRequest.Post(path, form) : UnityWebRequest.Get(path);
			if (addSubscriptionKey)
				request.SetRequestHeader("Ocp-Apim-Subscription-Key", RimGPTMod.Settings.azureSpeechKey);
			try
			{
				var asyncOperation = request.SendWebRequest();
				while (!asyncOperation.isDone)
					await Task.Yield();
			}
			catch (Exception exception)
			{
				var error = $"Error communicating with Azure: {exception}";
				errorCallback?.Invoke(error);
				return default;
			}
			var response = request.downloadHandler.text;
			var code = request.responseCode;
			if (code >= 300)
			{
				var error = $"Got {code} response from OpenAI: {response}";
				errorCallback?.Invoke(error);
				return default;
			}
			try
			{
				return JsonConvert.DeserializeObject<T>(response);
			}
			catch (Exception)
			{
				Log.Error($"Azure malformed output: {response}");
			}
			return default;
		}

		public static async Task<AudioClip> AudioClipFromAzure(string path, string text, Action<string> errorCallback)
		{
			var voice = RimGPTMod.Settings.azureVoice;
			var style = RimGPTMod.Settings.azureVoiceStyle;
			var styledegree = RimGPTMod.Settings.azureVoiceStyleDegree;
			var rate = RimGPTMod.Settings.speechRate;
			var pitch = RimGPTMod.Settings.speechPitch;
			var xml = await new Ssml().Say(text).WithProsody(rate, pitch).AsVoice(voice, style, styledegree).ToStringAsync();
			if (debug)
				Log.Warning($"[{voice}] [{style}] [{styledegree}] [{rate}] [{pitch}] => {xml}");
			using var request = UnityWebRequest.Put(path, Encoding.Default.GetBytes(xml));
			using var downloadHandlerAudioClip = new DownloadHandlerAudioClip(path, AudioType.MPEG);
			request.method = "POST";
			request.SetRequestHeader("Ocp-Apim-Subscription-Key", RimGPTMod.Settings.azureSpeechKey);
			request.SetRequestHeader("Content-Type", "application/ssml+xml");
			request.SetRequestHeader("X-Microsoft-OutputFormat", "audio-16khz-64kbitrate-mono-mp3");
			request.downloadHandler = downloadHandlerAudioClip;
			try
			{
				var asyncOperation = request.SendWebRequest();
				while (!asyncOperation.isDone)
					await Task.Yield();
			}
			catch (Exception exception)
			{
				var error = $"Error communicating with Azure: {exception}";
				errorCallback?.Invoke(error);
				return default;
			}
			var code = request.responseCode;
			if (debug)
				Log.Warning($"Azure => {code} {request.error}");
			if (code >= 300)
			{
				var response = request.downloadHandler.text;
				var error = $"Got {code} {request.error} response from Azure: {response}";
				errorCallback?.Invoke(error);
				return default;
			}
			if (debug)
				Log.Warning($"Azure => {downloadHandlerAudioClip.audioClip?.length} seconds");
			// SaveAudioClip.Save("/Users/ap/Desktop/test.wav", downloadHandlerAudioClip.audioClip);
			return downloadHandlerAudioClip.audioClip;
		}

		public static async Task<AudioClip> DownloadAudioClip(string path, Action<string> errorCallback)
		{
			using var request = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.MPEG);
			try
			{
				var asyncOperation = request.SendWebRequest();
				while (!asyncOperation.isDone)
					await Task.Yield();
			}
			catch (Exception exception)
			{
				var error = $"Error communicating with Azure: {exception}";
				errorCallback?.Invoke(error);
				return default;
			}
			var response = request.downloadHandler.text;
			var code = request.responseCode;
			if (code >= 300)
			{
				var error = $"Got {code} response from Azure: {response}";
				errorCallback?.Invoke(error);
				return default;
			}
			return DownloadHandlerAudioClip.GetContent(request);
		}

		//public static async Task PlayTTSMP3(string text, string voice = "Salli", string source = "ttsmp3")
		//{
		//    var form = new WWWForm();
		//    form.AddField("msg", text);
		//    form.AddField("lang", voice);
		//    form.AddField("source", source);
		//    var response = await DispatchFormPost<TTSResponse>("https://ttsmp3.com/makemp3_new.php", form);
		//    var audioClip = await DownloadAudioClip(response.URL);
		//    GetAudioSource().PlayOneShot(audioClip);
		//}

		public static async Task PlayAzure(string text, bool delay, Action<string> errorCallback)
		{
			var url = $"https://{RimGPTMod.Settings.azureSpeechRegion}.tts.speech.microsoft.com/cognitiveservices/v1";
			var audioClip = await AudioClipFromAzure(url, text, errorCallback);
			GetAudioSource().PlayOneShot(audioClip, RimGPTMod.Settings.speechVolume);
			if (delay)
				await Task.Delay((int)(audioClip.length * 1000));
		}

		public static void TestKey(Action successCallback)
		{
			_ = Task.Run(async () =>
			{
				var text = "This is a test message";
				string error = null;
				if (RimGPTMod.Settings.chatGPTKey != "")
				{
					var result = await AI.SimplePrompt("Tell me something random in 15 words or less.");
					text = result.Item1;
					error = result.Item2;
				}
				if (text != null)
					await PlayAzure(text, false, e => error = e);
				if (error != null)
					LongEventHandler.ExecuteWhenFinished(() =>
						{
							var dialog = new Dialog_MessageBox(error);
							Find.WindowStack.Add(dialog);
						});
				else
					successCallback?.Invoke();
			});
		}

		public static void LoadVoiceInformation()
		{
			voices = new Voice[0];
			if (RimGPTMod.Settings.azureSpeechKey != "" && RimGPTMod.Settings.azureSpeechRegion != "")
			{
				var url = $"https://{RimGPTMod.Settings.azureSpeechRegion}.tts.speech.microsoft.com/cognitiveservices/voices/list";
				_ = Task.Run(async () =>
				{
					voices = await DispatchFormPost<Voice[]>(url, null, true, null);

					var currentVoice = Voice.From(RimGPTMod.Settings.azureVoice);
					if (currentVoice != null && currentVoice.LocaleName.Contains(Tools.Language) == false)
					{
						currentVoice = voices.Where(voice => voice.LocaleName.Contains(Tools.Language)).OrderBy(voice => voice.DisplayName).FirstOrDefault();
						RimGPTMod.Settings.azureVoice = currentVoice?.ShortName ?? "";
						RimGPTMod.Settings.azureVoiceStyle = "default";
					}
				});
			}
		}
	}
}