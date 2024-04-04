using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RimGPT;
using System;
using System.Globalization;
using System.IO;

namespace OpenAI
{
	/// <summary> Handles loading API key from %User%/.openai/auth.json & JSON Config. </summary>
	public static class Configuration
	{
		/// <summary>
		/// Used for serializing and deserializing PascalCase request object fields into snake_case format for JSON. Ignores null fields when creating JSON strings.
		/// </summary>
		public static JsonSerializerSettings JsonSerializerSettings => new()
		{
			NullValueHandling = NullValueHandling.Ignore,
			MissingMemberHandling = MissingMemberHandling.Ignore,
			Culture = CultureInfo.InvariantCulture,
			ContractResolver = new DefaultContractResolver
			{
				NamingStrategy = new CustomNamingStrategy()
			}
		};

		/// <summary> Gets an Key from a file located in "{userPath}/.openai/auth.json"  </summary>
		public static string GetApiKeyFromFile()
		{
			var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var authPath = $"{userPath}/.openai/auth.json";

			if (File.Exists(authPath))
			{
				try
				{
					var json = File.ReadAllText(authPath);
					return JsonConvert.DeserializeObject<string>(json, JsonSerializerSettings);
				}
				catch (Exception ex)
				{
					Logger.Error($"Failed to load API Key from file. {ex}");
				}
			}
			else
			{
				string message = $"The 'auth.json' file does not exist in the current directory: {userPath}. Please create a JSON file in this format:\n{{\n    \"api_key\": \"sk-...W6yi\"\n}}";
				Logger.Error(message);
			}
			return default;
		}
	}
}