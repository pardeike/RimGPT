using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class FluentFluentSayDate(ISsml ssml, DateTime date) : FluentSsml(ssml), IFluentSayDate, ISsmlWriter
	{
		private DateFormat _dateFormat;
		private readonly DateTime _date = date;

		public ISsml As(DateFormat dateFormat)
		{
			_dateFormat = dateFormat;

			return this;
		}

		public async Task WriteAsync(XmlWriter writer)
		{
			var format = DateFormatMap[_dateFormat];
			var date = FormatDate(_date, _dateFormat);

			var sayAsWriter = new SayAsWriter("date", format, date);

			await sayAsWriter.WriteAsync(writer)
				 .ConfigureAwait(false);
		}

		private static string FormatDate(DateTime date, DateFormat dateFormat)
		{
			var stringFormat = DateFormatToDateTimeFormatString[dateFormat];

			return date.ToString(stringFormat);
		}

		private static readonly IReadOnlyDictionary<DateFormat, string> DateFormatToDateTimeFormatString =
			 new Dictionary<DateFormat, string>()
			 {
					 {DateFormat.NotSet, "yyyyMMdd"},
					 {DateFormat.MonthDayYear, "MMddyyyy"},
					 {DateFormat.DayMonthYear, "ddMMyyyy"},
					 {DateFormat.YearMonthDay, "yyyyMMdd"},
					 {DateFormat.MonthDay, "MMdd"},
					 {DateFormat.DayMonth, "ddMM"},
					 {DateFormat.YearMonth, "yyyyMM"},
					 {DateFormat.MonthYear, "MMyyyy"},
					 {DateFormat.Day, "dd"},
					 {DateFormat.Month, "MM"},
					 {DateFormat.Year, "yyyy"}
			 };

		private static readonly IReadOnlyDictionary<DateFormat, string> DateFormatMap =
			 new Dictionary<DateFormat, string>()
			 {
					 {DateFormat.NotSet, null},
					 {DateFormat.MonthDayYear, "mdy"},
					 {DateFormat.DayMonthYear, "dmy"},
					 {DateFormat.YearMonthDay, "ymd"},
					 {DateFormat.MonthDay, "md"},
					 {DateFormat.DayMonth, "dm"},
					 {DateFormat.YearMonth, "ym"},
					 {DateFormat.MonthYear, "my"},
					 {DateFormat.Day, "d"},
					 {DateFormat.Month, "m"},
					 {DateFormat.Year, "y"}
			 };
	}
}