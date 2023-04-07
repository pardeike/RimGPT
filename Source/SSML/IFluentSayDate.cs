namespace Kevsoft.Ssml
{
    public interface IFluentSayDate : ISsml
    {
        ISsml As(DateFormat dateFormat);
    }
}