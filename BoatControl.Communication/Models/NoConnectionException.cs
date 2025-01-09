using System;

namespace BoatControl.Communication.Models
{
    public class NoConnectionException : Exception
    {
        public NoConnectionException()
        {
        }

        public NoConnectionException(string message) : base(message)
        {
        }
    }
}
