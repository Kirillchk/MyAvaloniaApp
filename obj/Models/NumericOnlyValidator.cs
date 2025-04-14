public class NumericOnlyValidator
{
    public static bool IsTextNumeric(string text)
    {
        return int.TryParse(text, out _);
    }
}