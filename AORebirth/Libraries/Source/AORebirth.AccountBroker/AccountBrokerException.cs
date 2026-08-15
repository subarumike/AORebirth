namespace AORebirth.AccountBroker
{
    using System;

    public sealed class AccountBrokerException : Exception
    {
        public AccountBrokerException(string code, string message)
            : base(message)
        {
            this.Code = code;
        }

        public string Code { get; private set; }
    }
}
