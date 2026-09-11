namespace AORebirth.Interfaces.Persistence.Accounts
{
    /// <summary>
    /// Caller-supplied creation values. No defaults, hashing or policy are applied here.
    /// Creation time is supplied by persistence using the legacy application's local clock.
    /// Initial GM data is preserved on creation; there is deliberately no GM mutation operation.
    /// </summary>
    public sealed class NewGameAccountData
    {
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
