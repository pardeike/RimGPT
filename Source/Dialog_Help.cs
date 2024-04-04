using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimGPT
{
	public class Dialog_Help : Window
	{
		private readonly HelpType helpType;
		private Vector2 dialogSize = new(640f, 520f);

		private static readonly Dictionary<DialogSize, Vector2> dialogSizes = new()
		{
			{ DialogSize.Small, new Vector2(320f, 260f) },
			{ DialogSize.Medium, new Vector2(500f, 400f) },
			{ DialogSize.Large, new Vector2(640f, 520f) }
		};

		private static readonly Dictionary<HelpType, string> helpTexts = new()
			{
            // Mod Help
            { HelpType.Default, helpText},
			{ HelpType.ModHelp, @"This mod utilizes two types of APIs: Text-to-Speech services through Microsoft Azure and AI chat services. For AI chat services, this mod supports a variety of AI providers, both externally hosted, such as OpenAI, OpenRouter and Cohere, as well as locally hosted using Ollama, LocalAI or any other locally installed models that support API. 

You must provide the necessary API keys yourself, as the overall demand for this free mod would be too costly. Your task is to create the required accounts, connect them with a credit card (if necessary), and add a minimum amount to their balance. If you are going to use a locally hosted AI model, you still need to setup a Microsoft Azure account for Text-to-Speech services. 

To set up the mod, please navigate to the AI Configuration menu by clicking on the 'AI Config' button. There, you can get further instructions on each AI Provider, add your API keys, and set up your local models." },

            // Remote APIs
            { HelpType.OpenAI, @"To enable ChatGPT functionality from OpenAI: 

1. Create an account on their developer platform at https://platform.openai.com/. 
2. Visit https://platform.openai.com/account/api-keys and create a new secret key. 
3. Paste the key into the mod settings.
4. Choose a model from the drop down list below.
5. Click the 'Active' checkbox at the top of the settings.

If you're unable to create an account, try multiple times. 

Using API keys does in fact require a payment method but its costs are minimal. For more information, refer to https://platform.openai.com/docs/api-reference/authentication and https://platform.openai.com/account/usage." },
			{ HelpType.OpenRouter, @"OpenRouter.ai is an aggregate for chat models, offering access to most of the major AI providers like OpenAI and Anthropic (Claude.ai), as well as over 100 more, including several free models. You will need to deposit a minimal amount of credits into your account using a credit card, bank account, cash app or cyrpto to use the API Key ($5.00 USD in the US). 

To learn more about the free models by visiting https://openrouter.ai/models?q=free.

To activate AI functionality with OpenRouter, follow these steps:

1. Visit https://openrouter.ai/ and sign up for an account.
2. Navigate to https://openrouter.ai/account/keys and generate a new key.
3. Copy and paste this key into the mod settings.
4. Choose a model from the drop down list below.
5. Click the 'Active' checkbox at the top of the settings." },
            //{ HelpType.Cohere,"" },
            {HelpType.TogetherAI, @"Together.ai is an aggregate for chat models, offering access to around 50 language models. The prices of their models are not competitive, but they offer a $25 free credit when you sign up for an account. You can find pricing information at https://www.together.ai/pricing.

To Activate AI functionality with Together.ai, follow these steps:

1. Visit https://www.together.ai/ and sign up for an account by hitting the 'Get Started' button.
2. Once you login, your API key will be displayed as a popup.
3. Copy and paste this key into the mod settings.
4. Enter your model id into the box below. The model should be formatted as organization/model, for example: 'cognitivecomputations/dolphin-2.5-mixtral-8x7b'. 
To get a full list of models, visit https://docs.together.ai/docs/inference-models#chat-models and look for 'Model String for API' under Chat Models. 
5. Click the 'Active' checkbox at the top of the settings.
" },
			{ HelpType.OtherExternal,"" },

            // Local APIs
            { HelpType.Ollama,"" },
			{ HelpType.LocalAI,"" },
			{ HelpType.OtherLocal,"" },

            // Voice Services
            { HelpType.Azure, @"This mod uses Microsoft Azure's Cognitive Services to enable TTS (Text-to-Speech) through their Speech service API. Although account creation requires a credit card, 500,000 characters per month are free. 

To begin, go to https://azure.microsoft.com/en-gb/free/cognitive-services/ and click 'Start free'. Sign up or log in using your Microsoft or GitHub account. 

Locate 'Speech Services' and configure the settings. Create a resource group and a subscription (maybe use 'Budget' to set a monthly cost limit). 

Within the resource group, find 'Keys and Endpoint' where you can create a key and a location (e.g., centralus). Enter both into the mod settings." },

            // Other
            { HelpType.BaseUrl,"The Base URL is the core web address for an AI API provider, serving as the starting point for all API requests. You shouldn't need to change this unless you are using an alternate provider than what is built into the mod." },
			{ HelpType.ModelId,"" },
			{ HelpType.SecondaryModelId,"" },
		};

		Vector2 scrollPosition;

		const string helpText = @"This mod utilizes two external APIs: openai.com for ChatGPT and Microsoft Azure for Text-to-Speech services. You must provide both keys yourself, as the overall demand for this free mod would be too costly. Your task is to create both accounts, connect them with a credit card and add a minimum amount to their balance.

# OpenAI

To enable ChatGPT functionality, visit https://platform.openai.com/account/api-keys and create a new secret key. Paste the key into the mod settings. If you're unable to create an account, try multiple times. Using API keys does in fact require a paid membership but its costs are minimal. For more information, refer to https://platform.openai.com/docs/api-reference/authentication and https://platform.openai.com/account/usage.

# Microsoft Azure

This mod uses Microsoft Azure's Cognitive Services to enable TTS (Text-to-Speech) through their Speech service API. Although account creation requires a credit card, 500,000 characters per month are free. To begin, go to https://azure.microsoft.com/en-gb/free/cognitive-services/ and click 'Start free'. Sign up or log in using your Microsoft or GitHub account. Locate 'Speech Services' and configure the settings. Create a resource group and a subscription (maybe use 'Budget' to set a monthly cost limit). Within the resource group, find 'Keys and Endpoint' where you can create a key and a location (e.g., centralus). Enter both into the mod settings.";

		public override Vector2 InitialSize => dialogSize;

		public Dialog_Help(HelpType helpType, DialogSize dialogSize)
		{
			doCloseX = true;
			forcePause = true;
			absorbInputAroundWindow = true;
			onlyOneOfTypeAllowed = true;
			closeOnAccept = true;
			closeOnCancel = true;
			this.helpType = helpType;
			this.dialogSize = dialogSizes[dialogSize];
		}

		public static void Show(HelpType helpType, DialogSize dialogSize = DialogSize.Large) => Find.WindowStack?.Add(new Dialog_Help(helpType, dialogSize));

		public override void DoWindowContents(Rect inRect)
		{
			var y = inRect.y;

			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(0f, y, inRect.width, 42f), "RimGPT Help");
			y += 42f;

			var textRect = new Rect(inRect.x, y, inRect.width, inRect.height - y);
			_ = Widgets.TextAreaScrollable(textRect, helpTexts[helpType], ref scrollPosition);
		}

		public enum HelpType
		{
			// Mod Help
			Default,
			ModHelp,

			// Remote APIs
			OpenAI,
			OpenRouter,
			//Cohere,
			TogetherAI,
			OtherExternal,

			// Local APIs
			Ollama,
			LocalAI,
			OtherLocal,

			// Voice Services
			Azure,

			// Other
			BaseUrl,
			ModelId,
			SecondaryModelId,
		}

		public enum DialogSize { Small, Medium, Large }
	}
}