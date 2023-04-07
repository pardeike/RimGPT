using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
    public class BreakWriter : FluentSsml, IBreak, ISsmlWriter
    {
        private TimeSpan? _duration;
        private BreakStrength _strength;

        public BreakWriter(ISsml ssml)
            : base(ssml)
        {
        }

        public async Task WriteAsync(XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "break", null)
                .ConfigureAwait(false);

            if (_strength != BreakStrength.NotSet)
            {
                var value = BreakStrengthAttributeMap[_strength];

                await writer.WriteAttributeStringAsync(null, "strength", null, value)
                    .ConfigureAwait(false);
            }

            if (_duration.HasValue)
            {
                var milliseconds = _duration.Value.TotalMilliseconds;
                await writer.WriteAttributeStringAsync(null, "time", null, $"{milliseconds:F0}ms")
                    .ConfigureAwait(false);
            }

            await writer.WriteEndElementAsync()
                .ConfigureAwait(false);
        }

        public IBreak For(TimeSpan duration)
        {
            _duration = duration;

            return this;
        }

        public IBreak WithStrength(BreakStrength strength)
        {
            _strength = strength;

            return this;
        }

        private static readonly IReadOnlyDictionary<BreakStrength, string> BreakStrengthAttributeMap =
            new Dictionary<BreakStrength, string>()
            {
                {BreakStrength.None, "none"},
                {BreakStrength.ExtraWeak, "x-weak"},
                {BreakStrength.Weak, "weak"},
                {BreakStrength.Medium, "medium"},
                {BreakStrength.Strong, "strong"},
                {BreakStrength.ExtraStrong, "x-strong"},
            };

    }
}