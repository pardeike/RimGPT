﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RimGPT;
using System;
using System.IO;

namespace OpenAI
{
	public class Configuration
	{
		public Auth Auth { get; }

		/// Used for serializing and deserializing PascalCase request object fields into snake_case format for JSON. Ignores null fields when creating JSON strings.
		private readonly JsonSerializerSettings jsonSerializerSettings = new()
		{
			NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ContractResolver = new DefaultContractResolver()
			{
				NamingStrategy = new CustomNamingStrategy()
			}
		};

		public Configuration(string apiKey = null, string organization = null)
		{
			if (apiKey == null)
			{
				var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				var authPath = $"{userPath}/.openai/auth.json";

				if (File.Exists(authPath))
				{
					var json = File.ReadAllText(authPath);
					Auth = JsonConvert.DeserializeObject<Auth>(json, jsonSerializerSettings);
				}
				else
				{
					Logger.Error("API Key is null and auth.json does not exist. Please check https://github.com/srcnalt/OpenAI-Unity#saving-your-credentials");
				}
			}
			else
			{
				Auth = new Auth()
				{
					ApiKey = apiKey,
					Organization = organization
				};
			}
		}
	}
}