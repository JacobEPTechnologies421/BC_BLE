//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using BoatControl.Communication.Models;
//using BoatControl.Communication.Storage;
//using BoatControl.Shared.Messaging;

using System;
using System.Runtime.Serialization;

namespace BoatControl.Communication.Caching
{
    [Serializable]
    internal class Md5MismatchException : Exception
    {
        public Md5MismatchException()
        {
        }

        public Md5MismatchException(string message) : base(message)
        {
        }

        public Md5MismatchException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected Md5MismatchException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}