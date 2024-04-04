using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class VoiceWriter(ISsmlWriter innerWriter, string name, string style, float styledegree) : ISsmlWriter
	{
		private readonly ISsmlWriter _innerWriter = innerWriter;
		private readonly string _name = name;
		private readonly string _style = style;
		private readonly float _styledegree = styledegree;

		public async Task WriteAsync(XmlWriter writer)
		{
			await writer.WriteStartElementAsync(null, "voice", null)
				 .ConfigureAwait(false);

			await writer.WriteAttributeStringAsync(null, "name", null, _name)
				 .ConfigureAwait(false);

			await writer.WriteAttributeStringAsync(null, "style", null, _style)
				 .ConfigureAwait(false);

			await writer.WriteAttributeStringAsync(null, "styledegree", null, $"{_styledegree:F2}")
				 .ConfigureAwait(false);

			await _innerWriter.WriteAsync(writer)
				 .ConfigureAwait(false);

			await writer.WriteEndElementAsync()
				 .ConfigureAwait(false);
		}
	}
}
