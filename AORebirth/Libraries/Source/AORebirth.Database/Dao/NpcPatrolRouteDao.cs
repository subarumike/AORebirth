namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// Unified patrol route + segments loader.
    /// </summary>
    public class NpcPatrolRouteDao : Dao<DBNpcPatrolRoute, NpcPatrolRouteDao>
    {
        public static NpcPatrolRouteDao Instance => new NpcPatrolRouteDao();

        public DBNpcPatrolRoute GetByRouteId(int routeId)
        {
            return this.GetWhere(new { RouteId = routeId }).FirstOrDefault();
        }

        public DBNpcPatrolRoute GetByRouteKey(string routeKey)
        {
            if (string.IsNullOrWhiteSpace(routeKey))
            {
                return null;
            }

            return this.GetWhere(new { RouteKey = routeKey }).FirstOrDefault();
        }

        public IEnumerable<DBNpcPatrolRoute> GetByPlayfieldId(int playfieldId)
        {
            return this.GetWhere(new { PlayfieldId = playfieldId });
        }

        public IEnumerable<DBNpcPatrolSegment> GetSegmentsForRoute(int routeId)
        {
            return NpcPatrolSegmentDao.Instance.GetWhere(new { RouteId = routeId });
        }
    }
}
