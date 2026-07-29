using AORebirth.Enums;

namespace ZoneEngine.Core;

public sealed class QuestRewardInventoryGrantResult
{
	public QuestRewardInventoryGrantStatus Status { get; private set; }

	public InventoryError InventoryError { get; private set; }

	public string ExceptionMessage { get; private set; }

	private QuestRewardInventoryGrantResult()
	{
	}

	public static QuestRewardInventoryGrantResult Succeeded()
	{
		return new QuestRewardInventoryGrantResult
		{
			Status = QuestRewardInventoryGrantStatus.Success,
			InventoryError = (InventoryError)0
		};
	}

	public static QuestRewardInventoryGrantResult InventoryAddFailed(InventoryError inventoryError)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return new QuestRewardInventoryGrantResult
		{
			Status = QuestRewardInventoryGrantStatus.InventoryAddFailed,
			InventoryError = inventoryError
		};
	}

	public static QuestRewardInventoryGrantResult PersistFailed(string exceptionMessage)
	{
		return new QuestRewardInventoryGrantResult
		{
			Status = QuestRewardInventoryGrantStatus.PersistFailed,
			InventoryError = (InventoryError)0,
			ExceptionMessage = exceptionMessage
		};
	}

	public static QuestRewardInventoryGrantResult PersistReturnedFalse()
	{
		return new QuestRewardInventoryGrantResult
		{
			Status = QuestRewardInventoryGrantStatus.PersistReturnedFalse,
			InventoryError = (InventoryError)0
		};
	}
}
