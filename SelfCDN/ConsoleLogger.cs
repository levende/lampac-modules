using System;

namespace SelfCDN
{
    public class ConsoleLogger
    {
        public static void Log(string message)
        {
            if (ModInit.ModuleSettings.IsLogEnabled == true)
            {
                Console.WriteLine(message);
            }
        }

        public static void Log(Func<string> messageFunc)
        {
            if (ModInit.ModuleSettings.IsLogEnabled == true)
            {
                Console.WriteLine(messageFunc());
            }
        }
    }
}
