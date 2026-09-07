namespace AudioProvider;

public static class ConsoleUtils
{
    public static void PrintException(Exception exception)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine(exception);
        Console.ResetColor();
    }
}
