using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;

namespace SqsWebhook
{
    class ApplicationWebResult
    {
        public string Url { set; get; }
        public long RequestTime { set; get; }
        public long ResponseTime { set; get; }
        public short StatusCode { set; get; }
        public string RequestBody { set; get; }
        public string ResponseBody { set; get;}
        public bool IsSuccess
        {
            get
            {
                return this.StatusCode >= 200 && this.StatusCode <= 299;
            }
        }
    }

    class ApplicationErrorMessage
    {
        public string ApplicationName;
        public ApplicationWebResult Result;
    }
    
    class Program
    {
        const string JsonFile = "appsettings.json";
        private const int DefaultPollDelay = 6000;
        private const string DefaultHttpMethod = "POST";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Loading Configuration");


            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(JsonFile, false)
                .AddEnvironmentVariables()
                .Build();

            // Load settings
            string applicationName = GetConfigurationValue(config, "APP_NAME", true, "Sqs to HTTP");
            string sqsAccessKey = GetConfigurationValue(config, "SQS_ACCESS_KEY_ID", true);
            string sqsSecretAccessKey = GetConfigurationValue(config, "SQS_SECRET_ACCESS_KEY", true);
            string sqsQueueUrl = GetConfigurationValue(config, "SQS_QUEUE_URL", true);
            int pollDelay = GetConfigurationValueAsNumber(config, "POLL_DELAY", false, DefaultPollDelay);
            int maxMessages = GetConfigurationValueAsNumber(config, "MAX_MESSAGES", false, 5);
            string httpUrl = GetConfigurationValue(config, "HTTP_URL", true);
            string httpMethod = GetConfigurationValue(config, "HTTP_METHOD", false, DefaultHttpMethod);
            string errorUrl = GetConfigurationValue(config, "ERROR_URL", false, null);
            string headerFile = GetConfigurationValue(config, "HEADER_FILE", false, "config/headers.json");
            
            Console.WriteLine("Loaded");
            Console.WriteLine($"Running. Poll delay: {pollDelay}");

            AmazonSQSClient sqsClient = new AmazonSQSClient(
                sqsAccessKey,
                sqsSecretAccessKey
            );

            while (true)
            {
                var messageRequest = new ReceiveMessageRequest()
                {
                    QueueUrl = sqsQueueUrl,
                    MaxNumberOfMessages = maxMessages
                };

                var messageResponse = await sqsClient.ReceiveMessageAsync(messageRequest);

                foreach (var message in messageResponse.Messages)
                {
                    var jsonBody = JsonNode.Parse(message.Body);
                    
                    ApplicationWebResult response = SendWebRequest(
                        httpUrl,
                        jsonBody?.ToJsonString(), 
                        httpMethod,
                        Loadheaders(headerFile)
                    );

                    string serializedResponse = JsonSerializer.Serialize(response);
                    Console.WriteLine(serializedResponse);
                    Console.WriteLine("");

                    if (!response.IsSuccess && !string.IsNullOrEmpty(errorUrl))
                    {
                        ApplicationErrorMessage errorMessage = new ApplicationErrorMessage()
                        {
                            ApplicationName = applicationName,
                            Result = response
                        };
                        
                        Console.WriteLine("Sending failure notice:");
                        
                        string serializedErrorMessage = JsonSerializer.Serialize(errorMessage);
                        
                        Console.WriteLine(serializedErrorMessage);
                        
                        SendWebRequest(
                            errorUrl,
                            serializedErrorMessage,
                            "POST",
                            null, // todo: We should have an error header file maybe...
                            "application/json"
                        );
                        
                        Console.WriteLine("");
                    }
                }

                Console.WriteLine($"Delaying {pollDelay}");
                Thread.Sleep(pollDelay);
            }
        }

        public static ApplicationWebResult SendWebRequest(
            string url, 
            string body = null, 
            string method="POST",
            WebHeaderCollection headers = null,
            string contentType = "application/json"
        ) {
            WebRequest request = WebRequest.Create(url);
            request.Method = method;
            request.ContentType = contentType;

            if (headers != null)
            {
                request.Headers = headers;    
            }

            if (body != null)
            {
                using (StreamWriter writer = new StreamWriter(request.GetRequestStream()))
                {
                    writer.Write(body);
                }
            }
            
            DateTime startTime = DateTime.Now;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            DateTime endTime = DateTime.Now;
            string responseBody;
            using (var streamReader = new StreamReader(response.GetResponseStream()))
            {
                responseBody = streamReader.ReadToEnd();
            }

            ApplicationWebResult obj = new ApplicationWebResult();
            obj.Url = url;
            obj.RequestTime = ((DateTimeOffset)startTime).ToUnixTimeMilliseconds();
            obj.ResponseTime = ((DateTimeOffset)endTime).ToUnixTimeMilliseconds();
            obj.RequestBody = body;
            obj.ResponseBody = responseBody;
            obj.StatusCode = (short)response.StatusCode;
            
            return obj;
        }

        public static WebHeaderCollection Loadheaders(string fromFile)
        {
            string fileContent = File.ReadAllText(fromFile);

            Dictionary<string, string> readItems = JsonSerializer.Deserialize<Dictionary<string, string>>(fileContent);

            WebHeaderCollection collection = new WebHeaderCollection();

            foreach (KeyValuePair<string, string> obj in readItems)
            {
                collection.Add(obj.Key, obj.Value);
            }
            
            return collection;
        }
        
        public static bool IsEmpty(string input)
        {
            return string.IsNullOrEmpty(input);
        }

        public static void ThrowEnvError(string keyName)
        {
            throw new Exception($"Environment Variable: {keyName} is required to have a value.");
        }
        
        public static string GetConfigurationValue(
            IConfigurationRoot configuration,
            string key,
            bool isRequired = false,
            string defaultValue = null
        ) {
            IConfigurationSection section = configuration.GetSection(key);
            
            if (!section.Exists())
            {
                if (isRequired && IsEmpty(defaultValue))
                {
                    ThrowEnvError(key);
                }
            }
            
            return section.Exists() ? section.Value : defaultValue;
        }

        public static int GetConfigurationValueAsNumber(
            IConfigurationRoot configuration,
            string key,
            bool isRequired = false,
            int defaultValue = -1
        )
        {
            int pollDelay;

            if (Int32.TryParse(GetConfigurationValue(configuration, key), out pollDelay))
            {
                return pollDelay;
            }

            if (isRequired && defaultValue == -1)
            {
                ThrowEnvError(key);
            }
            
            return defaultValue;
        }
    }
}