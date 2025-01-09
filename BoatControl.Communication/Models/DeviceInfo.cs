using System;
using System.Collections.Generic;

namespace BoatControl.Communication.Models
{
    public class DeviceInfo
    {
        private string _number;
        public string Number
        {
            get => _number;
            set => _number = value?.ToUpper() ?? "";
        }

        public string Name { get; set; }

        public string Version { get; set; }


        #region Comparison
        public override bool Equals(object obj)
        {
            return obj is DeviceInfo device &&
                   string.Equals(Number,device.Number,StringComparison.InvariantCultureIgnoreCase);
        }

        public override int GetHashCode()
        {
            return 187193536 + EqualityComparer<string>.Default.GetHashCode(Number.ToLower());
        }
        #endregion
    }
}