using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static RimGPT.Dialog_Help;

namespace RimGPT
{
    public partial class RimGPTSettings : ModSettings
    {
        public static UserApiConfig selectedConfig = null;
        public static int selectedConfigIndex = -1;

        public static float animationDuration = 0.4f;
        public static float animationProgress = 0f;
        public static bool showInstructions = false;
        public static int configRowGapHide = 24;
        public static int configRowGapShow = 12;
        public static int configRowGap = 24;
        public static float instHeightHide = 0f;
        public static float instHeightShow = 0f;
        public static float instCurHeight = 0f;
        public static bool firstLoad = true;
        public static bool activeConfig = false;

        private class SettingsLLM : Window
        {
            // Reference to the settings to allow us to modify them
            private readonly RimGPTSettings settings;

            // Constructor to pass in the settings reference
            public SettingsLLM(RimGPTSettings settings)
            {
                this.settings = settings;
                // Set properties for the window
                doCloseX = true;
            }

            // Override the method to specify the initial size of the window
            public override Vector2 InitialSize => new(800f, 680f);

            public Vector2 scrollPosition = Vector2.zero;

            public override void DoWindowContents(Rect inRect)
            {
                #region Top Section

                var topListing = new Listing_Standard();
                topListing.Begin(inRect);
                Text.Font = GameFont.Medium;
                topListing.Label("AI Provider Configuration");

                // Switch back to the small font for standard text and providerTypeOptions
                Text.Font = GameFont.Small;

                const string description = "Chose an AI Provider below to setup, you need at least one to run RimGPT.";
                const string instructions = @"1. Click on the plus button on the bottom left to add a new provider.
2. Use the first drop-down list to choose whether the provider is External (i.e., OpenAI) or Local (i.e., Ollama).
3. Choose a provider using the second drop-down list. Note: If you intend on using an OpenAI API-compatible provider that is not listed, you should choose 'OtherExternal' as your provider and enter the Base URL for the provider's API in the box below.
4. For External providers: paste in your API key. You can find instructions on how to get an API key for each of the providers listed by clicking the ? above the API Key box. Once you paste your API key, a request to the provider will be made to verify the key and retrieve the list of available models.
For Local providers: you should ensure that the Base URL matches the URL and Port number of your model. If you are using a Local model not listed that utilizes the OpenAI API standard, then choose 'LocalOpenAILike' and likewise ensure that your Base URL and Port number are correct.
5. Choose a model from the drop-down list or enter the model ID (i.e., gpt-3.5-turbo-0125) in the box. For external providers, the model drop-down should have populated with the list of available models.";

                var topCurY = topListing.curY;
                var instructBtnWidth = topListing.ColumnWidth / 4f;

                if (instHeightShow == 0f && Event.current.type == EventType.Layout)
                {
                    Text.Font = GameFont.Tiny;
                    instHeightShow = Text.CalcHeight(instructions, topListing.ColumnWidth);
                    Text.Font = GameFont.Small;
                }

                var rect = topListing.GetRect(UX.ButtonHeight);
                var anchor = Text.Anchor;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(rect, description);
                Text.Anchor = anchor;
                showInstructions = showInstructions || (firstLoad && settings.userApiConfigs.Count == 0);

                if (Widgets.ButtonText(rect.RightPartPixels(instructBtnWidth), showInstructions ? "Hide Instructions" : "Show Instructions"))
                {
                    showInstructions = !showInstructions;
                }

                topListing.GapLine(12f);

                var instRect = topListing.GetRect(instCurHeight);

                if (animationProgress > 0f)
                {
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(instRect, instructions);
                    Text.Font = GameFont.Small;
                }

                if (animationProgress > 0.2f) topListing.GapLine(Mathf.RoundToInt(12f * animationProgress));


                #endregion Top Section

                // ------------------- Bottom Section ------------------- //

                #region Bottom Section Variables

                // Width stuff
                var columnSpacing = 20f;
                var totalColumns = 2;
                var totalSpaceBetweenColumns = columnSpacing * (totalColumns - 1);

                // Column Stuff
                var availableWidth = inRect.width - totalSpaceBetweenColumns;
                var leftColumnWidth = availableWidth / 5;
                var rightColumnWidth = availableWidth - leftColumnWidth;

                // Rects
                var bottomRect = topListing.GetRect(inRect.height - topListing.CurHeight);
                var leftRect = new Rect(bottomRect.x, bottomRect.y, leftColumnWidth, bottomRect.height);
                var rightRect = new Rect(bottomRect.x + leftColumnWidth + columnSpacing, bottomRect.y, rightColumnWidth, bottomRect.height);

                // Other Variables
                var rowHeight = 24;

                #endregion Bottom Section Variables

                // ------------------- LLM Provider List ------------------- //

                #region LLM Provider List

                // LLM Label
                var leftListing = new Listing_Standard { ColumnWidth = leftColumnWidth };
                leftListing.Begin(leftRect);

                //leftListing.Gap(10f);
                leftListing.Label("FFFF00", "Available AI Providers", "", "These are all of the AI Providers you have configured.");

                // List of LLMs
                var llmListHeight = leftRect.height - UX.ButtonHeight - rowHeight - leftListing.CurHeight;
                var llmListOuterRect = leftListing.GetRect(llmListHeight);
                var llmListInnerRect = new Rect(0f, 0f, leftListing.ColumnWidth, llmListHeight);

                Widgets.DrawBoxSolid(llmListOuterRect, listBackground);
                Widgets.BeginScrollView(llmListOuterRect, ref scrollPosition, llmListInnerRect, true);

                var llmListListing = new Listing_Standard();
                llmListListing.Begin(llmListInnerRect);
                var i = 0;
                var y = 0f;
                if (settings.userApiConfigs != null)
                {
                    foreach (var config in settings.userApiConfigs)
                    {
                        if (config == null) continue;
                        LLMRow(new Rect(0, y, llmListInnerRect.width, rowHeight), config, i++);
                        if (firstLoad && config.Active) selectedConfig = config;
                        y += rowHeight;
                    }
                }
                llmListListing.End();

                Widgets.EndScrollView();

                #endregion LLM Provider List

                // ------------------- LLM Buttons ------------------- //

                #region LLM Buttons

                // Add Button
                var bRect = leftListing.GetRect(24);
                if (Widgets.ButtonImage(bRect.LeftPartPixels(24), Graphics.ButtonAdd[1]))
                {
                    Event.current.Use();

                    selectedConfig = new UserApiConfig();
                    settings.userApiConfigs.Add(selectedConfig);
                    selectedConfigIndex = settings.userApiConfigs.IndexOf(selectedConfig);
                }
                bRect.x += 32;

                // Delete Button
                if (Widgets.ButtonImage(bRect.LeftPartPixels(24), Graphics.ButtonDel[selectedConfig != null ? 1 : 0]))
                {
                    Event.current.Use();
                    _ = settings.userApiConfigs.Remove(selectedConfig);
                    var newCount = settings.userApiConfigs.Count;
                    if (newCount == 0)
                    {
                        selectedConfigIndex = -1;
                        selectedConfig = null;
                    }
                    else
                    {
                        while (newCount > 0 && selectedConfigIndex >= newCount)
                            selectedConfigIndex--;
                        selectedConfig = settings.userApiConfigs[selectedConfigIndex];
                    }
                }
                bRect.x += 32;

                // We can only have 1 of each provider right now.
                // Duplicate Button
                //var dupable = selectedConfig != null;
                //if (Widgets.ButtonImage(bRect.LeftPartPixels(24), Graphics.ButtonDup[dupable ? 1 : 0]) && dupable)
                //{
                //    Event.current.Use();
                //    var namePrefix = Regex.Replace(selectedConfig.Name, @" \d+$", "");
                //    var existingNames = settings.userApiConfigs.Select(p => p.Name).ToHashSet();
                //    for (var n = 1; true; n++)
                //    {
                //        var newName = $"{namePrefix} {n}";
                //        if (existingNames.Contains(newName) == false)
                //        {
                //            var xml = selectedConfig.ToXml();
                //            selectedConfig = new UserApiConfig();
                //            UserApiConfig.UserApiConfigFromXML(xml, selectedConfig);
                //            selectedConfig.Name = newName;
                //            settings.userApiConfigs.Add(selectedConfig);
                //            selectedConfigIndex = settings.userApiConfigs.IndexOf(selectedConfig);
                //            break;
                //        }
                //    }
                //}
                leftListing.End();

                #endregion LLM Buttons

                // ------------------- LLM Provider Configuration ------------------- //

                #region LLM Provider Dropdowns

                // LLM Label
                var rightListing = new Listing_Standard { ColumnWidth = rightColumnWidth };
                rightListing.Begin(rightRect);

                //rightListing.Gap(10f);
                rightListing.Label("FFFF00", "AI Provider Configurations", "", "Configure your AI Providers here.");

                var labelWidth = rightListing.ColumnWidth / 4f;

                // LLM Provider Configuration List
                if (selectedConfig != null) // Show only if an LLM is selected
                {
                    // Button Setup
                    var buttonAreaRect = rightListing.GetRect(UX.ButtonHeight);
                    var buttonCount = 3;
                    var buttonSpacing = 20f;
                    var buttonWidth = (rightColumnWidth - (buttonCount - 1) * buttonSpacing) / buttonCount;
                    var curY = rightListing.curY;

                    // Provider Type dropdown
                    var buttonRect = new Rect(buttonAreaRect.x, buttonAreaRect.y, buttonWidth, buttonAreaRect.height);
                    string[] providerTypeOptions = ["Local", "External"];

                    if (Widgets.ButtonText(buttonRect, providerTypeOptions.Contains(selectedConfig.Type) == false ? "Provider Type" : selectedConfig.Type))
                    {
                        ProviderTypeMenu(providerTypeOptions, type => selectedConfig.Type = type);
                    }

                    buttonRect.x += buttonWidth + buttonSpacing; // Move Rect to the right

                    // Provider dropdown
                    var providerText = providerTypeOptions.Contains(selectedConfig.Type) == false ? "" : "Choose Provider"; // Text to display if a provider has not been selected
                    var providerSelectedText = selectedConfig.Type == selectedConfig.GetProviderType() ? selectedConfig.Provider : "Choose Provider"; // Check if selected provider is different from the provider type
                    if (Widgets.ButtonText(buttonRect, selectedConfig.Provider?.Length == 0 ? providerText : providerSelectedText) && selectedConfig.Type?.Length > 0)
                    {
                        ProviderChoiceMenu(ApiTools.GetApiConfigs()
                            .Where(a =>
                                a.IsLocal == (selectedConfig.Type == "Local") &&
                                !settings.userApiConfigs.Any(b => b.Provider == a.Provider.ToString())) // Only show providers that have not already been selected
                            .Select(c => c.Provider.ToString()), l => l,
                            l =>
                                {
                                    selectedConfig.Provider = l ?? "";
                                    selectedConfig.Name = l ?? "";
                                    selectedConfig.BaseUrl = ApiTools.GetApiConfigs().Find(a => a.Provider.ToString() == l)?.BaseUrl;
                                    selectedConfig.Port = ApiTools.GetApiConfigs().Find(a => a.Provider.ToString() == l)?.Port;
                                });
                    }

                    buttonRect.x += buttonWidth + buttonSpacing; // Move Rect to the right

                    // Get the rightListing in the correct place to draw the Active Checkbox
                    var curX = rightListing.curX;
                    var curColumnWidth = rightListing.ColumnWidth;

                    rightListing.curY = curY - UX.ButtonHeight;
                    rightListing.curX = buttonRect.x;
                    rightListing.ColumnWidth = buttonWidth;

                    bool canBeActive = selectedConfig.Type == "Local" || (selectedConfig.Type == "External" && selectedConfig.Key.Length > 0);
                    bool isActive = selectedConfig.Active;
                    if (selectedConfig?.Provider?.Length > 0 && canBeActive)
                    {
                        rightListing.CheckboxLabeled("Active", ref isActive);
                        if (isActive)
                        {
                            foreach (var config in settings.userApiConfigs)
                            {
                                if (config != selectedConfig)
                                {
                                    config.Active = false;
                                }
                            }
                            if (activeConfig != isActive) Personas.Add($"Player has activated {selectedConfig.Provider} as their AI Provider.", 0);
                        }
                        selectedConfig.Active = isActive;
                    }
                    else if (selectedConfig?.Provider?.Length > 0)
                    {
                        Text.Font = GameFont.Tiny;
                        rightListing.Label("API Key required to activate.");
                        Text.Font = GameFont.Small;
                    }
                    else
                    {
                        rightListing.Gap(configRowGap);
                    }
                    activeConfig = activeConfig ? activeConfig : isActive;

                    // Move the rightListing back to where it was
                    rightListing.curX = curX;
                    rightListing.ColumnWidth = curColumnWidth;

                    rightListing.Gap(configRowGap);

                    #endregion LLM Provider Dropdowns

                    #region LLM Provider Configuration

                    // Configuration Rows
                    var configRow = new ConfigRow(rightListing, configRowGap, labelWidth);

                    if (selectedConfig.Type == "External")
                    {
                        bool parseSuccess = Enum.TryParse(selectedConfig.Provider, true, out HelpType helpType);

                        var prevKey = selectedConfig.Key;
                        rightListing.TextField(ref selectedConfig.Key, "API Key (paste only)", true, () =>
                        {
                            selectedConfig.Key = "";
                            selectedConfig.Active = false;
                        },
                        parseSuccess ? helpType : HelpType.Default);

                        if (selectedConfig.Key != "" && selectedConfig.Key != prevKey)
                        {
                            Tools.ReloadGPTModels();
                        }

                        rightListing.Gap(configRowGap);
                    }

                    rightListing.TextField(ref selectedConfig.BaseUrl, "Base URL", false, () => selectedConfig.BaseUrl = "", HelpType.BaseUrl, DialogSize.Small);

                    rightListing.Gap(configRowGap);

                    var rightRoom = rightListing.ColumnWidth - labelWidth - (buttonSpacing * 2);
                    buttonWidth = rightRoom / 2;
                    var testButtonWidth = 48;
                    var rightButtonWidth = buttonWidth + buttonSpacing;
                    var modelRectXOffset = rightListing.ColumnWidth - buttonWidth;
                    var modelRect = new Rect(rightListing.curX + modelRectXOffset, rightListing.curY, rightButtonWidth, rowHeight);

                    var testRect = new Rect(
                        rightListing.curX + labelWidth - testButtonWidth - (buttonSpacing / 2),
                        rightListing.curY,
                        testButtonWidth,
                        rowHeight);
                    configRow.Make("Model ID", () => selectedConfig.ModelId, v => selectedConfig.ModelId = v, buttonWidth + buttonSpacing);

                    // Primary model Test Button (only enabled if a model and provider are selected)
                    if (selectedConfig.Provider.Length > 0 && selectedConfig.ModelId.Length > 0)
                    {
                        Text.Font = GameFont.Tiny;
                        if (Widgets.ButtonText(testRect, "Test"))
                        {
                            AI.TestKey(
                                response => LongEventHandler.ExecuteWhenFinished(() =>
                                {
                                    var dialog = new Dialog_MessageBox(response);
                                    Find.WindowStack.Add(dialog);
                                }),
                                selectedConfig, selectedConfig.ModelId);
                        }
                        Text.Font = GameFont.Small;
                    }

                    var modelIds = OpenAIApi.apiConfigs?.Find(a => a.Provider.ToString() == selectedConfig.Provider)?.Models?.Select(m => m.Id);

                    // Primary Model dropdown
                    if (modelIds.EnumerableNullOrEmpty() == false)
                    {
                        if (Widgets.ButtonText(modelRect, selectedConfig.Provider.Length == 0 ? "" : selectedConfig.ModelId.Length > 0 ? selectedConfig.ModelId : "Select Model") && selectedConfig.Provider.Length > 0)
                        {
                            ModelChoiceMenu(modelIds, l => l, l => 
                                selectedConfig.ModelId = l ?? "");
                        }
                    }
                    rightListing.Gap(configRowGap);

                    //Secondary Model
                    curColumnWidth = rightListing.ColumnWidth;
                    curY = rightListing.curY;
                    rightListing.ColumnWidth -= columnSpacing + (curColumnWidth / 2);
                    rightListing.CheckboxLabeled("Alternate between two models", ref selectedConfig.UseSecondaryModel);

                    if (selectedConfig.UseSecondaryModel)
                    {
                        rightListing.curY = curY;
                        rightListing.curX += columnSpacing + (curColumnWidth / 2);
                        rightListing.Slider(ref selectedConfig.ModelSwitchRatio, 1, 20, f => $"Ratio: {f}:1", 1, "Adjust the frequency at which the system switches between the primary and secondary AI models. The 'Model Switch Ratio' value determines after how many calls to the primary model the system will switch to the secondary model for one time. A lower ratio means more frequent switching to the secondary model.\n\nExample: With a ratio of '1', there is no distinction between primary and secondary—each call alternates between the two. With a ratio of '10', the system uses the primary model nine times, and then the secondary model once before repeating the cycle.", gameFont: GameFont.Small);

                        rightListing.curX -= columnSpacing + (curColumnWidth / 2);
                        rightListing.ColumnWidth = curColumnWidth;

                        rightListing.Gap(configRowGap);

                        modelRect.x = rightListing.curX + modelRectXOffset;
                        modelRect.y = rightListing.curY;

                        testRect.y = rightListing.curY; // I added this later, need to clean up.

                        configRow.Make("2nd Model ID", () => selectedConfig.SecondaryModelId, v => selectedConfig.SecondaryModelId = v, buttonWidth + buttonSpacing);

                        // Secondary model Test Button (only enabled if a model and provider are selected)
                        if (selectedConfig.Provider.Length > 0 && selectedConfig.SecondaryModelId.Length > 0)
                        {
                            Text.Font = GameFont.Tiny;
                            if (Widgets.ButtonText(testRect, "Test"))
                            {
                                AI.TestKey(
                                    response => LongEventHandler.ExecuteWhenFinished(() =>
                                    {
                                        var dialog = new Dialog_MessageBox(response);
                                        Find.WindowStack.Add(dialog);
                                    }),
                                    selectedConfig, selectedConfig.SecondaryModelId);
                            }
                            Text.Font = GameFont.Small;
                        }

                        if (modelIds.EnumerableNullOrEmpty() == false)
                        {
                            if (Widgets.ButtonText(modelRect, selectedConfig.Provider?.Length == 0 ? "" : selectedConfig.SecondaryModelId?.Length > 0 ? selectedConfig.SecondaryModelId : "Select Model") && selectedConfig.Provider?.Length > 0)
                            {
                                ModelChoiceMenu(modelIds, l => l, l => 
                                selectedConfig.SecondaryModelId = l ?? "");
                            }
                        }
                    }
                    rightListing.ColumnWidth = curColumnWidth;
                    rightListing.Gap(configRowGap);
                }
                rightListing.End();

                #endregion LLM Provider Configuration

                DrawCloseButton(inRect);
                ProgressAnimation();
                topListing.End();
                firstLoad = false;
            }

            /// <summary>
            /// Create a row of settings in the right listing
            /// </summary>
            /// <param name="_rightListing">The instance of the listing being used.</param>
            /// <param name="_rowGap">The height of the gab between rows.</param>
            /// <param name="_labelWidth">The width of the label.</param>
            public class ConfigRow(Listing_Standard _rightListing, int _rowGap, float _labelWidth)
            {
                public Listing_Standard RightListing => _rightListing;
                public int RowGap => _rowGap;
                public float LabelWidth => _labelWidth;

                public void Make<T>(string label, Func<T> getter, Action<T> setter)
                {
                    Make(label, getter, setter, 0f);
                    RightListing.Gap(RowGap);
                }

                public void Make<T>(string label, Func<T> getter, Action<T> setter, float rightButtonWidth = 0f)
                {
                    var curY = RightListing.curY;

                    RightListing.Label(label);
                    RightListing.curY = curY;
                    RightListing.curX += LabelWidth;
                    RightListing.ColumnWidth -= LabelWidth + rightButtonWidth;

                    T value = getter();
                    string valueAsString = value == null ? "" : value.ToString();
                    string result = RightListing.TextEntry(valueAsString);

                    if (typeof(T) == typeof(string))
                    {
                        setter((T)(object)result);
                    }
                    else if (typeof(T) == typeof(bool) && bool.TryParse(result, out bool boolResult))
                    {
                        setter((T)(object)boolResult);
                    }
                    else if (typeof(T) == typeof(int) && int.TryParse(result, out int intResult))
                    {
                        setter((T)(object)intResult);
                    }

                    RightListing.curX -= LabelWidth;
                    RightListing.ColumnWidth += LabelWidth + rightButtonWidth;
                }
            }

            private void DrawCloseButton(Rect inRect)
            {
                // Set up the button's dimensions.
                const float buttonHeight = 40f;
                const float buttonWidth = 150f; // Or adjust to your preferred width

                // Calculate the position to center the button horizontally.
                float buttonX = (inRect.width - buttonWidth) / 2f;
                // Position the button at the bottom with a margin.
                float buttonY = inRect.yMax - buttonHeight - 10f;

                var closeButtonRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);

                // Draw the button and check for clicks.
                if (Widgets.ButtonText(closeButtonRect, "Close", true, false, true))
                {
                    // If the button is clicked, close the window.
                    Close();
                }
            }

            public static void ProgressAnimation()
            {
                var animate = (showInstructions && animationProgress < 1f) || (!showInstructions && animationProgress > 0f);
                if (animate)
                {
                    animationProgress += (showInstructions ? 1 : -1) * (Time.deltaTime / animationDuration);
                    animationProgress = Mathf.Clamp01(animationProgress);

                    instCurHeight = Animate(instHeightHide, instHeightShow);
                    configRowGap = Animate(configRowGapHide, configRowGapShow);
                }
            }

            public static float Animate(float from, float to)
            {
                return Mathf.RoundToInt(Mathf.Min(to, from + ((to - from) * animationProgress)));
            }

            public static int Animate(int from, int to)
            {
                return from + Mathf.RoundToInt((to - from) * animationProgress);
            }

        }

        public static void LLMRow(Rect rect, UserApiConfig config, int idx)
        {
            if (config == null) return;
            var active = config?.Active ?? false;

            if (config == selectedConfig)
                Widgets.DrawBoxSolid(rect, active ? backgroundActive : background);
            else if (Mouse.IsOver(rect))
                Widgets.DrawBoxSolid(rect, active ? highlightedBackgroundActive : highlightedBackground);
            else if (active)
                Widgets.DrawBoxSolid(rect, listBackgroundActive);

            var tRect = rect;
            tRect.xMin += 3;
            tRect.yMin += 1;
            var label = config.Name?.Length == 0 ? "New LLM" : config.Name;
            _ = Widgets.LabelFit(tRect, label);

            if (Widgets.ButtonInvisible(rect))
            {
                selectedConfig = config;
                selectedConfigIndex = idx;
            }
        }

        public static void ProviderTypeMenu(string[] types, Action<string> action)
        {
            var options = new List<FloatMenuOption> { new("Provider Type", () => action(default)) };
            foreach (var type in types)
                options.Add(new FloatMenuOption(type, () => action(type)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public static void ProviderChoiceMenu<T>(IEnumerable<T> providers, Func<T, string> itemFunc, Action<T> action)
        {
            var options = new List<FloatMenuOption> { new("Choose Provider", () => action(default)) };
            foreach (var provider in providers)
                options.Add(new FloatMenuOption(itemFunc(provider), () => action(provider)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public static void ModelChoiceMenu<T>(IEnumerable<T> models, Func<T, string> itemFunc, Action<T> action)
        {
            var options = new List<FloatMenuOption> { new("Choose Model", () => action(default)) };
            if (models == null)
                return;

            foreach (var model in models)
                options.Add(new FloatMenuOption(itemFunc(model), () => action(model)));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}