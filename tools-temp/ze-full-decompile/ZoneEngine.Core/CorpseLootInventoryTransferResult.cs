using AORebirth.Enums;

namespace ZoneEngine.Core;

public sealed class CorpseLootInventoryTransferResult
{
	public CorpseLootInventoryTransferStatus Status { get; set; }

	public int TargetPageNumber { get; set; }

	public int TargetSlot { get; set; }

	public InventoryError InventoryError { get; set; }

	public string ExceptionMessage { get; set; }

	public bool Succeeded => Status == CorpseLootInventoryTransferStatus.Success;

	public CorpseLootInventoryTransferResult()
	{
		Status = CorpseLootInventoryTransferStatus.NoFreeSlot;
		TargetPageNumber = -1;
		TargetSlot = -1;
		InventoryError = (InventoryError)0;
	}
}
