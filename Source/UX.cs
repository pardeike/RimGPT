using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimGPT
{
	public static class UX
	{
        public static void SmallLabel(this Listing_Standard list, string text)
        {
            var rect = list.GetRect(20f);
            var anchor = Text.Anchor;
            var font = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(rect, text);
            Text.Anchor = anchor;
            Text.Font = font;
        }

        public static void Label(this Listing_Standard list, string text, string hexColor)
        {
            var tagged = new TaggedString($"<color=#{hexColor}>{text}</color>");
            _ = list.Label(tagged);
            list.Gap(6f);
        }

        public static void TextField(this Listing_Standard list, ref string text, string label = null, bool isPassword = false)
        {
            var rect = list.GetRect(20f);
            if (label != null)
            {
                var anchor = Text.Anchor;
                var font = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(rect, label);
                Text.Anchor = anchor;
                Text.Font = font;

                // TODO
                if (Widgets.ButtonText(rect.RightPartPixels(24), "?"))
                    Log.Warning($"HELP FOR {label}");
            }
            if (isPassword && text != "")
            {
                if (list.ButtonText("Clear"))
                    text = "";
            }
            else
               text = list.TextEntry(text);
        }

        public static void Slider(this Listing_Standard list, ref int value, int min, int max, Func<string> label)
        {
            float f = value;
            var h = HorizontalSlider(list.GetRect(22f), ref f, min, max, label == null ? null : label(), 1f);
            value = (int)f;
            list.Gap(h);
        }

        public static void Slider(this Listing_Standard list, ref float value, float min, float max, Func<string> label, float roundTo = -1f)
        {
            var rect = list.GetRect(22f);
            var h = HorizontalSlider(rect, ref value, min, max, label == null ? null : label(), roundTo);
            list.Gap(h);
        }

        public static float HorizontalSlider(Rect rect, ref float value, float leftValue, float rightValue, string label, float roundTo = -1f)
        {
            if (label != null)
            {
                var anchor = Text.Anchor;
                var font = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(rect, label);
                Text.Anchor = anchor;
                Text.Font = font;

                rect.y += 18f;
            }
            value = GUI.HorizontalSlider(rect, value, leftValue, rightValue);
            if (roundTo > 0f)
                value = Mathf.RoundToInt(value / roundTo) * roundTo;
            return 4f + label != null ? 18f : 0f;
        }

        public static string Float(this int value, int decimals, string unit = null)
            => value.ToString("F" + decimals) + (unit == null ? "" : $" {unit}");

        public static int Milliseconds(this float f) => Mathf.FloorToInt(f * 1000);

        public static string ToPercentage(this float value, bool addPlus = true)
        {
            var percentageValue = value * 100;
            if (addPlus) return $"{percentageValue:+0.##;-0.##;0}%";
            return $"{percentageValue:0.##;-0.##;0}%";
        }

        public static void Voices(this Listing_Standard list)
        {
            var rect = list.GetRect(22f);
            var currentVoice = Voice.From(RimGPTMod.Settings.azureVoice);
            if (Widgets.ButtonText(rect, currentVoice?.DisplayName ?? ""))
            {
                var options = new List<FloatMenuOption>();
                var voices = TTS.voices.Where(voice => voice.LocaleName.Contains(Tools.Language)).OrderBy(voice => voice.DisplayName);
                foreach (var voice in voices)
                {
                    var hasStyles = voice.StyleList.NullOrEmpty() == false;
                    var floatMenuOption = new FloatMenuOption(voice.DisplayName + (hasStyles ? " *" : ""), () =>
                    {
                        RimGPTMod.Settings.azureVoice = voice.ShortName;
                        RimGPTMod.Settings.azureVoiceStyle = VoiceStyle.Values[0].Value;
                    });
                    var tooltip = $"{voice.Gender}, {voice.WordsPerMinute} Words/min, {voice.LocaleName}";
                    floatMenuOption.tooltip = new TipSignal?(tooltip);
                    options.Add(floatMenuOption);
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        public static bool HasVoiceStyles()
        {
            var currentVoice = Voice.From(RimGPTMod.Settings.azureVoice);
            var availableStyles = currentVoice?.StyleList;
            return availableStyles.NullOrEmpty() == false;
        }

        public static void VoiceStyles(this Listing_Standard list)
        {
            var rect = list.GetRect(22f);
            var currentVoice = Voice.From(RimGPTMod.Settings.azureVoice);
            var availableStyles = currentVoice?.StyleList;
            if (availableStyles.NullOrEmpty()) availableStyles = new[] { "default" };
            var currentStyle = VoiceStyle.From(RimGPTMod.Settings.azureVoiceStyle);
            if (Widgets.ButtonText(rect, currentStyle?.Name ?? ""))
            {
                var options = new List<FloatMenuOption>();
                foreach (var styleName in availableStyles)
                {
                    var style = VoiceStyle.From(styleName);
                    var floatMenuOption = new FloatMenuOption(style.Name, () => RimGPTMod.Settings.azureVoiceStyle = style.Value);
                    if (style.Tooltip != null)
                        floatMenuOption.tooltip = new TipSignal?(style.Tooltip);
                    options.Add(floatMenuOption);
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
    }
}