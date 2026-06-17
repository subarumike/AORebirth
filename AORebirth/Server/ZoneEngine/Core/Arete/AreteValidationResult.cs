namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System.Collections.Generic;

    #endregion

    public sealed class AreteValidationResult
    {
        private readonly List<string> errors = new List<string>();

        public IEnumerable<string> Errors
        {
            get
            {
                return this.errors;
            }
        }

        public int ErrorCount
        {
            get
            {
                return this.errors.Count;
            }
        }

        public bool IsValid
        {
            get
            {
                return this.errors.Count == 0;
            }
        }

        public void AddError(string location, string message)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                location = "arete";
            }

            this.errors.Add(location + ": " + message);
        }

        public void AddErrors(AreteValidationResult result)
        {
            if (result == null)
            {
                return;
            }

            foreach (string error in result.Errors)
            {
                this.errors.Add(error);
            }
        }
    }
}
