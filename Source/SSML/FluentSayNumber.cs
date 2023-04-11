using System.Globalization;
using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class FluentSayNumber : IFluentSayNumber, ISsmlWriter
	{
		private readonly Ssml _ssml;
		private readonly int _value;
		private SayAsWriter _writer;

		public FluentSayNumber(Ssml ssml, int value)
		{
			this._ssml = ssml;
			this._value = value;
		}

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