namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    #endregion

    public sealed class AreteContentManifest
    {
        public AreteContentManifest()
        {
            this.DialoguePacks = new List<string>();
            this.QuestPacks = new List<string>();
        }

        public IList<string> DialoguePacks { get; set; }

        public IList<string> QuestPacks { get; set; }
    }

    public sealed class AreteContentManifestLoadResult
    {
        public AreteContentManifestLoadResult(
            IEnumerable<string> dialoguePackFiles,
            IEnumerable<string> questPackFiles,
            AreteValidationResult validation)
        {
            this.DialoguePackFiles = new List<string>(dialoguePackFiles ?? Enumerable.Empty<string>());
            this.QuestPackFiles = new List<string>(questPackFiles ?? Enumerable.Empty<string>());
            this.Validation = validation ?? new AreteValidationResult();
        }

        public IList<string> DialoguePackFiles { get; private set; }

        public IList<string> QuestPackFiles { get; private set; }

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
