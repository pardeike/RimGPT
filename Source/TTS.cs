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
			if (shortName == null || shortName == "")
				return null;
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

	public class TTS
	{
		public static string APIURL => $"https://{RimGPTMod.Settings.azureSpeechRegion}.tts.speech.microsoft.com/cognitiveservices";

		public static Voice[] voices = new Voice[0];

		public static AudioSource audioSource = null;

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
				while (!asyncOperation.isDone && RimGPTMod.Running)
					await Task.Delay(200);
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
				Logger.Error($"Azure malformed output: {response}");
			}
			return default;
		}

		public static async Task<AudioClip> AudioClipFromAzure(Persona persona, string path, string text, Action<string> errorCallback)
		{
			var voice = persona.azureVoice;
			var style = persona.azureVoiceStyle;
			var styledegree = persona.azureVoiceStyleDegree;
			var rate = persona.speechRate;
			var pitch = persona.speechPitch;
			var xml = await new Ssml().Say(text).WithProsody(rate, pitch).AsVoice(voice, style, styledegree).ToStringAsync();
			if (Tools.DEBUG)
				Logger.Warning($"[{voice}] [{style}] [{styledegree}] [{rate}] [{pitch}] => {xml}");
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
				while (!asyncOperation.isDone && RimGPTMod.Running)
					await Task.Delay(200);
				RimGPTMod.Settings.charactersSentAzure += text.Length;
			}
			catch (Exception exception)
			{
				var error = $"Error communicating with Azure: {exception}";
				errorCallback?.Invoke(error);
				return default;
			}
			var code = request.responseCode;
			if (Tools.DEBUG)
				Logger.Warning($"Azure => {code} {request.error}");
			if (code >= 300)
			{
				var error = $"Got {code} response from Azure: {request.error}";
				errorCallback?.Invoke(error);
				return default;
			}
			return await Main.Perform(() =>
			{
				var audioClip = downloadHandlerAudioClip.audioClip;
				// SaveAudioClip.Save("/Users/ap/Desktop/test.wav", audioClip);
				return audioClip;
			});
		}

		public static async Task<AudioClip> DownloadAudioClip(string path, Action<string> errorCallback)
		{
			using var request = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.MPEG);
			try
			{
				var asyncOperation = request.SendWebRequest();
				while (!asyncOperation.isDone && RimGPTMod.Running)
					await Task.Delay(200);
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

		public static void TestKey(Persona persona, Action callback)
		{
			Tools.SafeAsync(async () =>
			{
				var text = "This is a test message";
				string error = null;
				if (RimGPTMod.Settings.chatGPTKey != "")
				{
					var prompt = "Say something random.";
					if (persona.personalityLanguage != "-")
						prompt += $" Your response must be in {persona.personalityLanguage}.";
					var dummyAI = new AI();
					var result = await dummyAI.SimplePrompt(prompt);
					text = result.Item1;
					error = result.Item2;
				}
				if (text != null)
				{
					var audioClip = await AudioClipFromAzure(persona, $"{APIURL}/v1", text, e => error = e);
					if (audioClip != null)
					{
						var source = GetAudioSource();
						source.Stop();
						source.clip = audioClip;
						source.volume = RimGPTMod.Settings.speechVolume;
						source.Play();
					}
				}
				if (error != null)
					LongEventHandler.ExecuteWhenFinished(() =>
						{
							var dialog = new Dialog_MessageBox(error, null, null, null, null, null, false, callback, callback);
							Find.WindowStack.Add(dialog);
						});
				else
					callback?.Invoke();
			});
		}
	}
}