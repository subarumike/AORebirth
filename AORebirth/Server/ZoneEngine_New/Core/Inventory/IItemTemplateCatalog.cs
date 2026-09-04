namespace ZoneEngine_New.Core.Inventory
{
    public interface IItemTemplateCatalog
    {
        bool TryGet(int aoid, out ItemTemplate template);

        ItemTemplate Require(int aoid);
    }
}
