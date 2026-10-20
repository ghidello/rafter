namespace Sotsera.Rafter.Internal;

internal static class DeclarationName
{
    public static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name) || name[0] is < 'a' or > 'z' || name[^1] == '-')
        {
            return false;
        }
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-')
                || (character == '-' && index > 0 && name[index - 1] == '-'))
            {
                return false;
            }
        }
        return true;
    }
}
