using System;
using Verse;

namespace RimGPT
{
	// for scheduling a recurring action
	// - updateIntervalFunc is a function that returns the time in ticks between invoking the action
	// - action is executed at each scheduled interval
	// - startImmediately to indicate the action will be executed as soon as the task starts
	//
	public struct UpdateTask(Func<int> updateIntervalFunc, Action<Map> action, bool startImmediately)
	{
		public int updateTickCounter = startImmediately ? 0 : Rand.Range(updateIntervalFunc() / 2, updateIntervalFunc());
		public Func<int> updateIntervalFunc = updateIntervalFunc;
		public Action<Map> action = action;
	}
}