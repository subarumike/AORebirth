#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace AORebirth.Database.Entities
{
    #region Usings ...

    using AORebirth.Database.Dao;

    #endregion

    /// <summary>
    /// MySQL <c>tradeskill</c> row. Property names match live columns (Id1/Id2/ResultIds/…)
    /// so Dapper 1.13 maps correctly under MySqlConnector.
    /// </summary>
    [Tablename("tradeskill")]
    public class DBTradeSkill : IDBEntity
    {
        #region Public Properties

        public int DeleteFlag { get; set; }

        /// <summary>Source High ID (cluster for implants).</summary>
        public int Id1 { get; set; }

        /// <summary>Target High ID (implant for implants).</summary>
        public int Id2 { get; set; }

        public int IsImplant { get; set; }

        public int MaxBump { get; set; }

        public int MaxXP { get; set; }

        public int MinTarget { get; set; }

        public int MinXP { get; set; }

        public int QlRangePercent { get; set; }

        public string ResultIds { get; set; }

        public string Skill { get; set; }

        public string SkillPerBump { get; set; }

        public string SkillPercent { get; set; }

        #endregion

        public int Id { get; set; }
    }
}
