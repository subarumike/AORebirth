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

        /// <summary>Selects starter rows for the character's final, breed-normalized gender.</summary>
        public CharacterStartProfessionPackage SelectForGender(int gender)
        {
            var selected = new List<CharacterStartPackageItem>();
            for (int i = 0; i < Items.Count; i++)
            {
                CharacterStartPackageItem item = Items[i];
                if (!item.Gender.HasValue || item.Gender.Value == gender)
                    selected.Add(item);
            }

            return new CharacterStartProfessionPackage(ProfessionId, Profession, Evidence, selected.ToArray(), UploadedNanos);
        }
    }
}
