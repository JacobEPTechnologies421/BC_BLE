using System;
using System.Collections.Generic;
using System.Text;
using BoatControl.Communication.Models;

namespace BoatControl.Communication.Helpers
{
    public static class DeviceExtensions
    {
        public static bool IsUnregistered(this DeviceInfo device)
        {
            return string.Equals(device.Number, "Unregistered", StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
