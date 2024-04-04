using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class SubWriter(ISsmlWriter innerWriter, string alias) : ISsmlWriter
	{
		private readonly ISsmlWriter _innerWriter = innerWriter;
		private readonly string _alias = alias;

		public async Task WriteAsync(XmlWriter writer)
		{
			await writer.WriteStartElementAsync(null, "sub", null)
				 .ConfigureAwait(false);

			await writer.WriteAttributeStringAsync(null, "alias", null, _alias)
				 .ConfigureAwait(false);

			await _innerWriter.WriteAsync(writer)
				 .ConfigureAwait(false);

			await writer.WriteEndElementAsync()
				 .ConfigureAwait(false);
		}
	}
}