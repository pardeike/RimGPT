using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class ProsodyWriter : ISsmlWriter
	{
		private readonly ISsmlWriter _innerWriter;
		private readonly float _rate;
		private readonly float _pitch;

		public ProsodyWriter(ISsmlWriter innerWriter, float rate, float pitch)
		{
			_innerWriter = innerWriter;
			_rate = rate;
			_pitch = pitch;
		}

		private static async Task AddAttribute(XmlWriter writer, string name, float value)
		{
			if (value < -1f)
				value = -1f;
			if (value > 1f)
				value = 1f;
			if (value != 0f)
			{
				var str = $"{(int)(value * 100)}%";
				if (value > 0)
					str = "+" + str;

				await writer.WriteAttributeStringAsync(null, name, null, str)
					 .ConfigureAwait(false);
			}
		}

		public async Task WriteAsync(XmlWriter writer)
		{
			var needsWrapping = _rate != 0 || _pitch != 0;

			if (needsWrapping)
			{
				await writer.WriteStartElementAsync(null, "prosody", null)
					 .ConfigureAwait(false);

				await AddAttribute(writer, "rate", _rate);
				await AddAttribute(writer, "pitch", _pitch);
			}

			await _innerWriter.WriteAsync(writer)
				 .ConfigureAwait(false);

			if (needsWrapping)
				await writer.WriteEndElementAsync()
					 .ConfigureAwait(false);
		}
	}
}