using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
    public class FluentSayAsCharacters : FluentSsml, IFluentSayAsCharacters, ISsmlWriter
    {
        private readonly string _value;
        private ISsmlWriter _innerWriter;

        public FluentSayAsCharacters(ISsml ssml, string value)
            : base(ssml)
        {
            _value = value;
            _innerWriter = new SayAsWriter("characters", "characters", _value);
        }
        
        public ISsml WithGlyphInformation()
        {
            _innerWriter = new SayAsWriter("characters", "glyph", _value);

            return this;
        }

        public async Task WriteAsync(XmlWriter writer)
        {
            await _innerWriter.WriteAsync(writer)
                .ConfigureAwait(false);
        }
    }
}