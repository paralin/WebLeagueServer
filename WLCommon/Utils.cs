namespace WLCommon
{
    public static class Utils
    {
        public static string ToSteamID64(this int accountid)
        {
            return (accountid + 76561197960265728) + "";
        }
    }
}