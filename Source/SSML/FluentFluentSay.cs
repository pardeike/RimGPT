using System;
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

        public IFluentSay AsVoice(string name, string style = "Default")
        {
            _ssmlWriter = new VoiceWriter(_ssmlWriter, name, style);

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