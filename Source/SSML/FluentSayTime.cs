using System;
using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
    public class FluentSayTime : IFluentSayTime, ISsmlWriter
    {
        private readonly ISsml _ssml;
        private readonly TimeSpan _value;
        private TimeFormat _format;

        public FluentSayTime(ISsml ssml, TimeSpan value)
        {
            _ssml = ssml;
            _value = value;
        }

        public ISsml In(TimeFormat format)
        {
            _format = format;

            return _ssml;
        }

        public async Task WriteAsync(XmlWriter writer)
        {
            var format = GetFormat(_format);
            var value = GetValue(_value, _format);

            var sayAsWriter = new SayAsWriter("time", format, value);

            await sayAsWriter.WriteAsync(writer)
                .ConfigureAwait(false);
        }

        private static string GetValue(TimeSpan value, TimeFormat format)
        {
            if (format == TimeFormat.TwelveHour)
            {
                return new TimeSpanToTwelveHourTimeConvertor().Convert(value);
            }
            else
            {
                return value.ToString();
            }
        }

        private static string GetFormat(TimeFormat format)
        {
            switch (format)
            {
                case TimeFormat.TwelveHour:
                    return "hms12";

                case TimeFormat.TwentyFourHour:
                    return "hms24";

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}