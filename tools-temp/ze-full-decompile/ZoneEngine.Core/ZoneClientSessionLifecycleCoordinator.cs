using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ZoneEngine.Core;

public sealed class ZoneClientSessionLifecycleCoordinator
{
	private readonly List<ZoneClientSessionPhase> phaseHistory = new List<ZoneClientSessionPhase>();

	public ZoneClientSessionPhase Phase { get; private set; }

	public string PhaseTraceName => GetTraceName(Phase);

	public ReadOnlyCollection<ZoneClientSessionPhase> PhaseHistory => phaseHistory.AsReadOnly();

	public ZoneClientSessionLifecycleCoordinator()
	{
		TransitionTo(ZoneClientSessionPhase.Connected);
	}

	public void BeginCharacterLoading()
	{
		TransitionTo(ZoneClientSessionPhase.CharacterLoading);
	}

	public void EnterPlayfieldLoadingForCharacterLoadOrZoningExit()
	{
		TransitionTo(ZoneClientSessionPhase.PlayfieldLoading);
	}

	public void EnterReadyBlockForSessionInit()
	{
		TransitionTo(ZoneClientSessionPhase.ReadyBlock);
	}

	public void EnterFullCharacterBoundaryForSessionInit()
	{
		TransitionTo(ZoneClientSessionPhase.FullCharacterBoundary);
	}

	public void EnterCharInPlayForVisibilityEntry()
	{
		TransitionTo(ZoneClientSessionPhase.CharInPlay);
	}

	public void CompleteInPlayForSessionInit()
	{
		TransitionTo(ZoneClientSessionPhase.InPlay);
	}

	public void EnterZoningForPlayfieldTransfer()
	{
		if (Phase != ZoneClientSessionPhase.Zoning)
		{
			if (Phase == ZoneClientSessionPhase.CharInPlay)
			{
				Phase = ZoneClientSessionPhase.InPlay;
				phaseHistory.Add(ZoneClientSessionPhase.InPlay);
			}
			if (CanTransitionTo(ZoneClientSessionPhase.Zoning))
			{
				TransitionTo(ZoneClientSessionPhase.Zoning);
				return;
			}
			Phase = ZoneClientSessionPhase.Zoning;
			phaseHistory.Add(ZoneClientSessionPhase.Zoning);
		}
	}

	public void EnterDisconnectingForSessionDispose()
	{
		TransitionTo(ZoneClientSessionPhase.Disconnecting);
	}

	public bool CanTransitionTo(ZoneClientSessionPhase phase)
	{
		if (phaseHistory.Count == 0)
		{
			return phase == ZoneClientSessionPhase.Connected;
		}
		return IsAllowedTransition(Phase, phase);
	}

	public static string GetTraceName(ZoneClientSessionPhase phase)
	{
		return "ZoneClientSession." + phase;
	}

	private void TransitionTo(ZoneClientSessionPhase phase)
	{
		if (phaseHistory.Count <= 0 || Phase != phase)
		{
			if (!CanTransitionTo(phase))
			{
				throw new InvalidOperationException("Invalid ZoneClient session transition from " + GetTraceName(Phase) + " to " + GetTraceName(phase) + ".");
			}
			Phase = phase;
			phaseHistory.Add(phase);
		}
	}

	private static bool IsAllowedTransition(ZoneClientSessionPhase from, ZoneClientSessionPhase to)
	{
		if (from == to)
		{
			return true;
		}
		if (to == ZoneClientSessionPhase.Disconnecting)
		{
			return true;
		}
		return from switch
		{
			ZoneClientSessionPhase.Connected => to == ZoneClientSessionPhase.CharacterLoading, 
			ZoneClientSessionPhase.CharacterLoading => to == ZoneClientSessionPhase.PlayfieldLoading, 
			ZoneClientSessionPhase.PlayfieldLoading => to == ZoneClientSessionPhase.ReadyBlock, 
			ZoneClientSessionPhase.ReadyBlock => to == ZoneClientSessionPhase.FullCharacterBoundary, 
			ZoneClientSessionPhase.FullCharacterBoundary => to == ZoneClientSessionPhase.CharInPlay, 
			ZoneClientSessionPhase.CharInPlay => to == ZoneClientSessionPhase.InPlay || to == ZoneClientSessionPhase.Zoning, 
			ZoneClientSessionPhase.InPlay => to == ZoneClientSessionPhase.Zoning, 
			ZoneClientSessionPhase.Zoning => to == ZoneClientSessionPhase.PlayfieldLoading || to == ZoneClientSessionPhase.ReadyBlock, 
			_ => false, 
		};
	}
}
