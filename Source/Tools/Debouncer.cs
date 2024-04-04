using System;
using System.Threading;

namespace RimGPT
{
	public class Debouncer(int debounceInterval)
	{
		private Timer _debounceTimer;
		private readonly object _lockObject = new();
		private readonly int _debounceInterval = debounceInterval;

		public void Debounce(Action action)
		{
			lock (_lockObject)
			{
				_debounceTimer?.Dispose();
				_debounceTimer = new Timer(
					 _ => action(),
					 null,
					 _debounceInterval,
					 Timeout.Infinite);
			}
		}
	}
}