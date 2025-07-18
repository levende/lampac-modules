using System;

namespace SelfCDN.Exceptions
{
    public sealed class GroqMediaParserException : Exception
    {
        public GroqMediaParserException(string message) : base(message)
        {
        }

        public GroqMediaParserException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
