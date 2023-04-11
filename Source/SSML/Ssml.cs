using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
	public class Ssml : ISsml
	{
		private readonly List<ISsmlWriter> _says = new List<ISsmlWriter>();
		private readonly string _lang;
		private readonly string _version;
		private SsmlConfiguration _configuration;

		public Ssml(string lang = "en-US", string version = "1.0")
		{
			_lang = lang;
			_version = version;
			_configuration = new SsmlConfiguration(false);
		}

		public IFluentSay Say(string value)
		{
			var say = new FluentFluentSay(value, this);

			_says.Add(say);

			return say;
		}

		public IFluentSayDate Say(DateTime value)
		{
			var say = new FluentFluentSayDate(this, value);

			_says.Add(say);

			return say;
		}

		public IFluentSayTime Say(TimeSpan value)
		{
			if (value.Ticks < 0)
			{
				throw new ArgumentException("Time must be positive", nameof(value));
			}

			if (value.Ticks >= TimeSpan.TicksPerDay)
			{
				throw new ArgumentException("Time must be positive", nameof(value));
			}

			var fluentSayTime = new FluentSayTime(this, value);

			_says.Add(fluentSayTime);

			return fluentSayTime;
		}

		public IFluentSayNumber Say(int value)
		{
			var fluentSayNumber = new FluentSayNumber(this, value);

			_says.Add(fluentSayNumber);

			return fluentSayNumber;
		}

		public async Task Write(XmlWriter writer)
		{
			await writer.WriteStartDocumentAsync()
				 .ConfigureAwait(false);

			await writer.WriteStartElementAsync(null, "speak", "http://www.w3.org/2001/10/synthesis")
				 .ConfigureAwait(false);

			if (!_configuration.ExcludeSpeakVersion)
			{
				await writer.WriteAttributeStringAsync(null, "version", null, _version)
					 .ConfigureAwait(false);
			}

			await writer.WriteAttributeStringAsync("xml", "lang", null, _lang)
							.ConfigureAwait(false);

			for (var index = 0; index < _says.Count; index++)
			{
				var say = _says[index];

				await say.WriteAsync(writer)
					 .ConfigureAwait(false);

				if (index != _says.Count - 1)
				{
					await writer.WriteStringAsync(" ")
						 .ConfigureAwait(false);
				}
			}

			await writer.WriteEndElementAsync()
				 .ConfigureAwait(false);

			await writer.WriteEndDocumentAsync();
		}

		public async Task<string> ToStringAsync()
		{
			var stringBuilder = new StringBuilder();
			var xmlWriterSettings = new XmlWriterSettings()
			{
				Async = true
			};

			using (var xmlWriter = XmlWriter.Create(stringBuilder, xmlWriterSettings))
			{
				await Write(xmlWriter);
				await xmlWriter.FlushAsync();

				return stringBuilder.ToString();
			}
		}

		public IBreak Break()
		{
			var @break = new BreakWriter(this);

			_says.Add(@break);

			return @break;
		}

		public ISsml WithConfiguration(SsmlConfiguration configuration)
		{
			_configuration = configuration;

			return this;
		}
	}

	/*
	public enum InterpretAs
	{
		 None = 0,
		 Date,
		 Time,
		 Telephone,
		 Characters,
		 Cardinal,
		 Ordinal
	}

	*/
}
