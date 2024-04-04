namespace Kevsoft.Ssml
{
	public class SsmlConfiguration(bool excludeSpeakVersion)
	{
		public bool ExcludeSpeakVersion { get; } = excludeSpeakVersion;
	}
}