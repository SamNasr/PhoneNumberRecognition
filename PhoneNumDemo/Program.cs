using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Configuration;
using Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Lookups.V1;

namespace PhoneNumberReader
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

            // Replace the following fields with your own specific subscription Key.  Otherwise, the service call will fail.
            string subscriptionKey = config["AppSettings:subscriptionKey"];
            string endpoint = config["AppSettings:endpoint"];

            ComputerVisionClient client = InstantiateClient(endpoint, subscriptionKey);
            string fileName = @".\Images\Card.jpg";
            GetTextFromImage(client, fileName).Wait();
        }

        public static ComputerVisionClient InstantiateClient(string endpoint, string key)
        {
            ComputerVisionClient client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(key)) { Endpoint = endpoint };
            return client;
        }

        public static async Task GetTextFromImage(ComputerVisionClient client, string imageFileName)
        {
            using (Stream stream = File.OpenRead(imageFileName))
            {
                // Get the recognized text
                OcrResult localFileOcrResult = await client.RecognizePrintedTextInStreamAsync(true, stream);

                // Display text, language, angle, orientation, and regions of text from the results.
                Console.WriteLine("File Name: " + imageFileName);
                Console.WriteLine("Language: " + localFileOcrResult.Language);
                Console.WriteLine("Text Angle: " + localFileOcrResult.TextAngle);
                Console.WriteLine("Orientation: " + localFileOcrResult.Orientation);
                Console.WriteLine();
                Console.WriteLine("Text regions: ");

                // Getting only one line of text for testing purposes. To see a full demonstration, remove the counter & conditional.
                int counter = 0;
                foreach (var localRegion in localFileOcrResult.Regions)
                {
                    Console.WriteLine("Region bounding box: " + localRegion.BoundingBox);
                    foreach (var line in localRegion.Lines)
                    {
                        Console.WriteLine("Line bounding box: " + line.BoundingBox);
                        counter++;

                        foreach (var word in line.Words)
                        {
                            Console.WriteLine("Word bounding box: " + word.BoundingBox);
                            Console.WriteLine("Text: " + word.Text);

                            // Validate using RegEx
                            Regex rx = new Regex(@"^[2-9]\d{2}-\d{3}-\d{4}$", RegexOptions.IgnorePatternWhitespace);
                            MatchCollection matches = rx.Matches(word.Text);

                            if (matches.Count >= 1)
                            {
                                Console.WriteLine($"Phone Number Found: {matches.First()}");
                                await IsTwilioVerified(matches.First().ToString());
                            }
                        }
                    }
                }
            }
        }

        private static async Task<bool> IsTwilioVerified(string num)
        {
            bool IsValid = false;
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

            // Find your Account Sid and Token at twilio.com/console. See http://twil.io/secure for more info
            string accountSid = config["AppSettings:accountSid"];
            string authToken = config["AppSettings:authToken"];

            try
            {
                // Find your Account Sid and Token at twilio.com/console. See http://twil.io/secure for more info
                TwilioClient.Init(accountSid, authToken);

                // Reference: https://www.twilio.com/docs/lookup/tutorials/validation-and-formatting
                var phoneNum = new Twilio.Types.PhoneNumber(num);
                var numInfo = await PhoneNumberResource.FetchAsync(countryCode: "US",
                    pathPhoneNumber: phoneNum);
                Console.WriteLine($"Twilio Verified Phone Number: { numInfo.PhoneNumber }");

                if (numInfo.PhoneNumber.ToString().Length >= 1)
                    IsValid = true;
            }

            catch (ApiException e)
            {
                Console.WriteLine($"Twilio Error {e.Code} - {e.MoreInfo}");
            }

            catch (Exception ex)
            {
                Console.WriteLine("Error:" + ex.Message);
                throw;
            }

            return IsValid;
        }
    }
}
