using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Verse.Sound;
using System.Threading.Tasks;
using System.Text;
using System;
using System.Xml.Linq;

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

    public static class TTS
    {
        public static AudioSource audioSource = null;

        public static AudioSource GetAudioSource()
        {
            if (audioSource == null)
            {
                var gameObject = new GameObject("HarmonyOneShotSourcesWorldContainer");
                gameObject.transform.position = Vector3.zero;
                var gameObject2 = new GameObject("HarmonyOneShotSource");
                gameObject2.transform.parent = gameObject.transform;
                gameObject2.transform.localPosition = Vector3.zero;
                audioSource = AudioSourceMaker.NewAudioSourceOn(gameObject2);
            }
            return audioSource;
        }

        public static async Task<T> DispatchFormPost<T>(string path, WWWForm form)
        {
            using var request = UnityWebRequest.Post(path, form);
            var asyncOperation = request.SendWebRequest();
            while (!asyncOperation.isDone)
                await Task.Yield();
            return JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
        }

        public static async Task<AudioClip> AudioClipFromAzure(string path, string text, string voice, string style)
        {
            var speakElement = new XElement("speak", new XAttribute("version", "1.0"), new XAttribute(XNamespace.Xml + "lang", "en-US"));
            var voiceElement = new XElement("voice", new XAttribute(XNamespace.Xml + "lang", "en-US"), new XAttribute("name", voice), new XAttribute("style", style), new XText(text));
            speakElement.Add(voiceElement);
            using var request = UnityWebRequest.Put(path, Encoding.Default.GetBytes(speakElement.ToString()));
            using var downloadHandlerAudioClip = new DownloadHandlerAudioClip(path, AudioType.MPEG);
            request.method = "POST";
            request.SetRequestHeader("Ocp-Apim-Subscription-Key", Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY"));
            request.SetRequestHeader("Content-Type", "application/ssml+xml");
            request.SetRequestHeader("X-Microsoft-OutputFormat", "audio-16khz-64kbitrate-mono-mp3");
            request.downloadHandler = downloadHandlerAudioClip;
            var asyncOperation = request.SendWebRequest();
            while (!asyncOperation.isDone)
                await Task.Yield();
            return downloadHandlerAudioClip.audioClip;
        }

        public static async Task<AudioClip> DownloadAudioClip(string path)
        {
            using var request = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.MPEG);
            var asyncOperation = request.SendWebRequest();
            while (!asyncOperation.isDone)
                await Task.Yield();
            return DownloadHandlerAudioClip.GetContent(request);
        }

        public static async Task PlayTTSMP3(string text, string voice = "Salli", string source = "ttsmp3")
        {
            var form = new WWWForm();
            form.AddField("msg", text);
            form.AddField("lang", voice);
            form.AddField("source", source);
            var response = await DispatchFormPost<TTSResponse>("https://ttsmp3.com/makemp3_new.php", form);
            var audioClip = await DownloadAudioClip(response.URL);
            GetAudioSource().PlayOneShot(audioClip);
        }

        public static async Task PlayAzure(string text, string voice = "en-US-TonyNeural", string style = "Default")
        {
            var region = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");
            var audioClip = await AudioClipFromAzure($"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1", text, voice, style);
            GetAudioSource().PlayOneShot(audioClip);
            await Task.Delay((int)(audioClip.length * 1000));
        }
    }
}