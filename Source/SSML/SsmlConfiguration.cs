namespace Kevsoft.Ssml
{
	public class SsmlConfiguration
	{
		public bool ExcludeSpeakVersion { get; }

		public SsmlConfiguration(bool excludeSpeakVersion)
		{
			ExcludeSpeakVersion = excludeSpeakVersion;
		}
	}
}