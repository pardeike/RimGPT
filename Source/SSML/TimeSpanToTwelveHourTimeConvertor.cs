using System;

namespace Kevsoft.Ssml
{
    public class TimeSpanToTwelveHourTimeConvertor
    {
        public string Convert(TimeSpan value)
        {
            var amPm = "AM";
            var hours = value.Hours;
            var mins = value.Minutes;
            var seconds = value.Seconds;

            if (hours == 12)
            {
                amPm = "PM";
            }

            if (hours == 0)
            {
                hours = 12;
            }

            if (hours >= 13)
            {
                amPm = "PM";
                hours -= 12;
            }

            return $"{hours:00}:{mins:00}:{seconds:00}{amPm}";

        }
    }
}
