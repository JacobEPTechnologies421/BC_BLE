using System;
using System.Collections.Generic;
using System.Text;

namespace BoatControl.Communication.Connections.Ble
{
    internal class RegisterResult
    {

        public RegisterResult(string data)
        {
            var split = data.Split(';');
            Number = split[0];
            Secret = split[1];
        }

        public string Number { get; set; }

        public string Secret { get; set; }
    }
}
