using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class SayAsWriter : ISsmlWriter
	{
		private readonly string _interpretAs;
		private readonly string _format;
		private readonly string _value;

		public SayAsWriter(string interpretAs, string format, string value)
		{
			_interpretAs = interpretAs;
			_format = format;
			_value = value;
		}

		public SayAsWriter(string interpretAs, string value)
			 : this(interpretAs, null, value)
		{

		}

		public async Task WriteAsync(XmlWriter writer)
		{
			await writer.WriteStartElementAsync(null, "say-as", null)
				 .ConfigureAwait(false);

			await writer.WriteAttributeStringAsync(null, "interpret-as", null, _interpretAs)
				 .ConfigureAwait(false);

			if (_format != null)
			{
				await writer.WriteAttributeStringAsync(null, "format", null, _format)
					 .ConfigureAwait(false);
			}

			await writer.WriteStringAsync(_value)
				 .ConfigureAwait(false);

			await writer.WriteEndElementAsync()
				 .ConfigureAwait(false);
		}
	}
}