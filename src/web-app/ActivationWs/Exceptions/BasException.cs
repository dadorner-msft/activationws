namespace ActivationWs.Exceptions
{
    public class BasException : Exception
    {
        public BasException(string message)
            : base(message)
        {
        }

        public BasException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
