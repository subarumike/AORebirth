namespace AORebirth.Enums
{
    /// <summary>
    /// How an item instance entered the world. Stored on <c>item_instances.Source</c>.
    /// </summary>
    public enum ItemSource : byte
    {
        Command = 0,
        Loot = 1,
        Quest = 2,
        Vendor = 3,
        Other = 4
    }
}
