using UnityEngine;
using Verse;
using System.Threading.Tasks;

namespace RimGPT
{
	public class Dialog_Personality : Window
	{
		public Persona persona;
		public string text;
		public string textSecondary;
		Vector2 scrollPosition;
		Vector2 scrollPositionSecondary;

		public override Vector2 InitialSize => new(1024f, 768f);

		public string loadingMessagePrimary = "";
		public string loadingMessageSecondary = "";
		private bool uiNeedsRefresh = false;
		public Dialog_Personality(Persona persona)
		{
			this.persona = persona;
			text = persona.personality;

			if (RimGPTMod.Settings.UseSecondaryModel)
			{
				textSecondary = persona.personalitySecondary ?? AI.defaultPersonalitySecondary;
			}

			doCloseX = false;
			doCloseButton = false;
			forcePause = true;
			absorbInputAroundWindow = true;
			onlyOneOfTypeAllowed = true;
			closeOnAccept = false;
			closeOnCancel = false;
		}

		public static void Show(Persona persona) => Find.WindowStack?.Add(new Dialog_Personality(persona));

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Small;
			float y = inRect.y;
			GUIStyle centeredStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter
			};

			if (uiNeedsRefresh)
			{
				RefreshUI();
			}

			var buttonHeight = 48f;
			var padding = 10f;

			// Primary personality interface
			Widgets.Label(new Rect(0f, y, inRect.width, 42f), RimGPTMod.Settings.ChatGPTModelPrimary);

			// Determine the height allocated to each text area based on whether the secondary model is used
			var primaryTextHeight = RimGPTMod.Settings.UseSecondaryModel ? (inRect.height - (padding + buttonHeight) * 2 - 96f) / 2
																																	 : inRect.height - (padding + buttonHeight) - 96f;

			// The primary text area rectangle
			var primaryTextRect = new Rect(inRect.x, y + 42f, inRect.width, primaryTextHeight);

			if (!string.IsNullOrEmpty(loadingMessagePrimary))
			{
				Widgets.Label(new Rect(primaryTextRect.x, primaryTextRect.y + primaryTextRect.height / 2 - 15f, primaryTextRect.width, 30f), loadingMessagePrimary);
			}
			else
			{
				text = Widgets.TextAreaScrollable(primaryTextRect, text, ref scrollPosition);
			}

			// Buttons for primary interface
			var primaryButtonRect = new Rect(primaryTextRect.x, primaryTextRect.yMax + padding, primaryTextRect.width, buttonHeight);
			RenderButtons(primaryButtonRect, isPrimary: true);

			// Secondary personality interface if enabled
			if (RimGPTMod.Settings.UseSecondaryModel)
			{
				float secondaryY = primaryButtonRect.yMax + padding;
				Widgets.Label(new Rect(0f, secondaryY, inRect.width, 42f), RimGPTMod.Settings.ChatGPTModelSecondary);

				// The secondary text area rectangle
				var secondaryTextHeight = primaryTextHeight - (padding + buttonHeight + 24f); // Same height as primary
				var secondaryTextRect = new Rect(inRect.x, secondaryY + 42f, inRect.width, secondaryTextHeight);

				if (!string.IsNullOrEmpty(loadingMessageSecondary))
				{
					Widgets.Label(new Rect(secondaryTextRect.x, secondaryTextRect.y + secondaryTextRect.height / 2 - 15f, secondaryTextRect.width, 30f), loadingMessageSecondary);
				}
				else
				{
					textSecondary = Widgets.TextAreaScrollable(secondaryTextRect, textSecondary, ref scrollPositionSecondary);
				}

				// Buttons for secondary interface
				var secondaryButtonRect = new Rect(secondaryTextRect.x, secondaryTextRect.yMax + padding, secondaryTextRect.width, buttonHeight);
				RenderButtons(secondaryButtonRect, isPrimary: false);
			}

			// Final buttons: Save and Cancel
			float finalButtonHeight = 24f;
			float finalButtonWidth = 100f;
			float finalButtonSpacing = 10f;
			float finalButtonY = RimGPTMod.Settings.UseSecondaryModel ? inRect.height - finalButtonHeight - 32f
																																: primaryButtonRect.yMax + padding;

			// Save Button
			Rect saveButtonRect = new Rect(inRect.x + (inRect.width / 2f) - finalButtonWidth - (finalButtonSpacing / 2f), finalButtonY, finalButtonWidth, buttonHeight);

			if (Widgets.ButtonText(saveButtonRect, "Save"))
			{
				Save();
			}

			// Cancel Button
			Rect cancelButtonRect = new Rect(inRect.x + (inRect.width / 2f) + (finalButtonSpacing / 2f), finalButtonY, finalButtonWidth, buttonHeight);

			if (Widgets.ButtonText(cancelButtonRect, "Cancel"))
			{
				Close();
			}
		}

		private void RenderButtons(Rect rect, bool isPrimary)
		{
			if (!string.IsNullOrEmpty(loadingMessagePrimary) || !string.IsNullOrEmpty(loadingMessageSecondary))
				return;

			// Dynamic button labels based on the 'isPrimary' flag
			string copyLabel = isPrimary ? $"Copy to {RimGPTMod.Settings.ChatGPTModelSecondary}"
																	 : $"Copy to {RimGPTMod.Settings.ChatGPTModelPrimary}";
			string optimizeLabel = isPrimary ? $"Optimize for {RimGPTMod.Settings.ChatGPTModelSecondary}"
																			 : $"Optimize for {RimGPTMod.Settings.ChatGPTModelPrimary}";

			string[] labels = new string[] { copyLabel, optimizeLabel, "Default" };

			// Starting X position
			float x = rect.x;

			foreach (var label in labels)
			{
				var buttonRect = new Rect(x, rect.y, 300f, rect.height);
				if (Widgets.ButtonText(buttonRect, label))
				{
					if (label == copyLabel)
					{
						if (isPrimary) textSecondary = text;
						else text = textSecondary;
					}
					else if (label == optimizeLabel)
					{
						OptimizePersonalityForModel(
								isPrimary ? RimGPTMod.Settings.ChatGPTModelPrimary : RimGPTMod.Settings.ChatGPTModelSecondary,
								isPrimary ? RimGPTMod.Settings.ChatGPTModelSecondary : RimGPTMod.Settings.ChatGPTModelPrimary,
								isPrimary
						);
					}
					else if (label == "Default")
					{
						if (isPrimary) text = AI.defaultPersonality;
						else textSecondary = AI.defaultPersonalitySecondary;
					}
				}
				x += buttonRect.width + 10f; // Add spacing between buttons
			}
		}

		public void Save()
		{
			persona.personality = text;
			if (RimGPTMod.Settings.UseSecondaryModel)
			{
				persona.personalitySecondary = textSecondary;
			}
			Close();
		}

		private void OptimizePersonalityForModel(string requestModel, string targetModel, bool isPrimary)
		{

			string personalityToOptimize = isPrimary ? persona.personality : persona.personalitySecondary;

			// Set the appropriate loading message
			if (isPrimary)
			{
				loadingMessagePrimary = $"Optimizing Content for {targetModel}";
				loadingMessageSecondary = "Being Optimized...";
			}
			else
			{
				loadingMessageSecondary = $"Optimizing Content for {targetModel}";
				loadingMessagePrimary = "Being Optimized...";
			}

			AI.QuickOptimizePersonality(requestModel, targetModel, personalityToOptimize,
					 response => LongEventHandler.ExecuteWhenFinished(() =>
					 {
						 loadingMessagePrimary = "";
						 loadingMessageSecondary = "";
						 if (isPrimary)
						 {
							 textSecondary = response;
						 }
						 else
						 {
							 text = response;
						 }
					 }
					 )


			);
		}
		private void RefreshUI()
		{
			// Reset the flag first to avoid multiple unnecessary refreshes
			uiNeedsRefresh = false;

			// Logic to refresh the parts of the UI that display dynamic content
			// For example, you may reassign the text fields, reset scroll positions, etc.
			text = persona.personality;
			if (RimGPTMod.Settings.UseSecondaryModel)
			{
				textSecondary = persona.personalitySecondary ?? AI.defaultPersonalitySecondary;
			}

			// Optionally, if you need to fully redraw the window, though rarely necessary:
			Find.WindowStack.TryRemove(this, doCloseSound: false);
			Find.WindowStack.Add(new Dialog_Personality(persona));
		}
		private void NotifyContentChanged()
		{
			uiNeedsRefresh = true;
		}
	}
}
