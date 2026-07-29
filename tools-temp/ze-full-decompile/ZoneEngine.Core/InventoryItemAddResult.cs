using AORebirth.Enums;

namespace ZoneEngine.Core;

public sealed class InventoryItemAddResult
{
	public InventoryItemAddStatus Status { get; private set; }

	public int TargetSlot { get; private set; }

	public InventoryError InventoryError { get; private set; }

	public bool Succeeded => Status == InventoryItemAddStatus.Success;

	private InventoryItemAddResult()
	{
		TargetSlot = -1;
		InventoryError = (InventoryError)0;
	}

	public static InventoryItemAddResult Success(int targetSlot)
	{
		return new InventoryItemAddResult
		{
			Status = InventoryItemAddStatus.Success,
			TargetSlot = targetSlot,
			InventoryError = (InventoryError)0
		};
	}

	public static InventoryItemAddResult NoFreeSlot()
	{
		return new InventoryItemAddResult
		{
			Status = InventoryItemAddStatus.NoFreeSlot,
			InventoryError = (InventoryError)0
		};
	}

	public static InventoryItemAddResult Failed(int targetSlot, InventoryError inventoryError)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		return new InventoryItemAddResult
		{
			Status = InventoryItemAddStatus.Failed,
			TargetSlot = targetSlot,
			InventoryError = inventoryError
		};
	}
}
