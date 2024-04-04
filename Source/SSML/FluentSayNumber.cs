using System.Globalization;
using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class FluentSayNumber(Ssml ssml, int value) : IFluentSayNumber, ISsmlWriter
	{
		private readonly Ssml _ssml = ssml;
		private readonly int _value = value;
		private SayAsWriter _writer;

		public ISsml AsCardinalNumber()
		{
			_writer = new SayAsWriter("cardinal", _value.ToString(CultureInfo.InvariantCulture));

			return _ssml;
		}

		public ISsml AsOrdinalNumber()
		{
			_writer = new SayAsWriter("ordinal", _value.ToString(CultureInfo.InvariantCulture));

			return _ssml;
		}

		public async Task WriteAsync(XmlWriter writer)
		{
			await _writer.WriteAsync(writer)
				 .ConfigureAwait(false);
		}
	}
}