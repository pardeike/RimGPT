namespace Kevsoft.Ssml
{
	public interface IFluentSay : ISsml
	{
		IFluentSay AsAlias(string alias);
		IFluentSay AsVoice(string name, string style = "default", float styledegree = 1);

		IFluentSay WithProsody(float rate = 0, float pitch = 0);

		IFluentSay Emphasised();

		IFluentSay Emphasised(EmphasiseLevel level);

		IFluentSay AsTelephone();

		IFluentSayAsCharacters AsCharacters();
	}
}