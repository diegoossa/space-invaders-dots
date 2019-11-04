namespace Unity.Build
{
    internal static class StringExtensions
    {
        public static string ToForwardSlash(this string value)
        {
            return value.Replace('\\', '/');
        }
    }
}
