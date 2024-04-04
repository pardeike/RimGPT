using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class PlainTextWriter(string value) : ISsmlWriter
	{
		private readonly string _value = value;

		public async Task WriteAsync(XmlWriter writer)
		{
			await writer.WriteStringAsync(_value)
				 .ConfigureAwait(false);
		}
	}
}