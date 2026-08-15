namespace AORebirth.AccountBroker
{
    using System;
    using System.Text.RegularExpressions;

    public static class UsernamePolicy
    {
        private static readonly Regex NewRegistrationPattern =
            new Regex("^[A-Za-z0-9]{6,32}$", RegexOptions.Compiled);

        private static readonly Regex LegacyPattern =
            new Regex("^[A-Za-z0-9]{1,32}$", RegexOptions.Compiled);

        public static string NormalizeForNewRegistration(string username)
        {
            if (username == null || !NewRegistrationPattern.IsMatch(username))
            {
                throw new AccountBrokerException(
                    "INVALID_USERNAME",
                    "New account usernames must be ASCII alphanumeric and 6-32 characters.");
            }

            return username.ToLowerInvariant();
        }

        public static string NormalizeForLegacyLink(string username)
        {
            if (username == null || !LegacyPattern.IsMatch(username))
            {
                throw new AccountBrokerException(
                    "INVALID_LEGACY_USERNAME",
                    "Legacy account usernames must be ASCII alphanumeric and 1-32 characters.");
            }

            return username.ToLowerInvariant();
        }
    }
}
