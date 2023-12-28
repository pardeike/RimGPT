using UnityEngine;
using Verse;

namespace RimGPT
{
	public partial class RimGPTSettings
	{
		public class DetailedReportSettingsWindow : Window
		{
			public RimGPTSettings settings;

			public DetailedReportSettingsWindow(RimGPTSettings settings)
			{
				this.settings = settings;

				// Set properties for the window
				doCloseX = true;

			}

			// Override the method to specify the initial size of the window
			public override Vector2 InitialSize => new Vector2(600f, 400f); // Adjust size as needed

			// Override DoWindowContents to lay out the contents of the window
			public override void DoWindowContents(Rect inRect)
			{
				var list = new Listing_Standard();

				// Begin the layout group 
				list.Begin(inRect);

				// Add a header label
				Text.Font = GameFont.Medium;
				list.Label("AI Insights Configuration");

				// Switch back to the small font for standard text and options
				Text.Font = GameFont.Small;

				// Description paragraph explaining permanently monitored aspects by the AI
				var description = "RimGPT automatically monitors a range of essential data points to create adaptive and responsive " +
									 "personas. This includes game state, weather, colonist activities, messages, alerts, letters, and resources.\n\n" +
									 "Below you can enable additional insight feeds for more in-depth analysis:";
				list.Label(description);

				// Add some spacing before the detailed insight options
				list.GapLine(18f);

				// Checkbox for enabling detailed power AI insight
				list.CheckboxLabeled("Enable Detailed Power AI Insight", ref settings.reportEnergyStatus,
												 "Provides the AI with detailed power grid statistics.");

				list.Gap(16f);

				// Checkbox for enabling detailed research AI insight
				list.CheckboxLabeled("Enable Detailed Research AI Insight", ref settings.reportResearchStatus,
												 "Allows the AI awareness of all researched tech, current research progress, and available research.");

				list.Gap(16f);

				// Checkbox for enabling detailed thoughts & mood AI insight
				list.CheckboxLabeled("Enable Detailed Thoughts & Mood AI Insight", ref settings.reportColonistThoughts,
												 "Enables periodic in-depth analysis by the AI of colonists' recent thoughts and their effects on mood.");

				list.Gap(16f);

				// Checkbox for enabling detailed colonist opinion AI insight
				list.CheckboxLabeled("Enable Detailed Colonist Opinion AI Insight", ref settings.reportColonistOpinions,
												 "Regularly feeds the AI a holistic view of interpersonal dynamics and mood. " +
												 "Ad-hoc opinion reports will continue regardless of this setting—for example, " +
												 "social interaction reports still include opinions—but this adds a holistic picture periodically.");

				list.Gap(16f);

				// Checkbox for enabling detailed colonist roster AI insight (Experimental feature)
				list.CheckboxLabeled("Enable Detailed Colonist Roster AI Insight (Experimental)", ref settings.reportColonistRoster,
												 "Gives the AI continuous updates on all colonists, including demographics, skills, traits, and health conditions. " +
												 "Note: May be resource-intensive.");

				list.Gap(16f);

				// Checkbox for enabling detailed room data AI insight (Experimental feature)
				list.CheckboxLabeled("Enable Detailed Room Data AI Insight (Experimental)", ref settings.reportRoomStatus,
												 "Activates comprehensive room reporting to AI, covering aspects such as cleanliness, wealth, and more. " +
												 "Warning: This is resource-heavy.");

				// End the group for AI Insights settings
				list.End();
			}
		}
	}
}