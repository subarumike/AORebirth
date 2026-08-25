namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// Patrol segment loader.
    /// </summary>
    public class NpcPatrolSegmentDao : Dao<DBNpcPatrolSegment, NpcPatrolSegmentDao>
    {
        public static NpcPatrolSegmentDao Instance => new NpcPatrolSegmentDao();

        public IEnumerable<DBNpcPatrolSegment> GetByRouteId(int routeId)
        {
            return this.GetWhere(new { RouteId = routeId })
                .OrderBy(s => s.SegmentIndex);
        }
    }
}
