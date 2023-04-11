namespace Kevsoft.Ssml
{
	public static class SsmlConfigurationExtensions
	{
		public static ISsml ForAlexa(this ISsml ssml)
		{
			return ssml.WithConfiguration(new SsmlConfiguration(true));
		}
	}
}