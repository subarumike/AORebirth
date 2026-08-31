namespace AORebirth.AccountBroker
{
    public static class PasswordPolicy
    {
        public const int MinimumLength = 8;

        public const int MaximumLength = 128;

        public static bool IsValid(string password)
        {
            return !string.IsNullOrEmpty(password)
                && password.Length >= MinimumLength
                && password.Length <= MaximumLength;
        }

        public static void RequireValid(string password)
        {
            if (!IsValid(password))
            {
                throw new AccountBrokerException(
                    "INVALID_PASSWORD",
                    "Password must be between 8 and 128 characters.");
            }
        }
    }
}
