namespace AORebirth.Interfaces.Persistence.Accounts
{
    /// <summary>Account authentication inputs, not an authentication decision. Treat the hash as sensitive.</summary>
    public sealed class GameAccountAuthenticationData
    {
        public int AccountId { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public int AllowedCharacters { get; set; }
        public int Flags { get; set; }
        public int AccountFlags { get; set; }
        public int Expansions { get; set; }
        public int GmLevel { get; set; }
    }
}
