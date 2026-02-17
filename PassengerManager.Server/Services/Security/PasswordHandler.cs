using System.Security.Cryptography;
using System.Text;
using PasswordGenerator;
using static PassengerManager.Server.Services.Static.AuthDefaults;

namespace PassengerManager.Server.Services.Security
{
    //DEFAULT PASSWORD POLICY DICTATES AS FOLLOWS:
    //1.
    //2.

    public static class PasswordHandler
    {
        public static string GetHashedPassword(string password)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            StringBuilder builder = new StringBuilder();

            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }

        public static bool VerifyPassword(string inputPassword, string storedHashedPassword)
        {
            string inputHashedPassword = GetHashedPassword(inputPassword);

            return string.Equals(inputHashedPassword, storedHashedPassword, StringComparison.OrdinalIgnoreCase);
        }
    }
}
