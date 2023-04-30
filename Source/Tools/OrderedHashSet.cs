using System;
using System.Collections.ObjectModel;

namespace RimGPT
{
	public class OrderedHashSet<T> : KeyedCollection<T, T>
	{
		protected override T GetKeyForItem(T item) => item;

		public void RemoveFromStart(int max)
		{
			if (max <= 0)
				return;
			var count = Math.Min(max, Count);
			for (int i = 0; i < count; i++)
				RemoveAt(0);
		}
	}
}