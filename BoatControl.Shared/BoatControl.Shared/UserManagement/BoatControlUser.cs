using System;
using System.Collections.Generic;
using System.Text;

namespace BoatControl.Shared.UserManagement
{
    public class BoatControlUser
    {
        public int Id { get; set; }

        public string Email { get; set; }

        public string FullName { get; set; }

        public string UserToken { get; set; }

        public string[] Roles { get; set; }
    }
}
