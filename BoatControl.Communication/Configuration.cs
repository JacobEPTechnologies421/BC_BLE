using System;

namespace BoatControl.Communication
{
    public class Configuration
    {
        public static Configuration Instance = new Configuration();


        /// <summary>
        /// Get device cloud Uri
        /// </summary>
        /// <returns></returns>
        public virtual Uri GetDeviceCloudWebserviceUri(string number, int userId)
        {
            return new Uri($"wss://boatcontrol.net/tunnelclient?number={number}&userId={userId}");
            // TODO When we have wildcard SSL
            //return new Uri($"wss://{number}.device.boatcontrol.dk/tunnelclient?number={number}&userId={userId}");
        }

        public virtual Uri GetCloudUri()
        {
            return new Uri("https://boatcontrol.net/");
        }
    }
}
