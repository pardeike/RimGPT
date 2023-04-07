using System;
using System.Threading.Tasks;

namespace Kevsoft.Ssml
{
    public abstract class FluentSsml : ISsml
    {
        private readonly ISsml _inner;

        protected FluentSsml(ISsml inner)
        {
            _inner = inner;
        }


        IFluentSay ISsml.Say(string value)
        {
            return _inner.Say(value);
        }

        IFluentSayDate ISsml.Say(DateTime value)
        {
            return _inner.Say(value);
        }

        IFluentSayTime ISsml.Say(TimeSpan value)
        {
            return _inner.Say(value);
        }

        IFluentSayNumber ISsml.Say(int value)
        {
            return _inner.Say(value);
        }

        Task<string> ISsml.ToStringAsync()
        {
            return _inner.ToStringAsync();
        }

        IBreak ISsml.Break()
        {
            return _inner.Break();
        }

        ISsml ISsml.WithConfiguration(SsmlConfiguration configuration)
        {
            return _inner.WithConfiguration(configuration);
        }
    }
}