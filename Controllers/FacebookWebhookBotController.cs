using lotus_blue.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using lotus_blue.Models.WebHooksViewModel;
using lotus_blue.Models;
using Microsoft.EntityFrameworkCore;
using lotus_blue.Data;
using lotus_blue.Services;
using lotus_blue.Models;
using System.Diagnostics.Metrics;
using NuGet.Protocol.Plugins;
using System.Web;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace lotus_blue.Controllers
{
    [Route("api/facebook/webhook")]
    [ApiController]
    public class FacebookWebhookBotController : Controller
    {

        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly HashSet<string> ProcessedComments = new(); // Cache for processed comment IDs
        private static readonly Dictionary<string, UserViewModel> TrackedUsers = new();  // Dictionary to store tracked users by conversationId
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly ApplicationDbContext _context;

        // Facebook Pages Configuration
        public static readonly Dictionary<string, PageConfig> PAGES_CONFIG = new()
        {
         { "104568275507926", new PageConfig("EAAN4UYOC8OoBOx8cVnf3hpBJOXjo4ZCmCc15zQjLxmr2N3bzCNpYbqW18y8La6jU6fIQWuM0J7n7GdaXaZA77ZAZAbkEVIc7epmTLn3YfgeEPz8CnCpZAvDnDIGV7WbSA1PEN44R4B85ATV6TMyLg6OJCGbW2KqZA47060LqxaZB24vT0xgZA84FI4i5HkA3YPn83oZBpYcn8mDdDlr7Bup8ZD\r\n\r\n\r\n", new List<string> {  "تم الرد على الخاص", "شكرًا لتواصلك معنا!" }, "مرحبا معك سالي خبيرة التجميل من شركة مونلايت بخصوص توب باودر ماهو إستفسارك؟", "Moonlight" , "/FacebookWebhook/moonlight.png",false)},
         { "100736899432829", new PageConfig("EAAN4UYOC8OoBO5z7neZCiHc5ZAzK7rGZBRqfKqt2Ne9nhlT2fSCYftZBfyto0mbis263ssp942aaXZCURLcITeyenE1ZBgKZCLjpoScD3IA5e4282OZBmrAFFERO5GjpDFMCz4DBWeWRwlkWJea7ZAM79cPeZAhGqbux194OG81ZCuFZAMx0ZBJ9fuMHWksPDADD4HZAzUm5ioVwIKo2sZD\r\n", new List<string> { "تم الرد على الخاص ", "تم التواصل على الخاص " }, "مرحبا ماهو استفسارك بخصوص المشد السحري ؟", "Loxx King Woman", "/FacebookWebhook/loxkingman.png",false )},
         { "101509818947526", new PageConfig("EAAN4UYOC8OoBO2pYbTrQsC0fKLSoTZBXK1d6tOUXp8vFWYMx9Wf23zd3dNHuvrFcGAGKP0wBhRrIF17bL1RNfzRxOGckpLxDl9ZBtsPJbbBcmOw9Uej7j1sRkFYEOUYxjkAJhy4AA8jZB0I8OKhU2TIYHTpKkTfgWJDXZALZB7fXSINcOVzPSWibXZAT88tU9XuyzvZAIYBKZCcZD\r\n\r\n", new List<string> {"تم الأجابة على الخاص", " تم الرد خاص ", "  تم التواصل على الخاص عيني 💙" }, "  مرحبا بخصوص  الفاونديشن ماهو استفسارك \U0001f970؟؟\r\n  ", "Lotus Blue", "/FacebookWebhook/lotusblue.png",false) },
       // { "104710089055383", new PageConfig("EAAN4UYOC8OoBO5rHltJa7qYEdiTA6vaD1XdwEOEOJZCBuSGolzkZA08ZCEXRQ2ylvkxuLJ4QQqI2xgpTAP64nz1fxt9qtk55jR8cizUthjQRPPZASRS6wNzTyORsSfG2VHPLlGl816Twae8VVuyHqCT7FRwLNgN7KzDmvUxthF3lYRmVqyZBPK12KYdDyl6UlpOnlRIzHtbNCZBNAZD", new List<string> { "تم الاجابة", "تم التواصل معك" }, "ما هو استفسارك بخصوص المسكارا؟", "Beauty Center", "/SocialMedia/moonglight.png",false) },
        { "109230591424440", new PageConfig("EAAN4UYOC8OoBO1DOH2rQxXgVtmpVNwXI2onvt2YTzwyK75OgXBacTR9iJW2jKhZBgieZBc6ebWh2BqKjtu9lHRsXu0RCYYtSA2WadzDO9zsv4IJWCfmI0ZAJLV3e5EXHNQJeHVFMZBMmJxswMaMI5J1vlbNWTjkBFBkWn5Sm3DCkL7XZAHPmcCPqqrYltZAaaGdgCnYcnXw6JZA6ZCRN9BO4nuUZD\r\n", new List<string> { " تم الرد على الخاص ", " تم التواصل على الخاص" }, " مرحبا معك رهف اخصائية الشعر و التجميل من فلير بخصوص شامبو فلير ماهو استفسارك ؟؟❤❤", "Flare Woman" , "/FacebookWebhook/flareartboard.png",false) },
       { "211839025349185", new PageConfig("EAAN4UYOC8OoBO71oOJp36t8aThZAp5ZBb8ND1UW2ejv6mZCS966OrcIfNL3l7TUyZC6BZAKIIpItJp98LOIU4KcgGjciuQTm9fwA4xZCeZBIbW0FXbREWwDnzwt9ZAZAzW4UIM6UjpoOhm9kO83bw0dgGnfd0gTezJJGJuPZARotP4WKlCBTjVAbVlJaEdlLPyTMC95Gcvg43aejlGe7QZD", new List<string> { " تم الرد على الخاص ", " تم التواصل على الخاص" }, "مرحبا ماهو استفسارك بخصوص المشد السحري ؟", "Loxx King Man", "/FacebookWebhook/loxkingman.png",true )},
        { "416441791559844", new PageConfig("EAAN4UYOC8OoBO95ZAZBxBv7w78xl2ZB6OaushKmfqQ6Juq1yu8lutMTUeyn9qZBkEidfChPsoiGZB2lmbH9RKIo3eUNDysK0WvR2boO2EZCzUi9D3y8BHMFnB9ABRZAXAp0Gz75VTZCpFMjEa44wOoGKmie7hjMEwmIL9tunBQc8haZCwmd0PWMxMFayWRFHMjMoc7cZAGdc68ZBQLZB\r\n\r\n\r\n", new List<string> { " تم الرد على الخاص ", " تم التواصل على الخاص" }, " مرحبا معك محمد اخصائي الشعر  من فلير بخصوص شامبو فلير ماهو استفسارك ؟؟❤❤", "Flare Man" , "/FacebookWebhook/flareartboard.png",true) },

        };

        private readonly IHubContext<MessageHub> _hubContext;

        public FacebookWebhookBotController(IHubContext<MessageHub> hubContext, ApplicationDbContext context, GetCurrentTimeInIstanbul timeService)
        {
            _hubContext = hubContext;
            _context = context;
            _timeService = timeService;
        }
        // webhook verfication
        [HttpGet]
        public IActionResult VerifyWebhook(
         [FromQuery(Name = "hub.mode")] string hubMode,
         [FromQuery(Name = "hub.challenge")] string hubChallenge,
         [FromQuery(Name = "hub.verify_token")] string hubVerifyToken)
        {
            const string VERIFY_TOKEN = "myVerifyToken123";

            if (hubVerifyToken == VERIFY_TOKEN && hubMode == "subscribe")
                return Ok(hubChallenge);

            return BadRequest("Invalid verification token or mode.");
        }

        public async Task TriggerInstantMessageAsync(string conversationId, string pageId, string recipientId, int countryId)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(pageId) || string.IsNullOrEmpty(recipientId))
                {
                    Console.WriteLine("Invalid data for instant message.");
                    return;
                }

                Console.WriteLine($"Triggering instant message for conversation {conversationId}");

                // Page-specific price mappings
                var pagePrices = new Dictionary<string, (Dictionary<int, string> Prices, string Message, string FollowUp)>
        {
            {
                "104568275507926", // Moonlight page ID
                (
                    Prices: new Dictionary<int, string>
                    {
                        { 1, "22 الف" },
                        { 2, "130 درهم" },
                        { 4, "130 دينار" },
                        { 5, "15 ريال" },
                        { 9, "15 دينار" },
                        { 10, "15 دينار" },
                        { 7, "500 ليرة" }
                    },
                    Message: @"اهلا وسهلا حبيبتي بشركة moon light نورتينا ✨
المنتج طبي اصلي
يخفي عيوب البشرة بشكل كامل
ثابت على بشرتك لمدة 24 ساعة
مقاوم للماء و التعرق ولا يترك اي تكتلات على البشرة",
                    FollowUp: "ممكن اعرف لون بشرتك عزيزتي 🧏🏻‍♀️ ؟"
                )
            },
            {
                "100736899432829", // Loxx King Woman page ID
                (
                    Prices: new Dictionary<int, string>
                    {
                        { 1, "21 الف" },
                        { 2, "130 درهم" },
                        { 4, "130 دينار" },
                        { 5, "15 ريال" },
                        { 9, "15 دينار" },
                        { 10, "15 دينار" },
                        { 7, "600 ليرة" },
                        { 6, "80 شيكل" },
                        { 3, "120 ريال" }
                    },
                    Message: @"اهلاو سهلا عزيزتي
المشد السحري يعمل على التخلص من الترهلات و الدهون الزائدة حول البطن
يعمل على شد عضلات البطن العليا و السفلى
يعمل على اعادة تشكيل الخصر + نحت الجوانب
يعمل على استقامة الظهر و تقليل الم  الظهر
المشد يساعد على التخلص من دهون وترهلات ما بعد الولادة",
                    FollowUp: "فيني اعرف من شو بتعاني حضرتك من ترهلات او دهون على البطن ؟"
                )
            },
            {
                "211839025349185", // Loxx King Man page ID
                (
                    Prices: new Dictionary<int, string>
                    {
                        { 1, "21 الف" },
                        { 2, "130 درهم" },
                        { 4, "130 دينار" },
                        { 5, "15 ريال" },
                        { 9, "15 دينار" },
                        { 10, "15 دينار" },
                        { 7, "600 ليرة" },
                        { 6, "80 شيكل" },
                        { 3, "120 ريال" }
                    },
                    Message: @"اهلا وسهلا عزيزي
المشد السحري يعمل على التخلص من الترهلات و الدهون الزائدة حول البطن
يعمل على اعادة تشكيل الخصر + نحت الجوانب
يعمل على شد عضلات البطن العليا و السفلى
يعمل على استقامة الظهر و تقليل الم  الظهر
يساعد على تقوية النشاط الحراري في الجسم",
                    FollowUp: "فيني اعرف من شو بتعاني حضرتك من ترهلات او دهون على البطن ؟"
                )
            },
            {
                "101509818947526", // Lotusblue Man page ID
                (
                    Prices: new Dictionary<int, string>
                    {
                        { 1, "23 الف" },
                        { 2, "150 درهم" },
                        { 4, "150 دينار" },
                        { 5, "17 ريال" },
                        { 9, "15 دينار" },
                        { 10, "15 دينار" },
                        { 7, "600 ليرة" }
                    },
                    Message: @"🥰اهلا و سهلا نورتي❤️
معك اخصائية التجميل شروق  من شركة لوتس بلو
الفاونديشن طبي اصلي
يخفي الكلف و التصبغات و الهالات السوداء و النمش والحفر
مناسب لبشرة الدهنية و الجافة و المختلطة
لا يترك اي تكتلات على البشرة",
                    FollowUp: "بشرتك بيضاء ام متوسطة ام سمراء ؟"
                )
            }
        };

                // Retrieve page-specific data
                if (!pagePrices.TryGetValue(pageId, out var pageData))
                {
                    Console.WriteLine($"No predefined message for pageId: {pageId}.");
                    return;
                }

                // Construct the multi-line message
                var messageBuilder = new StringBuilder(pageData.Message);
                if (pageData.Prices.TryGetValue(countryId, out var price))
                {
                    messageBuilder.AppendLine($"السعر {price}");
                }
                else
                {
                    messageBuilder.AppendLine("عذراً، السعر غير متوفر لبلدك.");
                }
                messageBuilder.AppendLine(pageData.FollowUp);

                // Split the message into lines
                var lines = messageBuilder.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

                // Send each message and save to the database
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        await SendMessageToQueueConversation(conversationId, recipientId, pageId, trimmedLine);
                    }
                }

                Console.WriteLine($"Instant message triggered and saved for conversation {conversationId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TriggerInstantMessageAsync: {ex.Message}");
            }
        }

        private async Task SendMessageToQueueConversation(string conversationId, string recipientId, string pageId, string text)
        {
            // Validate inputs
            if (string.IsNullOrEmpty(conversationId))
            {

                Console.WriteLine($"Error: Conversation ID is missing.");

                return;
            }

            if (string.IsNullOrEmpty(recipientId))
            {

                Console.WriteLine($"Error: Recipient ID is missing for conversation {conversationId}.");

                return;
            }

            if (string.IsNullOrEmpty(pageId))
            {

                Console.WriteLine($"Error: Page ID is missing.");

                return;
            }

            // Prepare payload in the required format
            var payload = new
            {
                text,               // The message text
                pageId,             // The page ID associated with the message
                recipientId,        // The recipient's ID
                isFromUser = false  // Indicating the message is not from the user
            };

            Console.WriteLine($"Preparing to send message for conversation {conversationId} with payload: {JsonConvert.SerializeObject(payload)}");

            try
            {
                var jsonPayload = JsonConvert.SerializeObject(payload);
                var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Send the POST request
                using var httpClient = new HttpClient
                {
                    BaseAddress = new Uri("https://f570b//1ec8254.ngrok.app/") // Replace with your actual base URL
                };

                var response = await httpClient.PostAsync($"api/facebook/webhook/conversations/{conversationId}/messages", httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to send message for conversation {conversationId}. Status: {response.StatusCode}, Error: {errorContent}");
                }
                else
                {
                    Console.WriteLine($"Message sent successfully for conversation {conversationId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendMessageToQueueConversation for conversation {conversationId}: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveWebhook([FromBody] JObject data)
        {
            try
            {
                // Log the full payload for debugging
                Console.WriteLine($"Webhook triggered: {data.ToString(Formatting.Indented)}");

                // Extract entries from the webhook payload
                var entries = data["entry"]?.ToObject<List<JObject>>();
                if (entries == null || !entries.Any())
                {
                    Console.WriteLine("No valid entry found in the webhook payload.");
                    return Ok("No entry found");
                }

                foreach (var entry in entries)
                {
                    string pageId = entry["id"]?.ToString()?.Trim();

                    if (string.IsNullOrEmpty(pageId) || !PAGES_CONFIG.ContainsKey(pageId))
                    {
                        Console.WriteLine($"Invalid or unconfigured page ID: {pageId}");
                        continue;
                    }

                    // Log the entire entry for debugging
                    Console.WriteLine($"Processing entry for Page ID: {pageId}");
                    Console.WriteLine($"Entry Data: {entry.ToString(Formatting.Indented)}");
                    Console.WriteLine($"Processing entry for Page ID: {pageId}");
                    Console.WriteLine($"Entry Data: {entry.ToString(Formatting.Indented)}"); Console.WriteLine($"Processing entry for Page ID: {pageId}");
                    Console.WriteLine($"Entry Data: {entry.ToString(Formatting.Indented)}"); Console.WriteLine($"Processing entry for Page ID: {pageId}");
                    Console.WriteLine($"Entry Data: {entry.ToString(Formatting.Indented)}");
                    // Check for feed changes (like comments)
                    var changes = entry["changes"]?.ToObject<List<JObject>>();
                    if (changes != null && changes.Any())
                    {
                        Console.WriteLine($"Found {changes.Count} changes in the entry.");
                        foreach (var change in changes)
                        {
                            Console.WriteLine($"Processing change: {change.ToString(Formatting.Indented)}");

                            if (change["field"]?.ToString() == "feed" &&
                                change["value"]?["item"]?.ToString() == "comment")
                            {
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");
                                Console.WriteLine("Received a new comment event.");

                                await HandleComment(change["value"], pageId);
                            }
                            else
                            {
                                Console.WriteLine("Change is not a comment event.");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No changes found in the entry.");
                    }
                }

                return Ok("Event received");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        //[HttpPost]
        //public async Task<IActionResult> ReceiveWebhook([FromBody] JObject data)
        //{
        //    try
        //    {
        //        // Log the full payload for debugging
        //        Console.WriteLine($"[ReceiveWebhook] Full payload: {data}");

        //        // Extract entries from the webhook payload
        //        var entries = data["entry"]?.ToObject<List<JObject>>();
        //        if (entries == null || !entries.Any())
        //        {
        //            Console.WriteLine("[ReceiveWebhook] No valid entry found in the payload.");
        //            return Ok("No entry found");
        //        }

        //        foreach (var entry in entries)
        //        {
        //            string pageId = entry["id"]?.ToString()?.Trim();
        //            Console.WriteLine($"[ReceiveWebhook] Processing entry for Page ID: {pageId}");

        //            var messagingEvents = entry["messaging"]?.ToObject<List<JToken>>();


        //            var changes = entry["changes"]?.ToObject<List<JObject>>();
        //            if (changes != null && changes.Any())
        //            {
        //                foreach (var change in changes)
        //                {
        //                    Console.WriteLine("Processing feed change event:");
        //                    Console.WriteLine(change.ToString(Formatting.Indented));

        //                    if (change["field"]?.ToString() == "feed" && change["value"]?["item"]?.ToString() == "comment")
        //                    {
        //                        Console.WriteLine("Received a new comment event.");
        //                        await HandleComment(change["value"], pageId);
        //                    }
        //                }
        //            }



        //            if (messagingEvents != null && messagingEvents.Any())
        //            {
        //                foreach (var messageData in messagingEvents)
        //                {
        //                    // Extract sender ID (if available)
        //                    string? senderId = messageData["sender"]?["id"]?.ToString();
        //                    if (string.IsNullOrEmpty(senderId))
        //                    {
        //                        Console.WriteLine("[ReceiveWebhook] Missing senderId. Skipping.");
        //                        continue;
        //                    }

        //                    // Extract referral object
        //                    var referral = messageData["referral"];
        //                    string? rawRef = referral?["ref"]?.ToString();

        //                    Common.Countries? detectedCountry = null;
        //                    string? productCode = null;

        //                    if (referral != null && !string.IsNullOrEmpty(rawRef))
        //                    {
        //                        // Parse the `country_product` format
        //                        var parts = rawRef.Split('_');
        //                        if (parts.Length >= 2)
        //                        {
        //                            string countryCode = parts[0];
        //                            productCode = parts[1];

        //                            // Map country code to enum
        //                            detectedCountry = countryCode switch
        //                            {
        //                                "libya" => Common.Countries.ليبيا,
        //                                "iraq" => Common.Countries.العراق,
        //                                "uae" => Common.Countries.الإمارات,
        //                                "qatar" => Common.Countries.قطر,
        //                                "oman" => Common.Countries.سلطنة_عمان,
        //                                "palestine" => Common.Countries.فلسطين,
        //                                "turkey" => Common.Countries.تركيا,
        //                                "jordan" => Common.Countries.الأردن,
        //                                "kuwait" => Common.Countries.الكويت,
        //                                "bahrain" => Common.Countries.البحرين,
        //                                "saudi" => Common.Countries.السعودية,
        //                                "tunisia" => Common.Countries.تونس,
        //                                "morocco" => Common.Countries.المغرب,
        //                                "algeria" => Common.Countries.الجزائر,
        //                                "lebanon" => Common.Countries.لبنان,
        //                                _ => null
        //                            };
        //                        }
        //                        else
        //                        {
        //                            Console.WriteLine($"[ReceiveWebhook] Invalid ref format: {rawRef}");
        //                        }
        //                    }
        //                    else
        //                    {
        //                        Console.WriteLine("[ReceiveWebhook] Referral or rawRef is missing, continuing without country and product.");
        //                    }

        //                    // Process the message regardless of country or product
        //                    bool isEcho = messageData["message"]?["is_echo"]?.ToObject<bool>() ?? false;
        //                    Console.WriteLine($"Is Echo? {isEcho}");
        //                    await HandleMessage(messageData, pageId, isEcho, detectedCountry, productCode);
        //                }
        //            }
        //        }

        //        return Ok("Event received");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in ReceiveWebhook: {ex.Message}");
        //        return StatusCode(500, $"Internal server error: {ex.Message}");
        //    }
        //}


        // Unified message handler
        private async Task HandleMessage(JToken messageData, string pageId, bool isEcho, Common.Countries? detectedCountry, string? productParameter)
        {
            try
            {
                // Extract message details
                string senderId = messageData["sender"]?["id"]?.ToString();
                string messageText = messageData["message"]?["text"]?.ToString();
                string messageId = messageData["message"]?["mid"]?.ToString() ?? Guid.NewGuid().ToString();

                Console.WriteLine($"[HandleMessage] Processing message: SenderId={senderId}, MessageId={messageId}, PageId={pageId}");

                //// Skip saving if messageData contains invalid or empty message text
                //if (string.IsNullOrEmpty(messageText))
                //{
                //    Console.WriteLine("[HandleMessage] Message text is null or empty. Skipping save.");
                //    return;
                //}

                // Extract timestamp
                DateTime timestamp = DateTimeOffset
                    .FromUnixTimeMilliseconds((long)messageData["timestamp"])
                    .UtcDateTime.AddHours(3);



                bool isFromUser = !isEcho;
                string userId = isFromUser ? senderId : messageData["recipient"]?["id"]?.ToString();
                string recipientId = isFromUser ? pageId : userId;
                string conversationId = $"{pageId}_{userId}";

                // Retrieve gender from PAGES_CONFIG based on pageId
                bool gender = PAGES_CONFIG.ContainsKey(pageId) ? PAGES_CONFIG[pageId].Gender : false; // Default to false if not found


                // Retrieve or create conversation
                var conversation = await _context.SocialMediaConversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                bool isNewConversation = conversation == null;
                var nextLuxiraUserId = (_context.SocialMediaConversations.Max(c => (int?)c.LuxiraUserId) ?? 0) + 1;

                if (isNewConversation)
                {
                    conversation = new SocialMediaConversation
                    {
                        Id = conversationId,
                        UserId = userId,
                        UserName = isFromUser ? "User" : PAGES_CONFIG[pageId]?.PageName ?? "Unknown",
                        PageId = pageId,
                        PageName = PAGES_CONFIG[pageId]?.PageName,
                        Gender = gender, // Save gender in the conversation
                        IsArchived = false,
                        IsOrder = false,
                        IsRead = false,
                        SocialMediaType = SocialMediaType.Facebook,
                        CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                        UpdatedAt =_timeService.GetIstanbulTimeWithOffset(),
                        LuxiraUserId = nextLuxiraUserId,
                        ProductName = productParameter,
                        Country = detectedCountry
                    };

                    _context.SocialMediaConversations.Add(conversation);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    conversation.IsRead = !isFromUser;
                    conversation.UpdatedAt =_timeService.GetIstanbulTimeWithOffset();
                    await _context.SaveChangesAsync();
                }

                // Skip message save if the text is empty
                if (string.IsNullOrEmpty(messageText))
                {
                    Console.WriteLine("[HandleMessage] Message text is empty. Skipping message save.");
                }
                else
                {
                    // Save the new message
                    var newMessage = new SocialMediaMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        SocialMediaConversationId = conversation.Id,
                        MessageId = messageId,
                        SenderId = senderId,
                        SenderName = isFromUser ? conversation.UserName : PAGES_CONFIG[pageId]?.PageName,
                        ReceiverId = recipientId,
                        ReceiverName = isFromUser ? PAGES_CONFIG[pageId]?.PageName : conversation.UserName,
                        Text = messageText,
                        IsFromUser = isFromUser,
                        Timestamp = timestamp,
                        IsRead = !isFromUser
                    };

                    _context.SocialMediaMessages.Add(newMessage);
                    await _context.SaveChangesAsync();
                }

                // Send SignalR events for UI updates only if the message is not an echo
              
                    var eventData = new
                    {
                        conversationid = conversation.Id,
                        pagename = conversation.PageName,
                        pageid = pageId,
                        userid = userId,
                        username = conversation.UserName,
                        latestmessage = messageText ?? "No message",
                        lastmessagetimestamp = timestamp.ToString("HH:mm"),
                        unreadMessagesCount = await _context.SocialMediaMessages
                            .CountAsync(m => m.SocialMediaConversationId == conversation.Id && !m.IsRead),
                        countryname = conversation.Country?.ToString(),
                        productname = conversation.ProductName,
                        gendername = conversation.Gender ? "ذكر" : "أنثى"
                    };

                    string signalRMethod = isNewConversation ? "NewConversation" : "UpdateConversationList";
                    await _hubContext.Clients.All.SendAsync(signalRMethod, eventData);

                    // Notify conversation group of new message
                    if (!string.IsNullOrEmpty(messageText))
                    {
                        await _hubContext.Clients.Group(conversation.Id).SendAsync("ReceiveMessage", new
                        {
                            conversationid = conversation.Id,
                            sendername = isFromUser ? "User" : PAGES_CONFIG[pageId]?.PageName,
                            message = messageText,
                            pagename = conversation.PageName,
                            pageid = pageId,
                            isFromUser,
                            recipientid = recipientId,
                            timestamp = timestamp.ToString("HH:mm")
                        });
                    }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleMessage: {ex.Message}");
            }
        }



        // Retrieve conversation by composite Id
        private async Task<SocialMediaConversation> GetConversationById(string pageId, string userId)
        {
            // Form the composite Id as `{PageId}_{UserId}`
            var conversationId = $"{pageId}_{userId}";

            // Retrieve the conversation by this composite Id
            return await _context.SocialMediaConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);
        }

        // Method to calculate and broadcast total unread messages
        private async Task BroadcastTotalUnreadMessages()
        {
            var totalUnreadMessages = await _context.SocialMediaMessages.CountAsync(m => !m.IsRead);
            await _hubContext.Clients.All.SendAsync("TotalUnreadMessages", totalUnreadMessages);
        }

        // view page 
        [HttpGet("conversations")]
        public async Task<IActionResult> Conversations()
        {

            return View();
        }


        [HttpGet("allconversations")]
        public async Task<IActionResult> GetAllConversations(
    [FromQuery] string? search = null,
    [FromQuery] bool? isOrder = null,
    [FromQuery] bool? isRead = null,
    [FromQuery] string? pageId = null,
    [FromQuery] int? country = null,
    [FromQuery] bool? showUndefinedOrder = null,
    [FromQuery] bool? isOfferSent = null // ✅ New parameter
)
        // Add optional country parameter


        {
            var query = _context.SocialMediaConversations
                .Include(c => c.Messages)
                .OrderBy(c => c.UpdatedAt) // Change to ascending order
                .AsQueryable();

            // Apply filters if they are provided
            if (isOrder.HasValue)
            {
                query = query.Where(c => c.IsOrder == isOrder.Value);
            }
            if (isRead.HasValue)
            {
                query = query.Where(c => c.Messages.Any(m => m.IsRead == isRead.Value));
            }
            if (!string.IsNullOrEmpty(pageId))
            {
                query = query.Where(c => c.PageId == pageId); // Filter by pageId
            }

            // ✅ Filter for undefined country or product name
            if (showUndefinedOrder == true)
            {
                query = query.Where(c => c.Country == null || string.IsNullOrEmpty(c.ProductName));
            }

            // Apply country filter
            if (country.HasValue)
            {
                query = query.Where(c => (int)c.Country == country.Value); // Filter by country ID
            }

            // ✅ Filter by isOfferSent
            if (isOfferSent.HasValue)
            {
                query = query.Where(c => c.IsOfferSent == isOfferSent.Value);
            }

            // ✅ Filter for undefined country or product name
            if (showUndefinedOrder == true)
            {
                query = query.Where(c => c.Country == null || string.IsNullOrEmpty(c.ProductName));
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(search))
            {
                string lowerSearch = search.ToLower(); // Case-insensitive search
                query = query.Where(c =>
                    c.Messages.Any(m => m.Text.ToLower().Contains(lowerSearch)) || // Search in message text
                    c.UserName.ToLower().Contains(lowerSearch)); // Search in sender name
            }

            var conversationsData = await query
                .Select(c => new
                {
                    isOrder = c.IsOrder,
                    isRead= c.IsRead,
                    conversationid = c.Id,
                    pageid = c.PageId ?? "Unknown Page",
                    username = c.UserName ?? "Unknown User",
                    userid = c.UserId,
                    pagename = c.PageName ?? "Unknown Page",
                    unreadMessagesCount = c.Messages.Count(m => !m.IsRead),
                    lastmessage = c.Messages.OrderByDescending(m => m.Timestamp).Select(m => m.Text).FirstOrDefault() ?? "No message yet",
                    lastmessagetimestamp = c.Messages.OrderByDescending(m => m.Timestamp).Select(m => m.Timestamp).FirstOrDefault(),
                    pageIdKey = c.PageId,
                    country = c.Country,
                    luxirauserId = c.LuxiraUserId,
                    productname = c.ProductName,
                    gendername = c.Gender ? "ذكر" : "أنثى"
        })
                .ToListAsync();

            var conversations = conversationsData.Select(c => new
            {
                c.conversationid,
                c.pageid,
                c.username,
                c.userid,
                c.pagename,
                c.isRead,
                c.unreadMessagesCount,
                c.lastmessage,
                lastmessagetimestamp = c.lastmessagetimestamp.ToString("HH:mm"),
                pageImage = FacebookWebhookBotController.PAGES_CONFIG.ContainsKey(c.pageIdKey)
                    ? FacebookWebhookBotController.PAGES_CONFIG[c.pageIdKey].PagePhoto
                    : "/path/to/default-image.png",
                country = c.country.HasValue ? (int)c.country.Value : (int?)null,
                countryname = c.country?.ToString() ?? "بلد غير معروف",
                countryflag = c.country.HasValue ? Common.GetImageUrlByCountryName(c.country.ToString()) : "/path/to/default-flag.png",
                luxirauserId = c.luxirauserId,
                productname = c.productname,
                gendername=c.gendername,
                isOrder = c.isOrder,
            }).ToList();

            return Json(conversations);
        }


        // Action to toggle `IsOrder` status
        [HttpPost("toggle-order/{conversationId}")]
        public async Task<IActionResult> ToggleOrderStatus(string conversationId)
        {
            var conversation = await _context.SocialMediaConversations.FindAsync(conversationId);
            if (conversation == null)
            {
                return NotFound();
            }

            conversation.IsOrder = !conversation.IsOrder;
            conversation.UpdatedAt = _timeService.GetIstanbulTimeWithOffset();
            await _context.SaveChangesAsync();

            // Return the updated status
            return Ok(new { isOrder = conversation.IsOrder, message = "Conversation order status toggled successfully." });
        }


        // Action to toggle `IsRead` status
        [HttpPost("toggle-read/{conversationId}")]
        public async Task<IActionResult> ToggleReadStatus(string conversationId)
        {
            try
            {
                var conversation = await _context.SocialMediaConversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (conversation == null)
                    return NotFound("Conversation not found.");

                // Toggle the IsRead status of the conversation only
                conversation.IsRead = !conversation.IsRead;

                await _context.SaveChangesAsync();

                // Broadcast the updated read status to all clients
                await _hubContext.Clients.All.SendAsync("UpdateReadStatus", new
                {
                    conversationId = conversationId,
                    isRead = conversation.IsRead
                });

                // Return a JSON response
                return Ok(new
                {
                    conversationId = conversationId,
                    isRead = conversation.IsRead,
                    message = $"Conversation marked as {(conversation.IsRead ? "read" : "unread")}."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ToggleReadStatus: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }




        // Set all messages in the conversation to is read
        [HttpPost("conversations/{id}/markAsRead")]
        public async Task<IActionResult> MarkConversationAsRead(string id)
        {
            try
            {
                var conversation = await _context.SocialMediaConversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (conversation == null)
                    return NotFound("Conversation not found.");

                var unreadMessages = conversation.Messages.Where(m => !m.IsRead).ToList();

                if (unreadMessages.Count == 0)
                    return Ok("All messages in the conversation are already marked as read.");

                // Mark each unread message as read
                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                }

                // Update the conversation's IsRead status
                conversation.IsRead = true;

                await _context.SaveChangesAsync();

                // Broadcast the updated read status to all clients
                await _hubContext.Clients.All.SendAsync("UpdateReadStatus", new
                {
                    conversationId = id,
                    isRead = true
                });

                return Ok("All messages in the conversation marked as read.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkConversationAsRead: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }





        // Get messages for a specific conversation
        [HttpGet("conversations/{id}/messages")]
        public async Task<IActionResult> GetMessages(string id)
        {
            try
            {
                // Retrieve messages from the database without the `SenderName` calculation
                var messages = await _context.SocialMediaMessages
                    .Where(m => m.SocialMediaConversationId == id)
                    .Include(m => m.Conversation) // Ensure Conversation is loaded
                    .OrderBy(m => m.Timestamp) // Order by timestamp in ascending order
                    .ToListAsync();

                // Filter out duplicate messages based on `MessageId`
                var distinctMessages = messages
                    .GroupBy(m => m.MessageId)
                    .Select(g => g.First()) // Select the first message from each group
                    .ToList();

                // Project the data to add `sendername` based on `IsFromUser` and `PAGES_CONFIG`
                var result = distinctMessages.Select(m => new
                {
                    id = m.Id,
                    messageid = m.MessageId,
                    senderid = m.SenderId,
                    text = m.Text,
                    IsFromUser = m.IsFromUser,
                    timestamp = m.Timestamp.ToString("HH:mm"),
                    sendername = m.IsFromUser
                                 ? "User"
                                 : PAGES_CONFIG.TryGetValue(m.Conversation.PageId, out var config)
                                    ? config.PageName
                                    : "Unknown Page"
                }).ToList();

                return Json(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMessages: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpPost("conversations/{id}/messages")]
        public async Task<IActionResult> SendMessage(string id, [FromBody] JObject payload)
        {
            try
            {
                Console.WriteLine($"Received SendMessage request for conversation ID: {id}");

                // Extract necessary data from the payload
                string text = payload["text"]?.ToString();
                string recipientId = payload["recipientId"]?.ToString();
                string pageId = payload["pageId"]?.ToString();

                Console.WriteLine($"Extracted data - Text: {text}, Recipient ID: {recipientId}, Page ID: {pageId}");

                Console.WriteLine($"Recipient ID: {recipientId}");
          
                // Validate required fields
                if (string.IsNullOrEmpty(recipientId))
                {
                    Console.WriteLine("Error: Recipient ID is required.");
                    return BadRequest("Recipient ID is required.");
                }

                if (!PAGES_CONFIG.TryGetValue(pageId, out var config))
                {
                    Console.WriteLine("Error: Invalid page configuration.");
                    return BadRequest("Invalid page configuration.");
                }

                // Prepare the message payload for the Facebook API
                var messagePayload = new
                {
                    recipient = new { id = recipientId },
                    message = new { text = text }
                };

                var jsonPayload = JsonConvert.SerializeObject(messagePayload);
                var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var requestUrl = $"https://graph.facebook.com/v17.0/me/messages?access_token={config.AccessToken}";
                Console.WriteLine($"Sending message to Facebook API: {requestUrl} with payload: {jsonPayload}");

                var response = await _httpClient.PostAsync(requestUrl, httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to send message. Status: {response.StatusCode}, Error: {errorContent}");
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                Console.WriteLine("Message sent successfully to Facebook API.");
                return Ok("Message sent successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }



        //// Efficient user name retrieval method.
        //private async Task<string> GetUserName(string userId, string accessToken)
        //{
        //    try
        //    {
        //        var requestUrl = $"https://graph.facebook.com/{userId}?fields=name&access_token={accessToken}";
        //        var response = await _httpClient.GetAsync(requestUrl);

        //        if (response.IsSuccessStatusCode)
        //        {
        //            var content = await response.Content.ReadAsStringAsync();
        //            var json = JObject.Parse(content);
        //            return json["name"]?.ToString() ?? "Unknown User";
        //        }
        //        else
        //        {
        //            Console.WriteLine($"Failed to fetch name for user ID: {userId}. Status: {response.StatusCode}");
        //            return "Unknown User";
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error fetching user name: {ex.Message}");
        //        return "Unknown User";
        //    }
        //}




        // bot comments 
        private async Task HandleComment(JToken change, string pageId)
        {
            try
            {
                if (!PAGES_CONFIG.TryGetValue(pageId, out var config))
                {
                    Console.WriteLine($"Page configuration not found for Page ID: {pageId}");
                    return;
                }

                string commentId = change["comment_id"]?.ToString();
                string fromId = change["from"]?["id"]?.ToString();

                // Skip if the comment was posted by the page itself.
                if (fromId == pageId)
                {
                    Console.WriteLine("Skipping reply to self-posted comment.");
                    return;
                }

                if (ProcessedComments.Contains(commentId))
                {
                    Console.WriteLine($"Skipping already processed comment ID: {commentId}");
                    return;
                }

                if (await PageHasAlreadyReplied(commentId, pageId))
                {
                    Console.WriteLine($"Page has already replied to comment ID: {commentId}. Skipping reply.");
                    return;
                }

                ProcessedComments.Add(commentId);
                Console.WriteLine($"Processing comment ID: {commentId} for Page: {config.PageName}");

                await SendPublicReply(commentId, pageId);
                await SendPrivateReply(commentId, pageId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleComment: {ex.Message}");
            }
        }

        private async Task<bool> PageHasAlreadyReplied(string commentId, string pageId)
        {
            try
            {
                // Make a GET request to fetch the replies to the comment.
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"https://graph.facebook.com/v17.0/{commentId}/comments?access_token={PAGES_CONFIG[pageId].AccessToken}"
                );

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to fetch replies for comment ID: {commentId}");
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(content);

                // Check if the page ID is present in any of the replies.
                var replies = jsonResponse["data"]?.ToObject<List<JObject>>() ?? new List<JObject>();
                foreach (var reply in replies)
                {
                    string replyFromId = reply["from"]?["id"]?.ToString();
                    if (replyFromId == pageId)
                    {
                        // Page has already replied to this comment.
                        return true;
                    }
                }

                return false; // No reply from the page was found.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PageHasAlreadyReplied: {ex.Message}");
                return false;
            }
        }

        private async Task SendPublicReply(string commentId, string pageId)
        {
            try
            {
                // Retrieve the page configuration for the given pageId
                if (!PAGES_CONFIG.TryGetValue(pageId, out var config))
                {
                    Console.WriteLine($"Page configuration not found for Page ID: {pageId}");
                    return;
                }

                // Select a random public message from the configured list
                var random = new Random();
                string message = config.PublicMessages[random.Next(config.PublicMessages.Count)];

                // Prepare the HTTP request to send the reply
                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://graph.facebook.com/v17.0/{commentId}/comments")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(new { message }),
                        Encoding.UTF8, "application/json")
                };

                // Set the Authorization header with the access token
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken.Trim());

                // Send the request
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Public reply sent to comment ID: {commentId}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to send public reply: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending public reply: {ex.Message}");
            }
        }

        private async Task SendPrivateReply(string commentId, string pageId)
        {
            try
            {
                var config = PAGES_CONFIG[pageId];
                string message = config.PrivateMessage;

                // Prepare the request content as JSON
                var payload = new
                {
                    recipient = new { comment_id = commentId },
                    message = new { text = message }
                };

                var requestContent = new StringContent(
                    JsonConvert.SerializeObject(payload, Formatting.None),
                    Encoding.UTF8,
                    "application/json"
                );

                // Create the HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://graph.facebook.com/v17.0/me/messages")
                {
                    Content = requestContent
                };

                // Ensure the access token is properly trimmed to avoid header issues
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken.Trim());

                // Send the HTTP request
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Private reply sent to comment ID: {commentId}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to send private reply: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending private reply: {ex.Message}");
            }
        }

        // end of  bot comments 


        [HttpPost]
        [Route("updateCountry")]
        public async Task<IActionResult> UpdateCountry(string conversationId, int countryId)
        {
            if (string.IsNullOrEmpty(conversationId) || countryId <= 0)
            {
                return BadRequest("Invalid conversation ID or country ID.");
            }

            var conversation = await _context.SocialMediaConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return NotFound("Conversation not found.");
            }

            // Update the country
            conversation.Country = (Common.Countries)countryId;
            conversation.UpdatedAt =_timeService.GetIstanbulTimeWithOffset();

            _context.SocialMediaConversations.Update(conversation);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Country updated successfully." });
        }

        [HttpPost]
        [Route("updateProduct")]
        public async Task<IActionResult> UpdateProduct(string conversationId, string productName)
        {
            if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(productName))
            {
                return BadRequest("Invalid conversation ID or product name.");
            }

            var conversation = await _context.SocialMediaConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return NotFound("Conversation not found.");
            }

            // Update the product name
            conversation.ProductName = productName;
            conversation.UpdatedAt = _timeService.GetIstanbulTimeWithOffset();

            _context.SocialMediaConversations.Update(conversation);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Product updated successfully." });
        }


        // العروض 

        [HttpPost("send-offer-messages/{pageId}")]
        public async Task<IActionResult> SendOfferMessages(string pageId)
        {
            try
            {
                // Filter conversations by PageId and IsOrder = false and IsOfferSent = false
                var conversations = await _context.SocialMediaConversations
                    .Where(c => c.PageId == pageId && !c.IsOrder && !c.IsOfferSent)
                    .ToListAsync();

                if (!conversations.Any())
                {
                    return Ok("No conversations to send offers to.");
                }

                foreach (var conversation in conversations)
                {
                    // Generate message based on the Country property
                    string message = GetOfferMessageByCountryAndPage(pageId, conversation.Country);

                    if (string.IsNullOrEmpty(message))
                    {
                        Console.WriteLine($"No message template for country {conversation.Country}");
                        continue;
                    }

                    // Send the message
                    var payload = new
                    {
                        recipient = new { id = conversation.UserId },
                        message = new { text = message }
                    };

                    var jsonPayload = JsonConvert.SerializeObject(payload);
                    var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    if (!PAGES_CONFIG.TryGetValue(pageId, out var config))
                    {
                        return BadRequest("Invalid page configuration.");
                    }

                    var requestUrl = $"https://graph.facebook.com/v17.0/me/messages?access_token={config.AccessToken}";
                    var response = await _httpClient.PostAsync(requestUrl, httpContent);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Failed to send message to {conversation.UserId}. Error: {errorContent}");
                        continue;
                    }

                    // Mark the conversation as offer sent
                    conversation.IsOfferSent = true;
                    _context.SocialMediaConversations.Update(conversation);
                }

                // Save changes to the database
                await _context.SaveChangesAsync();

                return Ok("Offer messages sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        private string GetOfferMessageByCountryAndPage(string pageId, Common.Countries? country)
        {
            // flare woman 
            if (pageId == "109230591424440")
            {
                switch (country)
                {
                    case Common.Countries.ليبيا:
                        return "لا تفوتي فرصة اليوم واستفيدي من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\n\nاحصلي على عبوة واحدة بسعر خاص جدا 100 مع التوصيل \nوذلك شاملا لتكاليف الشحن والتوصيل! 💃🏽🚚😱\n\nأو\nأشتري عبوتين بسعر خاص أيضا 190 دينار فقط! 🥳💖\nولا تنسي أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيعي الفرصة! 📅🕐\n\nاحجزي الآن واستمتعي بشعر صحي وجميل، ولا تضيعي هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.العراق:
                        return "لا تفوتي فرصة اليوم واستفيدي من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\n\nاحصلي على عبوة واحدة بسعر خاص جدا 17 ألف دينار فقط💃🏽🚚😱\n أو \nأشتري عبوتين بسعر خاص أيضا 30 ألف دينار فقط! 🥳💖\nولا تنسي أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيعي الفرصة! 📅🕐\n\nاحجزي الآن واستمتعي بشعر صحي وجميل، ولا تضيعي هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.الإمارات:
                        return "لا تفوتي فرصة اليوم واستفيدي من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\nاحصلي على عبوة واحدة بسعر خاص جدا 80 درهم فقط، 💃🏽🚚😱\nأو\nأشتري عبوتين بسعر خاص أيضا 150 درهم فقط! 🥳💖\nولا تنسي أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيعي الفرصة! 📅🕐\n\nاحجزي الآن واستمتعي بشعر صحي وجميل، ولا تضيعي هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.سلطنة_عمان:
                        return "لا تفوتي فرصة اليوم واستفيدي من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\nاحصلي على عبوة واحدة بسعر خاص جدا 12 ريال فقط 💃🏽🚚😱\nأو\nأشتري عبوتين بسعر خاص أيضا 20 ريال فقط! 🥳💖\nولا تنسي أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيعي الفرصة! 📅🕐\n\nاحجزي الآن واستمتعي بشعر صحي وجميل، ولا تضيعي هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.تركيا:
                        return "لا تفوتي فرصة اليوم واستفيدي من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\nاحصلي على عبوة واحدة بسعر خاص جدا 400 ليرة فقط 💃🏽🚚😱\nأو\nأشتري عبوتين بسعر خاص أيضا 750 ليرة فقط! 🥳💖\nولا تنسي أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيعي الفرصة! 📅🕐\n\nاحجزي الآن واستمتعي بشعر صحي وجميل، ولا تضيعي هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.الكويت:
                        return "لا تفوتي فرصة اليوم واستفيدي من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\nاحصلي على عبوة واحدة بسعر خاص جدا 9 دينار فقط\nأو\nأشتري عبوتين بسعر خاص أيضا 16 دينار فقط! 🥳💖\nولا تنسي أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيعي الفرصة! 📅🕐\n\nاحجزي الآن واستمتعي بشعر صحي وجميل، ولا تضيعي هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.البحرين:
                        return "لا تفوتي فرصة اليوم واستفيدي من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\nاحصلي على عبوة واحدة بسعر خاص جدا 8 دينار فقط 💃🏽🚚😱\nأو\nأشتري عبوتين بسعر خاص أيضا 15 دينار فقط! 🥳💖\nولا تنسي أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيعي الفرصة! 📅🕐\n\nاحجزي الآن واستمتعي بشعر صحي وجميل، ولا تضيعي هذه الفرصة الرائعة! 💅🏻😘🤎";

                    default:
                        return string.Empty;
                }
            }

            // lotus blue 

            else if (pageId == "101509818947526")
            {
                switch (country)
                {
                    case Common.Countries.ليبيا:
                        return "عرض اليوم 😍\nالفاونديشن صار ب 100 دينار مع التوصيل 🥳🤩\nاطلبيه اليوم ولحقي حالك 💃🏻\nالعرض متاح لساعة فقط 🥺\nشو ناطرة؟ اطلبيه الآن و استغلي الفرصة 💃🏻❤️";

                    case Common.Countries.العراق:
                        return "عرض اليوم 😍\nالفاونديشن صار ب 20 ألف فقط مع التوصيل 🥳🤩\nاطلبيه اليوم ولحقي حالك 💃🏻\n…………………………. أو \nاشتري عبوتين فاونديشن بـ 35 ألف فقط 🥳🤩\nالعرض متاح لساعة فقط 🥺\nشو ناطرة؟ اطلبيه الآن و استغلي الفرصة 💃🏻❤️";

                    case Common.Countries.الإمارات:
                        return "عرض اليوم فقط 😍🤩\nالفاونديشن صار بـ 100 درهم فقط 🥳🤩\nاطلبيه اليوم ولحقي حالك 💃🏻\n…………………………. أو \nاشتري عبوتين فاونديشن بـ 200 درهم فقط 🥳🤩\nالعرض متاح لساعة فقط 🥺\nشو ناطرة؟ اطلبيه الآن و استغلي الفرصة 💃🏻❤️";

                    case Common.Countries.تركيا:
                        return "عرض اليوم 😍\nالفاونديشن صار بـ 500 ليرة 🥳🤩\nاطلبيه اليوم ولحقي حالك 💃🏻\nالعرض متاح لساعة فقط 🥺\nشو ناطرة؟ اطلبيه الآن و استغلي الفرصة 💃🏻❤️";

                    case Common.Countries.سلطنة_عمان:
                    case Common.Countries.الكويت:
                    case Common.Countries.البحرين:
                        return "عرض اليوم 😍\nالفاونديشن صار بـ 11 فقط 🥳🤩\nاطلبيه اليوم ولحقي حالك 💃🏻\n…………………………. أو \nاشتري عبوتين فاونديشن بـ 20 فقط 🥳🤩\nالعرض متاح لساعة فقط 🥺\nشو ناطرة؟ اطلبيه الآن و استغلي الفرصة 💃🏻❤️";

                    default:
                        return string.Empty;
                }
            }

            // Loxx King Man
            else if (pageId == "211839025349185")
            {
                switch (country)
                {
                    case Common.Countries.ليبيا:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيد من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 100 دينار مع التوصيل\nأو احصل على قطعتين بسعر 190 واستفيد منه 🥳\nلحق حالك على العرض لا يفوتك 💃";

                    case Common.Countries.الإمارات:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيد من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 100 درهم مع التوصيل\nأو احصل على قطعتين بسعر 190 واستفيد منه 🥳\nلحق حالك على العرض لا يفوتك 💃";

                    case Common.Countries.فلسطين:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيد من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 60 شيكل\nأو احصل على قطعتين بسعر 110 شيكل واستفيدي منه 🥳\nلحق حالك على العرض لا يفوتك 💃";

                    case Common.Countries.تركيا:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيد من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 450 ليرة مع التوصيل\nأو احصل على قطعتين بسعر 800 واستفيدي منه 🥳\nلحق حالك على العرض لا يفوتك 💃";

                    case Common.Countries.سلطنة_عمان:
                    case Common.Countries.البحرين:
                    case Common.Countries.الكويت:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيد من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 11\nأو احصل على قطعتين بسعر 18 واستفيد منه 🥳\nلحق حالك على العرض لا يفوتك 💃";

                    case Common.Countries.العراق:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيد من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 20 ألف مع التوصيل\nأو احصل على قطعتين بسعر 36 ألف واستفيد منه 🥳\nلحق حالك على العرض لا يفوتك 💃";

                    default:
                        return string.Empty;
                }
            }

            //Loxx King Woman
            else if (pageId == "100736899432829")
            {
                switch (country)
                {
                    case Common.Countries.ليبيا:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيدي من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 100 دينار مع التوصيل\nأو احصلي على قطعتين بسعر 190 واستفيدي منه 🥳\nلحقي حالك على العرض لا يفوتك 💃";

                    case Common.Countries.العراق:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيدي من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 20 ألف مع التوصيل\nأو احصلي على قطعتين بسعر 36 ألف واستفيدي منه 🥳\nلحقي حالك على العرض لا يفوتك 💃";

                    case Common.Countries.تركيا:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيدي من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 450 ليرة مع التوصيل\nأو احصلي على قطعتين بسعر 800 واستفيدي منه 🥳\nلحقي حالك على العرض لا يفوتك 💃";

                    case Common.Countries.فلسطين:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيد من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 60 شيكل\nأو احصل على قطعتين بسعر 110 شيكل واستفيدي منه 🥳\nلحق حالك على العرض لا يفوتك 💃";

                    case Common.Countries.قطر:
                        return "😍عرض قطر لوكس كينج😍\n\nعرض محدود ومميز من شركة LOOX KING الايطالية\nاستفيدي من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 100 ريال مع التوصيل\nأو احصلي على قطعتين بسعر 190 ريال واستفيدي منه 🥳\nلحقي حالك على العرض لا يفوتك 💃";


                    case Common.Countries.الإمارات:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيدي من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 100 درهم مع التوصيل\nأو احصلي على قطعتين بسعر 190 واستفيدي منه 🥳\nلحقي حالك على العرض لا يفوتك 💃";

                    case Common.Countries.سلطنة_عمان:
                    case Common.Countries.البحرين:
                    case Common.Countries.الكويت:
                        return "😍عرض محدود ومميز من شركة LOOX KING الايطالية😍\nاستفيدي من عرض تخفيض السعر..\nسعر المشد السحري الآن ب 10 فقط\nأو احصلي على قطعتين بسعر 18 واستفيدي منه 🥳\nلحقي حالك على العرض لا يفوتك 💃";

                    default:
                        return string.Empty;
                }
            }

            // moon light 
            if (pageId == "104568275507926")
            {
                switch (country)
                {
                    case Common.Countries.ليبيا:
                        return "تقدم لكم شركة مون لايت عرض اليوم فقط 🤩\n\nاشتري علبة الباودر بسعر 100 دينار فقط مع التوصيل! 😳\n\n✨ استفيدي من الفرصة الآن واحصلي على منتجات مون لايت بأفضل الأسعار ✨\n\nلا تفوتي العرض، اطلبي الآن قبل نفاذ الكمية! 🥰";

                    case Common.Countries.العراق:
                        return "تقدم لكم شركة مون لايت عرض اليوم فقط 🤩\n✨ الباودر صار ب 19 ألف فقط مع التوصيل 🤗✅\nاطلبيه هسه و لحقي العرض 🔥\n\n—————————————\nهمين العرض الثاني 😍\nاشتري عبوتين بسعر 35 ألف و تحصلين على هدية ببلاش من الشركة 🥳\nاستغلي الفرصة و اطلبيه الآن ⭐️";

                    case Common.Countries.تركيا:
                        return "تقدم لكم شركة مون لايت عرض اليوم فقط 🤩\n\nاشتري علبة الباودر بسعر 400 ليرة 😳\n------------------------------------------\nاشتري علبتين بسعر 750 ليرة 😳\n\n✨ استفيدي من الفرصة الآن واحصلي على منتجات مون لايت بأفضل الأسعار ✨\nلا تفوتي العرض، اطلبي الآن قبل نفاذ الكمية! 🥰";

                    case Common.Countries.الإمارات:
                        return "تقدم لكم شركة مون لايت عرض اليوم فقط 🤩\n\nاشتري عبوة الباودر بسعر 100 درهم فقط مع التوصيل! 😳\n--------------------------------------------------------\nاشتري عبوتين باودر بسعر 190 درهم فقط مع التوصيل! 😳\n\n✨ استفيدي من الفرصة الآن واحصلي على منتجات مون لايت بأفضل الأسعار ✨\nلا تفوتي العرض، اطلبي الآن قبل نفاذ الكمية! 🥰";

                    case Common.Countries.البحرين:
                    case Common.Countries.الكويت:
                    case Common.Countries.سلطنة_عمان:
                        return "تقدم لكم شركة مون لايت عرض اليوم فقط 🤩\n\nاشتري علبة الباودر بسعر 11 مع التوصيل 😳\n------------------------------------------\nاشتري علبتين بسعر 20 مع التوصيل 😳\n\n✨ استفيدي من الفرصة الآن واحصلي على منتجات مون لايت بأفضل الأسعار ✨\nلا تفوتي العرض، اطلبي الآن قبل نفاذ الكمية! 🥰";

                    default:
                        return string.Empty;
                }
            }


            //Flare Man
            else if (pageId == "416441791559844")
            {
                switch (country)
                {
                    case Common.Countries.ليبيا:
                        return "لا تفوت فرصة اليوم واستفيد من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\nاحصل على عبوة واحدة بسعر خاص جدا 100 مع التوصيل\nوذلك شاملا لتكاليف الشحن والتوصيل! 💃🏽🚚😱\nأو\nأشتري عبوتين بسعر خاص أيضا 190 دينار فقط! 🥳💖\nولا تنسى أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيع الفرصة! 📅🕐\n\nاحجز الآن واستمتع بشعر صحي وجميل، ولا تضيع هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.العراق:
                        return "لا تفوت فرصة اليوم واستفيد من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\n\nاحصل على عبوة واحدة بسعر خاص جدا 17 ألف دينار فقط💃🏽🚚😱\nأو\nأشتري عبوتين بسعر خاص أيضا 30 ألف دينار فقط! 🥳💖\nولا تنسى أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيع الفرصة! 📅🕐\n\nاحجز الآن واستمتع بشعر صحي وجميل، ولا تضيع هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.تركيا:
                        return "لا تفوت فرصة اليوم واستفيد من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\nاحصل على عبوة واحدة بسعر خاص جدا 400 ليرة فقط، 💃🏽🚚😱\nأو\nأشتري عبوتين بسعر خاص أيضا 750 ليرة فقط! 🥳💖\nولا تنسى أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيع الفرصة! 📅🕐\n\nاحجز الآن واستمتع بشعر صحي وجميل، ولا تضيع هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.الكويت:
                    case Common.Countries.البحرين:
                        return "لا تفوت فرصة اليوم واستفيد من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\nاحصل على عبوة واحدة بسعر خاص جدا 12 ريال فقط\nأو\nأشتري عبوتين بسعر خاص أيضا 20 ريال فقط! 🥳💖\nولا تنسى أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيع الفرصة! 📅🕐\n\nاحجز الآن واستمتع بشعر صحي وجميل، ولا تضيع هذه الفرصة الرائعة! 💅🏻😘🤎";

                    case Common.Countries.سلطنة_عمان:
                        return "لا تفوت فرصة اليوم واستفيد من عرض شركة FLARE على الشامبو الطبي الأصلي لمعالجة الشعر! 🌟🥳😍\nاحصل على عبوة واحدة بسعر خاص جدا 9 دينار فقط\nأو\nأشتري عبوتين بسعر خاص أيضا 16 دينار فقط! 🥳💖\nولا تنس أن هذا العرض ساري لمدة يوم واحد فقط، فلا تضيع الفرصة! 📅🕐\n\nاحجز الآن واستمتع بشعر صحي وجميل، ولا تضيع هذه الفرصة الرائعة! 💅🏻😘🤎";

                    default:
                        return string.Empty;
                }
            }


            else
            {
                return string.Empty;
            }
        }






    }


}
