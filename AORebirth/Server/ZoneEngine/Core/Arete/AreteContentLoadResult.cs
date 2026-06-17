namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    #endregion

    public sealed class AreteContentLoadResult<TPack>
    {
        public AreteContentLoadResult(IEnumerable<TPack> packs, AreteValidationResult validation)
        {
            this.Packs = new List<TPack>(packs ?? Enumerable.Empty<TPack>());
            this.Validation = validation ?? new AreteValidationResult();
        }

        public IList<TPack> Packs { get; private set; }

        public AreteValidationResult Validation { get; private set; }

        public bool IsValid
        {
            get
            {
                return this.Validation.IsValid;
            }
        }
    }
}
