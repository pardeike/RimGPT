using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
    public class EmphasiseWriter : ISsmlWriter
    {
        private readonly ISsmlWriter _innerWriter;
        private readonly EmphasiseLevel _emphasiseLevel;

        public EmphasiseWriter(ISsmlWriter innerWriter, EmphasiseLevel emphasiseLevel)
        {
            _innerWriter = innerWriter;
            _emphasiseLevel = emphasiseLevel;
        }

        private static readonly IReadOnlyDictionary<EmphasiseLevel, string> EmphasiseAttributeValueMap =
            new Dictionary<EmphasiseLevel, string>()
            {
                {EmphasiseLevel.Strong, "strong"},
                {EmphasiseLevel.Moderate, "moderate"},
                {EmphasiseLevel.None, "none"},
                {EmphasiseLevel.Reduced, "reduced"}
            };

        public async Task WriteAsync(XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "emphasis", null)
                .ConfigureAwait(false);

            if (_emphasiseLevel != EmphasiseLevel.NotSet)
            {
                var attrValue = EmphasiseAttributeValueMap[_emphasiseLevel];
                await writer.WriteAttributeStringAsync(null, "level", null, attrValue)
                    .ConfigureAwait(false);
            }

            await _innerWriter.WriteAsync(writer)
                .ConfigureAwait(false);

            await writer.WriteEndElementAsync()
                .ConfigureAwait(false);
        }
    }
}