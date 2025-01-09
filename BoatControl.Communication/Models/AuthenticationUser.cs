using System;
using System.Collections.Generic;
using System.Text;

namespace BoatControl.Communication.Models
{
    public class AuthenticationUser
    {
        public string BearerToken { get; set; }
        public int Id { get; set; }

        // Legacy
        public string UserToken { get; set; }
    }
}
