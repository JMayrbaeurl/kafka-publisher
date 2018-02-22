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
    using CommandLine;
    using Newtonsoft.Json;

    class Program
    {
        public static bool IsIotEdgeModule = false;
        
        private static DeviceClient deviceClient;

        public static int Main(string[] args) => MainAsync(args).Result;

        static async Task<int> MainAsync(string[] args)
        {
            DetectEnvironment();

            // Cert verification is not yet fully functional when using Windows OS for the container
            bool bypassCertVerification = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (!bypassCertVerification && IsIotEdgeModule) 
                InstallCert();

            var result = CommandLine.Parser.Default.ParseArguments<Options>(args);
            Parsed<Options> parsed = result as Parsed<Options>;
            if (parsed != null) 
            {
                Options options = parsed.Value;
                string connectionString = GetHubConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("No IoT Hub connection string configured");
                deviceClient = await Init(connectionString, options, bypassCertVerification);

                var cts = new CancellationTokenSource();
                void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts);
                AssemblyLoadContext.Default.Unloading += OnUnload;
                Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts); };

                ConsumeKafkaMessages(deviceClient, options, cts);

                return 0;
            } else 
            {
                NotParsed<Options> notparsed = result as NotParsed<Options>;
                //notparsed.Errors;
                return -1;
            }
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
                connectionString = Environment.GetEnvironmentVariable("KAFKAPUB_HUB_CS");
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
        static async Task<DeviceClient> Init(string connectionString, Options options, bool bypassCertVerification = false)
        {
            Console.WriteLine("Connection String {0}", connectionString);

            TransportType transportProtocol = TransportType.Mqtt_Tcp_Only;
            if(!String.IsNullOrEmpty(options.IoTHubProtocol))
                transportProtocol = (TransportType)Enum.Parse(typeof(TransportType), options.IoTHubProtocol);
            MqttTransportSettings mqttSetting = new MqttTransportSettings(transportProtocol);
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

        static void ConsumeKafkaMessages(DeviceClient deviceClient, Options options,
            CancellationTokenSource cts) {

            var config = CreateKafkaConfigurationFromConfig(options);

            using (var consumer = new Consumer<Ignore, string>(config, null, new StringDeserializer(Encoding.UTF8)))
            {
                consumer.Subscribe(options.Topics);
                InstallErrorHandlingForConsumer(consumer);

                consumer.OnMessage += async (_, msg) =>
                {
                    IList<Microsoft.Azure.Devices.Client.Message> iothubMessages = new List<Microsoft.Azure.Devices.Client.Message>();
                    if (options.Flatten)
                    {
                        // Future extension
                        iothubMessages.Add(new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(msg.Value)));
                    } else
                        iothubMessages.Add(new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(msg.Value)));

                    foreach(Microsoft.Azure.Devices.Client.Message singleMessage in iothubMessages)
                    {
                        Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending kafka message to Azure IoT Hub");

                        if(IsIotEdgeModule)
                            await deviceClient.SendEventAsync("output1", singleMessage);
                        else
                            await deviceClient.SendEventAsync(singleMessage);
                    }
                };

                while (!cts.Token.IsCancellationRequested)
                {
                    consumer.Poll(TimeSpan.FromMilliseconds(options.Samplingrate));
                }
            }
        }

        private static void InstallPartitionHandling(Consumer<Ignore, string> consumer)
        {
            consumer.OnPartitionsAssigned += (_, partitions) =>
            {
                Console.WriteLine($"Assigned partitions: [{string.Join(", ", partitions)}], member id: {consumer.MemberId}");
                consumer.Assign(partitions);
            };

            consumer.OnPartitionsRevoked += (_, partitions) =>
            {
                Console.WriteLine($"Revoked partitions: [{string.Join(", ", partitions)}]");
                consumer.Unassign();
            };
        }

        private static Dictionary<string, object> CreateKafkaConfigurationFromConfig(Options options) 
        {
            var config = new Dictionary<string, object>
            {
                { "api.version.request", options.DoAPIVersionRequest }
            };

            if (!String.IsNullOrEmpty(options.BrokerList))
                config.Add("bootstrap.servers", options.BrokerList);

            if (!String.IsNullOrEmpty(options.Consumergroup))
                config.Add("group.id", options.Consumergroup);
            else
                config.Add("group.id", "azure-iotedge-publisher");

            if (!String.IsNullOrEmpty(options.Securityprotocol))
                config.Add("security.protocol", options.Securityprotocol);

            if (!String.IsNullOrEmpty(options.SaslMechanism))
                config.Add("sasl.mechanisms", options.SaslMechanism);

            if (!String.IsNullOrEmpty(options.Username))
                config.Add("sasl.username", options.Username);

            if (!String.IsNullOrEmpty(options.Password))
                config.Add("sasl.password", options.Password);

            if (!String.IsNullOrEmpty(options.SslCaLocation))
                config.Add("ssl.ca.location", options.SslCaLocation);

            config.Add("session.timeout.ms", options.SessionTimeout);    

            if (options.MaxPollRecords != null)
                config.Add("max.poll.records", options.MaxPollRecords);

            if (!String.IsNullOrEmpty(options.ConfigFilePath)) 
            {
                string configsInFile = File.ReadAllText(options.ConfigFilePath);
                if (!String.IsNullOrEmpty(configsInFile))
                {
                    Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(configsInFile);
                    if (values != null && values.Count > 0) {
                        foreach(KeyValuePair<string, string> entry in values) 
                        {
                            config.Add(entry.Key, entry.Value);
                        }
                    }
                }
            }

            return config;
        }

        static void InstallErrorHandlingForConsumer(Consumer<Ignore, string> consumer) 
        {
            // Raised on critical errors, e.g. connection failures or all brokers down.
            consumer.OnError += (_, error)
                => Console.WriteLine($"Error: {error}");

            // Raised on deserialization errors or when a consumed message has an error != NoError.
            consumer.OnConsumeError += (_, error)
                => Console.WriteLine($"Consume error: {error}");
        }
    }
}
