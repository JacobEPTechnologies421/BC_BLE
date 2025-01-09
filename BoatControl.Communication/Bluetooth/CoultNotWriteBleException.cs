
namespace BoatControl.Logic
{
    [Serializable]
    internal class CoultNotWriteBleException : Exception
    {
        public CoultNotWriteBleException()
        {
        }

        public CoultNotWriteBleException(string? message) : base(message)
        {
        }

        public CoultNotWriteBleException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}