using System;

namespace WLNetwork.Utils
{
    public static class RandomPassword
    {
        public static string CreateRandomPassword(int passwordLength)
        {
            const string consonants = "bdfghjklmnprstvy";
            const string wovels = "aeiou";

            string password = "";
            var randomNum = new Random();

            while (password.Length < passwordLength)
            {
                password += consonants[randomNum.Next(consonants.Length)];
                if (password.Length < passwordLength)
                    password += wovels[randomNum.Next(wovels.Length)];
            }

            return password;
        }
    }
}