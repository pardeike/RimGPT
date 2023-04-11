using System;

namespace Kevsoft.Ssml
{
	public interface IBreak : ISsml
	{
		IBreak For(TimeSpan duration);

		IBreak WithStrength(BreakStrength strength);
	}
}