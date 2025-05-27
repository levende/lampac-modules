using System;

namespace SelfCDN
{
    public class ConsoleLogger
    {
        public static void Log(string message)
        {
            if (ModInit.IsLogEnabled)
            {
                Console.WriteLine(message);
            }
        }
    }
}
