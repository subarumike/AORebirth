using System;
using System.Collections.Generic;

namespace ZoneEngine.Core.Playfields;

public sealed class PlayfieldLifecycleCapture : IPlayfieldLifecycleRecorder, IDisposable
{
	private readonly List<PlayfieldLifecycleEvent> events = new List<PlayfieldLifecycleEvent>();

	private readonly IPlayfieldLifecycleRecorder previousRecorder;

	private readonly int previousOrder;

	public IList<PlayfieldLifecycleEvent> Events => events.AsReadOnly();

	internal PlayfieldLifecycleCapture(IPlayfieldLifecycleRecorder previousRecorder, int previousOrder)
	{
		this.previousRecorder = previousRecorder;
		this.previousOrder = previousOrder;
	}

	public void Dispose()
	{
		PlayfieldLifecycleTrace.Restore(previousRecorder, previousOrder);
	}

	public void Record(PlayfieldLifecycleEvent lifecycleEvent)
	{
		if (lifecycleEvent != null)
		{
			events.Add(lifecycleEvent);
		}
	}
}
