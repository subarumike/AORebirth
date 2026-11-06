namespace AORebirth.Interfaces.Persistence.CharacterCreation
{
    using System.Collections.Generic;

    /// <summary>One profession's create-time inventory and optional uploaded nanos.</summary>
    public sealed class CharacterStartProfessionPackage
    {
        public CharacterStartProfessionPackage(
            int professionId,
            string profession,
            string evidence,
            IReadOnlyList<CharacterStartPackageItem> items,
            IReadOnlyList<int> uploadedNanos)
        {
            ProfessionId = professionId;
            Profession = profession ?? string.Empty;
            Evidence = evidence ?? string.Empty;
            Items = items ?? new CharacterStartPackageItem[0];
            UploadedNanos = uploadedNanos ?? new int[0];
        }

        public int ProfessionId { get; }

        public string Profession { get; }

        public string Evidence { get; }

        public IReadOnlyList<CharacterStartPackageItem> Items { get; }

        public IReadOnlyList<int> UploadedNanos { get; }

        /// <summary>Resolves a new character's items without changing the shared cached package.</summary>
        public CharacterStartProfessionPackage ResolveForGender(int gender)
        {
            var items = new List<CharacterStartPackageItem>(Items.Count);
            foreach (CharacterStartPackageItem item in Items)
                items.Add(item.ResolveForGender(gender));

            return new CharacterStartProfessionPackage(ProfessionId, Profession, Evidence, items, UploadedNanos);
        }
    }
}
