using System;
using System.Security.Cryptography;
using System.Text;

namespace BoatControl.Communication.Helpers
{
    public static class StringExtensions
    {
        public static string ToSha256(this string text)
        {
            using (var algorithm = SHA256.Create())
            {
                // Create the at_hash using the access token returned by CreateAccessTokenAsync.
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(text));

                string hashString = string.Empty;
                foreach (byte x in hash)
                {
                    hashString += String.Format("{0:x2}", x);
                }
                return hashString.ToUpper();
            }
        }
    }
}