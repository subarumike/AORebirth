namespace AORebirth.Database.Domain.Shops
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Shops;
    using Dapper;

    /// <summary>
    /// MySQL-backed vendor catalog. The join preserves the placed vendor identity, vendor-template
    /// identity and ShopInvHash instead of collapsing shops by their shared item-template id.
    /// </summary>
    public sealed class MySqlShopDao : IShopDao
    {
        private const string VendorStockSql = @"
SELECT
    v.Id AS VendorId,
    v.Playfield AS PlayfieldId,
    v.Name AS VendorName,
    v.TemplateId AS PlacedTemplateId,
    v.Hash AS VendorTemplateHash,
    vt.Id AS VendorTemplateRowId,
    vt.ItemTemplate AS VendorItemTemplateId,
    vt.ShopInvHash AS StockGroupHash,
    vt.MinQl AS VendorMinQl,
    vt.MaxQl AS VendorMaxQl,
    vt.Buy AS BuyModifier,
    vt.Sell AS SellModifier,
    vt.Skill AS PricingSkill,
    s.Id AS StockRowId,
    s.Hash AS StockRowGroupHash,
    s.LowId,
    s.HighId,
    s.MinQl AS StockMinQl,
    s.MaxQl AS StockMaxQl,
    s.MultipleCount
FROM vendors v
LEFT JOIN vendortemplate vt ON vt.Hash = v.Hash
LEFT JOIN shopinventorytemplates s
    ON s.Hash = vt.ShopInvHash
    AND s.Active <> 0
    AND s.MinQl <= vt.MaxQl
    AND s.MaxQl >= vt.MinQl
WHERE v.Playfield = @PlayfieldId
ORDER BY v.Id, vt.Id, s.Id";

        private readonly Func<IDbConnection> connectionFactory;

        public MySqlShopDao(Func<IDbConnection> connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            this.connectionFactory = connectionFactory;
        }

        public IList<ShopVendorData> ListForPlayfield(int playfieldId)
        {
            if (playfieldId <= 0)
            {
                throw new ArgumentOutOfRangeException("playfieldId");
            }

            return this.WithConnection(connection => Materialize(
                connection.Query<JoinedRow>(VendorStockSql, new { PlayfieldId = playfieldId }).ToList(),
                playfieldId));
        }

        private static IList<ShopVendorData> Materialize(IList<JoinedRow> rows, int playfieldId)
        {
            var result = new List<ShopVendorData>();
            foreach (IGrouping<int, JoinedRow> vendorRows in rows.GroupBy(row => row.VendorId))
            {
                JoinedRow first = vendorRows.First();
                if (!first.VendorTemplateRowId.HasValue)
                {
                    throw new InvalidDataException(
                        "Vendor " + first.VendorId + " on playfield " + playfieldId
                        + " references missing vendor template " + first.VendorTemplateHash + ".");
                }

                int[] templateRows = vendorRows
                    .Where(row => row.VendorTemplateRowId.HasValue)
                    .Select(row => row.VendorTemplateRowId.Value)
                    .Distinct()
                    .ToArray();
                if (templateRows.Length != 1)
                {
                    throw new InvalidDataException(
                        "Vendor " + first.VendorId + " on playfield " + playfieldId
                        + " resolves to multiple vendor-template rows for " + first.VendorTemplateHash + ".");
                }

                var stock = new List<ShopStockData>();
                foreach (JoinedRow row in vendorRows.Where(row => row.StockRowId.HasValue))
                {
                    if (!string.Equals(row.StockGroupHash, row.StockRowGroupHash, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "Vendor " + first.VendorId + " stock row " + row.StockRowId.Value
                            + " changed ShopInvHash during materialization.");
                    }

                    int effectiveMin = Math.Max(first.VendorMinQl.Value, row.StockMinQl.Value);
                    int effectiveMax = Math.Min(first.VendorMaxQl.Value, row.StockMaxQl.Value);
                    if (effectiveMin > effectiveMax)
                    {
                        throw new InvalidDataException(
                            "Vendor " + first.VendorId + " stock row " + row.StockRowId.Value
                            + " has no effective QL overlap.");
                    }

                    stock.Add(new ShopStockData(
                        row.StockRowId.Value,
                        row.StockRowGroupHash,
                        row.LowId.Value,
                        row.HighId.Value,
                        row.StockMinQl.Value,
                        row.StockMaxQl.Value,
                        effectiveMin,
                        effectiveMax,
                        row.MultipleCount.GetValueOrDefault()));
                }

                result.Add(new ShopVendorData(
                    first.VendorId,
                    first.PlayfieldId,
                    first.VendorName ?? string.Empty,
                    first.PlacedTemplateId,
                    first.VendorTemplateHash,
                    first.VendorTemplateRowId.Value,
                    first.VendorItemTemplateId.Value,
                    first.StockGroupHash,
                    first.VendorMinQl.Value,
                    first.VendorMaxQl.Value,
                    first.BuyModifier.Value,
                    first.SellModifier.Value,
                    first.PricingSkill.GetValueOrDefault(),
                    stock));
            }

            return result;
        }

        private T WithConnection<T>(Func<IDbConnection, T> operation)
        {
            IDbConnection connection = this.connectionFactory();
            if (connection == null)
            {
                throw new InvalidOperationException("Shop connection factory returned null.");
            }

            Exception failure = null;
            try
            {
                if (connection.State == ConnectionState.Closed)
                {
                    connection.Open();
                }

                return operation(connection);
            }
            catch (Exception error)
            {
                failure = error;
                throw;
            }
            finally
            {
                try
                {
                    connection.Dispose();
                }
                catch (Exception disposalFailure)
                {
                    if (failure == null) throw;
                    failure.Data["ShopDao.ConnectionDisposeFailure"] = disposalFailure;
                }
            }
        }

        private sealed class JoinedRow
        {
            public int VendorId { get; set; }
            public int PlayfieldId { get; set; }
            public string VendorName { get; set; }
            public int PlacedTemplateId { get; set; }
            public string VendorTemplateHash { get; set; }
            public int? VendorTemplateRowId { get; set; }
            public int? VendorItemTemplateId { get; set; }
            public string StockGroupHash { get; set; }
            public int? VendorMinQl { get; set; }
            public int? VendorMaxQl { get; set; }
            public float? BuyModifier { get; set; }
            public float? SellModifier { get; set; }
            public int? PricingSkill { get; set; }
            public int? StockRowId { get; set; }
            public string StockRowGroupHash { get; set; }
            public int? LowId { get; set; }
            public int? HighId { get; set; }
            public int? StockMinQl { get; set; }
            public int? StockMaxQl { get; set; }
            public int? MultipleCount { get; set; }
        }
    }
}
