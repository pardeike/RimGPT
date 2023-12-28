using System;
using Verse;

namespace RimGPT
{
	// for scheduling a recurring action
	// - updateOn is the interval in ticks after which the action should be invoked
	// - action is executed at each scheduled interval
	// - startImmediately to indicate the action will be executed as soon as the task starts
	//
	public struct UpdateTask(int updateOn, Action<Map> action, bool startImmediately)
	{
		public int UpdateTicks = startImmediately ? -1 : 0;
		public int UpdateOn = updateOn;
		public Action<Map> Action = action;
	}
}