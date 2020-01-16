using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using RestSharp;

namespace FXSTestBot
{
    public static class TelegramWebhook
    {
        private static TraceWriter logger;
        

        [FunctionName("TelegramWebHook")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            logger = log;

            logger.Info("C# HTTP trigger function processed a request.");

            // Get request body
            var content = req.Content;

            var telegramMessage = content.ReadAsStringAsync().Result;
            var telegramUpdate = JsonConvert.DeserializeObject<TelegramUpdate>(telegramMessage);

            logger.Info($"Received this {telegramMessage}");

            var userMessage = telegramUpdate.Message.Text;

            if (IsStartMessage(userMessage))
            {
                Guid fxstreetUserId = ExtractFXStreetUserId(userMessage);
                if (fxstreetUserId != null)
                {
                    var telegramUserId = telegramUpdate.Message.From.Id;
                    var responseMessage = CreateResponseTelegramMessage(telegramUserId, fxstreetUserId);
                    bool respose = SendMessageToTelegram(responseMessage);
                }
            }

            return req.CreateResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(telegramMessage));
        }

        private static bool SendMessageToTelegram(TelegramResponse telegramResponse)
        {
            string token = Environment.GetEnvironmentVariable("TelgramToken");
            string resource = "sendMessage";
            RestClient restClient = new RestClient(new Uri($"https://api.telegram.org/{token}/{resource}?chat_id={telegramResponse.ChatId}&text={telegramResponse.Text}"));
            RestRequest restRequest = new RestRequest($"", DataFormat.Json);           
            //restRequest.AddJsonBody(telegramResponse);

            var response = restClient.Post(restRequest);

            return response.IsSuccessful;
        }

        private static bool IsStartMessage(string message)
        {
            return message.StartsWith("/start");
        }

        private static Guid ExtractFXStreetUserId(string message)
        {
            Guid fxstreetUserId = default(Guid);
            try
            {
                fxstreetUserId = Guid.Parse(message.Substring(6));
            }
            catch (Exception ex)
            {
                logger.Error("Something went wrong parsing the guid", ex);
            }

            return fxstreetUserId;

        }

        private static TelegramResponse CreateResponseTelegramMessage(string telegramUserId, Guid fxstreetUserId)
        {
            return new TelegramResponse()
            {
                ChatId = int.Parse(telegramUserId),
                Text = $"Welcome"
            };


        }
    }

    public class TelegramResponse
    {
        [JsonProperty("chat_id")]
        public int ChatId { get; set; }
        public string Text { get; set; }
    }

    public class TelegramUpdate
    {
        public string UpdateId { get; set; }
        public TelegramMessage Message { get; set; }

    }

    public class TelegramMessage
    {
        public double Date { get; set; }
        [JsonProperty("message_id")]
        public int MessageId { get; set; }
        public string Text { get; set; }
        public TelegramContent Chat { get; set; }
        public TelegramContent From { get; set; }
    }

    public class TelegramContent
    {
        [JsonProperty("last_name")]
        public string LastName { get; set; }
        public string Id { get; set; }
        [JsonProperty("first_name")]
        public string FirstName { get; set; }
        public string UserName { get; set; }
    }
}

//{
//"update_id":10000,
//"message":{
//  "date":1441645532,
//  "chat":{
//     "last_name":"Test Lastname",
//     "id":858012761,
//     "first_name":"Test",
//     "username":"Test"
//  },
//  "message_id":1365,
//  "from":{
//     "last_name":"Test Lastname",
//     "id":858012761,
//     "first_name":"Test",
//     "username":"Test"
//  },
//  "text":"/start 49629ec4-380f-4fda-8e18-9f0aa5adc75f"
//}
//}
