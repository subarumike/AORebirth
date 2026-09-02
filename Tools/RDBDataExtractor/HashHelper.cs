namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    internal static class HashHelper
    {
        internal static string Sha256Hex(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(data);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
