namespace Kevsoft.Ssml
{
	public interface IFluentSayNumber
	{
		ISsml AsCardinalNumber();

		ISsml AsOrdinalNumber();
	}
}