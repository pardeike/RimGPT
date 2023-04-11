using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class FluentFluentSay : FluentSsml, IFluentSay, ISsmlWriter
	{
		private readonly string _value;
		private ISsmlWriter _ssmlWriter;

		public FluentFluentSay(string value, ISsml ssml)
			 : base(ssml)
		{
			_value = value;
			_ssmlWriter = new PlainTextWriter(value);
		}

		public async Task WriteAsync(XmlWriter xml)
		{
			await _ssmlWriter.WriteAsync(xml)
				 .ConfigureAwait(false);
		}

		public IFluentSay AsAlias(string alias)
		{
			_ssmlWriter = new SubWriter(_ssmlWriter, alias);

			return this;
		}

		public IFluentSay AsVoice(string name, string style = "default", float styledegree = 1)
		{
			if (styledegree < 0.01f)
				styledegree = 0.01f;
			if (styledegree > 2f)
				styledegree = 2f;
			_ssmlWriter = new VoiceWriter(_ssmlWriter, name, style, styledegree);

			return this;
		}

		public IFluentSay WithProsody(float rate = 0, float pitch = 0)
		{
			_ssmlWriter = new ProsodyWriter(_ssmlWriter, rate, pitch);

			return this;
		}

		public IFluentSay Emphasised()
		{
			return Emphasised(EmphasiseLevel.NotSet);
		}

		public IFluentSay Emphasised(EmphasiseLevel level)
		{
			_ssmlWriter = new EmphasiseWriter(_ssmlWriter, level);

			return this;
		}

		public IFluentSay AsTelephone()
		{
			_ssmlWriter = new SayAsWriter("telephone", _value);

			return this;
		}

		public IFluentSayAsCharacters AsCharacters()
		{
			var fluentSayAsCharacters = new FluentSayAsCharacters(this, _value);

			_ssmlWriter = fluentSayAsCharacters;

			return fluentSayAsCharacters;
		}
	}
}