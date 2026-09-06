namespace AORebirth.Interfaces.Persistence.Accounts
{
    using System;

    /// <summary>Detached account data; contains no authentication or gameplay policy.</summary>
    public sealed class GameAccountData
    {
        public int AccountId { get; set; }
        /// <summary>Legacy local wall-clock creation time, without timezone conversion.</summary>
        public DateTime CreationDate { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public int AllowedCharacters { get; set; }
        public int Flags { get; set; }
        public int AccountFlags { get; set; }
        public int Expansions { get; set; }
        public int GmLevel { get; set; }
    }
}
