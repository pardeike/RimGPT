using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
    public class PlainTextWriter : ISsmlWriter
    {
        private readonly string _value;
        public PlainTextWriter(string value)
        {
            _value = value;
        }

        public async Task WriteAsync(XmlWriter writer)
        {
            await writer.WriteStringAsync(_value)
                .ConfigureAwait(false);
        }
    }
}