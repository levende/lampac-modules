using System;

namespace SelfCDN.Exceptions
{
    public sealed class OpenAiException : Exception
    {
        public OpenAiException(string message) : base(message)
        {
        }

        public OpenAiException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
