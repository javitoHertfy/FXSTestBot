using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Telegram.Bot;
using Microsoft.Azure.WebJobs.Host;
using System.Net;
using Telegram.Bot.Types;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace FXSTestBot2
{
    public static class TelegramWebhook
    {
        private static ILogger logger;
        private static ITelegramBotClient iTelegramBotClient;
        private static string token = "933340696:AAHywoMZNKKlSrMx51KnVGuw9cIhNcdjeVM";

        [FunctionName("TelegramWebHook")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req
            , ILogger log)
        {
            logger = log;

            logger.LogInformation("C# HTTP trigger function processed a request.");
            
            iTelegramBotClient = new TelegramBotClient(token);

            // Get request body
            

            var telegramMessage = req.StreamToStringAsync().Result;
            var telegramUpdate = JsonConvert.DeserializeObject<TelegramUpdate>(telegramMessage);

            logger.LogInformation($"Received this {telegramMessage}");

            var userMessage = telegramUpdate.Message.Text;
            var telegramUserId = telegramUpdate.Message.From.Id;

            if (IsStartMessage(userMessage))
            {
                Guid fxstreetUserId = ExtractFXStreetUserId(userMessage);
                if (fxstreetUserId != null)
                {                   
                    var responseMessage = CreateUserResponseTelegramMessage(telegramUserId, fxstreetUserId);
                    bool respose = await SendMessageToTelegram(responseMessage);
                }
            }
            if (IsStatsMessage(userMessage))
            {
                try
                {
                    int? days = ExtractDays(userMessage);

                    if (days.HasValue)
                    {
                        var stats = await GetStatsFromApi(days.Value);
                        var responseMessage = CreateStatResponseTelegramMessage(telegramUserId, stats, days.Value);
                        bool respose = await SendMessageToTelegram(responseMessage);
                       
                    }
                    else
                    {
                        days = 7;
                        var stats = await GetStatsFromApi(days.Value);
                        var responseMessage = CreateStatResponseTelegramMessage(telegramUserId, stats, days.Value);
                        bool respose = await SendMessageToTelegram(responseMessage);
                    }
                }
                catch (Exception)
                {
                    return new JsonResult(telegramMessage);
                }


            }
            return new JsonResult(telegramMessage);          
        }

        private static async Task<TradeStatResponse> GetStatsFromApi(int days)
        {
            var client = new RestClient("https://signalapi.fxstreet.com");            

            var request = new RestRequest($"/api/v1/Stats/summary?serverName=Ctrader-ICMarkets-MarketImpact&days={days}", DataFormat.Json);

            var tradeStatResponse = await client.GetAsync<TradeStatResponse>(request);

            return tradeStatResponse;
        }

        private static int? ExtractDays(string userMessage)
        {
            int? days = null; 
            try
            {
                days = int.Parse(userMessage.Substring(6));
            }
            catch (Exception ex)
            {
                logger.LogError("Something went wrong parsing the int", ex);
            }

            return days;
        }

        private static TelegramResponse CreateStatResponseTelegramMessage(string telegramUserId, TradeStatResponse tradeStats, int days)
        {
            return new TelegramResponse()
            {
                ChatId = int.Parse(telegramUserId),
                Text = $"In the last {days} days we have done {tradeStats.NumberOfTrades} trades with a winning ratio {tradeStats.WinRate}%"
            };
            
        }

        private static async Task<string> StreamToStringAsync(this HttpRequest request)
        {
            using (var sr = new StreamReader(request.Body))
            {
                return await sr.ReadToEndAsync();
            }
        }

        private static bool IsStatsMessage(string message)
        {
            return message.StartsWith("/stats");
        }
        private static async Task<bool> SendMessageToTelegram(TelegramResponse telegramResponse)
        {          
            var botClient = new TelegramBotClient(token);

            await botClient.SendTextMessageAsync(chatId: telegramResponse.ChatId, text: telegramResponse.Text);

            return true;

        }

        private static async Task<bool> SendPoll()
        {
            Message pollMessage = await iTelegramBotClient.SendPollAsync(
                chatId: "@group_or_channel_username",
                question: "Did you ever hear the tragedy of Darth Plagueis The Wise?"
                , options: new[] { "Yes for the hundredth time!", "No, who`s that?" });

            return true;

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
                logger.LogError("Something went wrong parsing the guid", ex);
            }

            return fxstreetUserId;

        }

        private static TelegramResponse CreateUserResponseTelegramMessage(string telegramUserId, Guid fxstreetUserId)
        {
            return new TelegramResponse()
            {
                ChatId = int.Parse(telegramUserId),
                Text = $"Let's begin with some basic questions to recommend the most profitable signals for your type of trading"
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

    public class TradeStatResponse
    {
        public TimeSpan AverageDuration { get; set; }
        public double NumberOfTrades { get; set; }
        public decimal? NetProfit { get; set; }
        public decimal WinRate { get; set; }
        public decimal LossRate { get; set; }
        public double NumberOfWinningTrades { get; set; }
        public double NumberOfLosingTrades { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossLoss { get; set; }
        public decimal AverageProfit { get; set; }
        public decimal AverageLoss { get; set; }
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