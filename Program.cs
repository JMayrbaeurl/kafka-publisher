namespace KafkaPublisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Confluent.Kafka;
    using Confluent.Kafka.Serialization;

    class Program
    {
        public static bool IsIotEdgeModule = false;
        
        public static int Main(string[] args) => MainAsync(args).Result;

        static async Task<int> MainAsync(string[] args)
        {
            DetectEnvironment();

            // Cert verification is not yet fully functional when using Windows OS for the container
            bool bypassCertVerification = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (!bypassCertVerification && IsIotEdgeModule) 
                InstallCert();

            string connectionString = GetHubConnectionString();
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("No IoT Hub connection string configured");
            DeviceClient deviceClient = await Init(connectionString, bypassCertVerification);

            var cts = new CancellationTokenSource();
            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts);
            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts); };

            string brokerList = args[0];
            var topics = args.Skip(1).ToList();
            await ConsumeKafkaMessages(deviceClient, brokerList, topics, cts);

            return 0;
        }

        private static void DetectEnvironment()
        {
            // detect the runtime environment. either we run standalone (native or containerized) or as IoT Edge module (containerized)
            // check if we have an environment variable containing an IoT Edge connectionstring, we run as IoT Edge module
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EdgeHubConnectionString")))
            {
                Console.WriteLine("IoT Edge detected, use IoT Edge Hub connection string read from environment.");
                IsIotEdgeModule = true;
            }
        }

        private static string GetHubConnectionString() 
        {
            string connectionString;

            // The Edge runtime gives us the connection string we need -- it is injected as an environment variable
            if (IsIotEdgeModule) {
                connectionString = Environment.GetEnvironmentVariable("EdgeHubConnectionString");
            } else {
                Console.WriteLine("IoT Hub connection string read from environment.");
                connectionString = Environment.GetEnvironmentVariable("_HUB_CS");
            }

            return connectionString;
        }

        static void CancelProgram(CancellationTokenSource cts)
        {
            Console.WriteLine("Termination requested, closing.");
            cts.Cancel();
        }

        /// <summary>
        /// Add certificate in local cert store for use by client for secure connection to IoT Edge runtime
        /// </summary>
        static void InstallCert()
        {
            string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            if (string.IsNullOrWhiteSpace(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing path to certificate file.");
            }
            else if (!File.Exists(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing certificate file.");
            }
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile(certPath)));
            Console.WriteLine("Added Cert: " + certPath);
            store.Close();
        }


        /// <summary>
        /// Initializes the DeviceClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task<DeviceClient> Init(string connectionString, bool bypassCertVerification = false)
        {
            Console.WriteLine("Connection String {0}", connectionString);

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            // During dev you might want to bypass the cert verification. It is highly recommended to verify certs systematically in production
            if (bypassCertVerification)
            {
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            DeviceClient ioTHubModuleClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            return ioTHubModuleClient;
        }

        static async Task ConsumeKafkaMessages(DeviceClient deviceClient, string brokerList, IEnumerable<string> topics,
            CancellationTokenSource cts) {

            var config = new Dictionary<string, object>
            {
                { "group.id", "azure-iotedge-consumer" },
                { "bootstrap.servers", brokerList }
            };

            using (var consumer = new Consumer<Ignore, string>(config, null, new StringDeserializer(Encoding.UTF8)))
            {
                consumer.Assign(new List<TopicPartitionOffset> { new TopicPartitionOffset(topics.First(), 0, 0) });

                // Raised on critical errors, e.g. connection failures or all brokers down.
                consumer.OnError += (_, error)
                    => Console.WriteLine($"Error: {error}");

                // Raised on deserialization errors or when a consumed message has an error != NoError.
                consumer.OnConsumeError += (_, error)
                    => Console.WriteLine($"Consume error: {error}");

                while (!cts.Token.IsCancellationRequested)
                {
                    Message<Ignore, string> msg;

                    if (consumer.Consume(out msg, TimeSpan.FromSeconds(1)))
                    {
                        Console.WriteLine($"Topic: {msg.Topic} Partition: {msg.Partition} Offset: {msg.Offset} {msg.Value}");

                        //string dataBuffer = JsonConvert.SerializeObject(tempData);
                        var eventMessage = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(msg.Value));
                        Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending kafka message");

                        if(IsIotEdgeModule)
                            await deviceClient.SendEventAsync("output1", eventMessage);
                        else
                            await deviceClient.SendEventAsync(eventMessage);
                    }
                }
            }
        }
    }
}
