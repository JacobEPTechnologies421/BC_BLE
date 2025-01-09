//using System;
//using System.Net;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;
//using BoatControl.Communication.Models;
//using Microsoft.Extensions.Logging;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;

//namespace BoatControl.Communication.ExternalLogging
//{
//    public class ExternalLogger
//    {
//        public static async Task<bool> LogAsync(string source, string type, LogLevel level, object obj)
//        {
//            try
//            {
//                var jObj = JObject.FromObject(obj);
//                try
//                {

//                    jObj["_type"] = new JValue(type);
//                    jObj["DeviceInfo"] = new JObject(
//                        new JProperty("DeviceType", DeviceInfo.DeviceType.ToString()),
//                        new JProperty("Manufacturer", DeviceInfo.Manufacturer),
//                        new JProperty("Model", DeviceInfo.Model),
//                        new JProperty("Name", DeviceInfo.Name),
//                        new JProperty("Platform", DeviceInfo.Platform.ToString()),
//                        new JProperty("Version", DeviceInfo.VersionString)
//                    );
//                    jObj["AppInfo"] = new JObject(
//                        new JProperty("Name", AppInfo.Name),
//                        new JProperty("Version", AppInfo.VersionString)
//                    );
//                }
//                catch
//                {
//                    /*Avoid exceptions when using console*/
//                }

//                using (var client = new HttpClient() { BaseAddress = new Uri("https://boatcontrol.net") })
//                {
//                    var response = await client.PostAsync(
//                        $"/api/logging/LogJson?level={level.ToString()}&source={source}",
//                        new StringContent(jObj.ToString(Formatting.None), Encoding.UTF8, "application/json")
//                    );

//                    var responseMessage = await response.Content.ReadAsStringAsync();
//                    response.Dispose();
//                    return responseMessage == "ok";
//                }

//            }
//            catch (Exception e)
//            {
//                return false;
//            }
//        }
//        public static void Log(string source, string type, LogLevel level, object obj) {

//            Task.Run(async () =>
//            {
                
//            });
//        }
//    }
//}
