using HarmonyLib;
using System;
using System.Linq;
using System.Xml.Linq;
using Verse;

namespace OpenAI
{
	public class UserApiConfig : IExposable
	{
		public class SettingAttribute : Attribute;

		[Setting] public string Name = "";
		[Setting] public string Provider = "";
		[Setting] public bool Active = false;
		[Setting] public string Key = "";
		[Setting] public string Organization = "";
		[Setting] public string BaseUrl = "";
		[Setting] public int? Port = null;
		[Setting] public string Type = "";
		[Setting] public long CharactersSent = 0;
		[Setting] public long CharactersReceived = 0;

		//[Setting] public List<ModelConfig> ModelConfigs = [new ModelConfig()];
		[Setting] public string ModelId = "";

		[Setting] public bool UseSecondaryModel = false;
		[Setting] public string SecondaryModelId = "";
		[Setting] public int ModelSwitchRatio = 10;

		public UserApiConfig()
		{
			Name = "";
			Provider = "";
			Active = false;
			Key = "";
			Organization = "";
			BaseUrl = "";
			Port = null;
			Type = "";
			CharactersSent = 0;
			CharactersReceived = 0;
			//ModelConfigs = [new ModelConfig()];
			ModelId = "";
			UseSecondaryModel = false;
			SecondaryModelId = "";
			ModelSwitchRatio = 10;
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref Name, "Name", "RimGPT");
			Scribe_Values.Look(ref Provider, "Provider", "RimGPT");
			Scribe_Values.Look(ref Active, "Active", false);
			Scribe_Values.Look(ref Key, "Key", "");
			Scribe_Values.Look(ref Organization, "Organization", "");
			Scribe_Values.Look(ref BaseUrl, "BaseUrl", "");
			Scribe_Values.Look(ref Port, "Port", null);
			Scribe_Values.Look(ref Type, "Type", "");
			Scribe_Values.Look(ref CharactersSent, "CharactersSent", 0);
			Scribe_Values.Look(ref CharactersReceived, "CharactersReceived", 0);
			//Scribe_Collections.Look(ref ModelConfigs, "ModelConfigs", LookMode.Deep);
			Scribe_Values.Look(ref ModelId, "ModelId", "");
			Scribe_Values.Look(ref UseSecondaryModel, "UseSecondaryModel", false);
			Scribe_Values.Look(ref SecondaryModelId, "SecondaryModelId", "");
			Scribe_Values.Look(ref ModelSwitchRatio, "ModelSwitchRatio", 10);
		}

		public string GetProviderType()
		{
			var success = Enum.TryParse(Provider, out ApiProvider provider);
			if (success)
				return provider.IsLocal() ? "Local" : "External";
			else
				return default;
		}

		public string ToXml()
		{
			var userConfigElement = new XElement("UserConfig");
			var fields = AccessTools.GetDeclaredFields(GetType())
				.Where(field => Attribute.IsDefined(field, typeof(SettingAttribute)));
			foreach (var field in fields)
			{
				var fieldElement = new XElement(field.Name, field.GetValue(this));
				userConfigElement.Add(fieldElement);
			}
			return userConfigElement.ToString();
		}

		public static void UserApiConfigFromXML(string xml, UserApiConfig config)
		{
			var xDoc = XDocument.Parse(xml);
			var root = xDoc.Root;
			foreach (var element in root.Elements())
			{
				var field = AccessTools.DeclaredField(typeof(UserApiConfig), element.Name.LocalName);
				if (field == null || Attribute.IsDefined(field, typeof(SettingAttribute)) == false)
					continue;
				field.SetValue(config, field.FieldType switch
				{
					Type t when t == typeof(float) => float.Parse(element.Value),
					Type t when t == typeof(int) => int.Parse(element.Value),
					Type t when t == typeof(int?) => int.TryParse(element.Value, out var result) ? (int?)result : null,
					Type t when t == typeof(long) => long.Parse(element.Value),
					Type t when t == typeof(bool) => bool.Parse(element.Value),
					Type t when t == typeof(string) => element.Value,
					_ => throw new NotImplementedException(field.FieldType.Name)
				});
			}
		}
	}

	//public class ModelConfig : IExposable
	//{
	//    public class SettingAttribute : Attribute;

	//    [Setting] public string ModelId = "";
	//    [Setting] public string Name = "";
	//    [Setting] public string Description = "";
	//    [Setting] public bool Active = false;
	//    [Setting] public float Temperature = 1;
	//    [Setting] public int TokenLimit = 1000;
	//    [Setting] public int Price = 0;
	//    [Setting] public string Usage = "";
	//    public UsageType UsageType = UsageType.Chat;

	//    public void ExposeData()
	//    {
	//        Scribe_Values.Look(ref ModelId, "ModelId", "");
	//        Scribe_Values.Look(ref Name, "Name", "");
	//        Scribe_Values.Look(ref Description, "Description", "");
	//        Scribe_Values.Look(ref Active, "Active", false);
	//        Scribe_Values.Look(ref Temperature, "Temperature", 1);
	//        Scribe_Values.Look(ref TokenLimit, "TokenLimit", 1000);
	//        Scribe_Values.Look(ref Price, "Price", 0);
	//        Scribe_Values.Look(ref Usage, "Usage", "");
	//    }
	//}

	//public enum UsageType
	//{
	//    Chat,
	//    Embedding,
	//    Summarization,
	//}
}