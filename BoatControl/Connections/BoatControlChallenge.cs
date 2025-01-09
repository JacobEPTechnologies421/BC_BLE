using System.Text;
using System.Text.RegularExpressions;

namespace BoatControl.Communication.Connections.Shared
{
    public class BoatControlChallenge
    {
        private static Regex _base64Regex = new Regex(@"^([A-Za-z0-9+/]{4})*([A-Za-z0-9+/]{4}|[A-Za-z0-9+/]{3}=|[A-Za-z0-9+/]{2}==)$", RegexOptions.Compiled);

        public string Challenge { get; set; }
        public string DeviceNumber { get; set; }

        public string DeviceVersion { get; set; }
        public int Owner { get; set; }
        public string Name { get; set; }

        public static BoatControlChallenge GetChallenge(string line)
        {
            var split = line.Trim('\n').Split(new[] { ' ' }, 5);
            if (split.Length != 5)
                return null; // Invalid
            return new BoatControlChallenge()
            {
                Challenge = split[0],
                DeviceNumber = split[1],
                DeviceVersion = split[2],
                Owner = int.TryParse(split[3], out var owner) ? owner : 0,
                Name = split[4].Length % 4 == 0 && _base64Regex.IsMatch(split[4]) ? Encoding.UTF8.GetString(System.Convert.FromBase64String(split[4])) : split[4]
            };
        }
    }
}