using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using Verse;

namespace RimGPT
{
    public class RimGPTSettings : ModSettings
    {
        public string chatGPTKey = "";
        public string azureSpeechKey = "";
        public string azureSpeechRegion = "";
        public string azureVoice = "en-US-AriaNeural";
        public string azureVoiceStyle = "chat";
        public float azureVoiceStyleDegree = 1.5f;
        public float speechVolume = 4f;
        public float speechRate = 0.1f;
        public float speechPitch = -0.02f;
        public int phraseBatchSize = 20;
        public float phraseDelayMin = 10f;
        public float phraseDelayMax = 20f;
        public int phraseMaxWordCount = 60;
        public int historyMaxWordCount = 20;
        public int historyMaxItemCount = 10;
        public string personality = AI.defaultPersonality;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref chatGPTKey, "chatGPTKey");
            Scribe_Values.Look(ref azureSpeechKey, "azureSpeechKey");
            Scribe_Values.Look(ref azureSpeechRegion, "azureSpeechRegion");
            Scribe_Values.Look(ref azureVoice, "azureVoice", "en-US-AriaNeural");
            Scribe_Values.Look(ref azureVoiceStyle, "azureVoiceStyle", "chat");
            Scribe_Values.Look(ref azureVoiceStyleDegree, "azureVoiceStyleDegree", 1.5f);
            Scribe_Values.Look(ref speechVolume, "speechVolume", 4f);
            Scribe_Values.Look(ref speechRate, "speechRate", 0.1f);
            Scribe_Values.Look(ref speechPitch, "speechPitch", -0.02f);
            Scribe_Values.Look(ref phraseBatchSize, "phraseBatchSize", 20);
            Scribe_Values.Look(ref phraseDelayMin, "phraseDelayMin", 10f);
            Scribe_Values.Look(ref phraseDelayMax, "phraseDelayMax", 20f);
            Scribe_Values.Look(ref phraseMaxWordCount, "phraseMaxWordCount", 60);
            Scribe_Values.Look(ref historyMaxWordCount, "historyMaxWordCount", 20);
            Scribe_Values.Look(ref historyMaxItemCount, "historyMaxItemCount", 10);
            Scribe_Values.Look(ref personality, "personality", AI.defaultPersonality);
        }

        public bool IsConfigured =>
            chatGPTKey?.Length > 0 && azureSpeechKey?.Length > 0 && azureSpeechRegion?.Length > 0;

        public string CurrentStyle
        {
            get
            {
                var currentStyle = VoiceStyle.From(RimGPTMod.Settings.azureVoiceStyle);
                var value = currentStyle?.Value;
                if (value == null || value == "default" || value == "chat" || value.Contains("-") || value.Contains("_"))
                    return "funny";
                return value;
            }
        }

        public void DoWindowContents(Rect inRect)
        {
            string prevKey;

            var list = new Listing_Standard { ColumnWidth = inRect.width / 2f };
            list.Begin(inRect);

            list.Label("OpenAI - ChatGPT", "FFFF00");
            prevKey = chatGPTKey;
            list.TextField(ref chatGPTKey, "API Key (paste only)", true);
            if (chatGPTKey != "" && chatGPTKey != prevKey)
                AI.TestKey(
                    response => LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        var dialog = new Dialog_MessageBox(response);
                        Find.WindowStack.Add(dialog);
                    })
                );

            list.Gap(16f);

            list.Label("Azure - Speech Services", "FFFF00");
            var prevRegion = azureSpeechRegion;
            list.TextField(ref azureSpeechRegion, "Region");
            if (azureSpeechRegion != prevRegion)
                TTS.LoadVoiceInformation();
            list.Gap(6f);
            prevKey = azureSpeechKey;
            list.TextField(ref azureSpeechKey, "API Key (paste only)", true);
            if (azureSpeechKey != "" && azureSpeechKey != prevKey)
                TTS.TestKey(() => TTS.LoadVoiceInformation());

            list.Gap(16f);

            list.Label("Azure - Voice", "FFFF00");
            list.Voices(); // list.TextField(ref azureVoice, "Voice");
            list.Gap(6f);
            if (UX.HasVoiceStyles())
                list.VoiceStyles(); // list.TextField(ref azureVoiceStyle, "Style");

            list.Gap(16f);

            list.Slider(ref azureVoiceStyleDegree, 0f, 2f, () => $"Style degree: {azureVoiceStyleDegree.ToPercentage(false)}", 0.01f);
            list.Slider(ref speechVolume, 0f, 10f, () => $"Speech volume: {speechVolume.ToPercentage(false)}", 0.01f);
            list.Slider(ref speechRate, -0.5f, 0.5f, () => $"Speech rate: {speechRate.ToPercentage()}", 0.01f);
            list.Slider(ref speechPitch, -0.5f, 0.5f, () => $"Speech pitch: {speechPitch.ToPercentage()}", 0.01f);
            if (list.ButtonText("Test", null, 0.2f))
                TTS.TestKey(null);

            list.NewColumn();

            list.Label("Commentary", "FFFF00");

            if (list.ButtonText("Edit personality", null, 0.4f))
                Dialog_Personality.Show();

            list.Gap(16f);

            list.Label("Sending game information");
            list.Slider(ref phraseBatchSize, 1, 100, () => $"Maximum batch size: {phraseBatchSize}");
            list.Gap(16f);
            list.Label("Delay between comments");
            list.Slider(ref phraseDelayMin, 1f, 100f, () => $"Minimum: {phraseDelayMin}");
            list.Slider(ref phraseDelayMax, 1f, 100f, () => $"Maximum: {phraseDelayMax}");
            list.Gap(16f);
            list.Label("Comments");
            list.Slider(ref phraseMaxWordCount, 1, 160, () => $"Phrase maximum words: {phraseMaxWordCount}");
            list.Gap(16f);
            list.Label("History limits");
            list.Slider(ref historyMaxWordCount, 1, 160, () => $"Words: {historyMaxWordCount}");
            list.Slider(ref historyMaxItemCount, 1, 100, () => $"Total items: {historyMaxItemCount}");

            list.Gap(16f);

            if (list.ButtonText("Restore Defaults"))
            {
                azureVoice = "en-US-AriaNeural";
                azureVoiceStyle = "chat";
                azureVoiceStyleDegree = 1.5f;
                speechVolume = 4f;
                speechRate = 0.1f;
                speechPitch = -0.02f;
                phraseBatchSize = 20;
                phraseDelayMin = 10f;
                phraseDelayMax = 20f;
                phraseMaxWordCount = 60;
                historyMaxWordCount = 20;
                historyMaxItemCount = 10;
                personality = AI.defaultPersonality;
            }

            list.End();
        }
    }
}