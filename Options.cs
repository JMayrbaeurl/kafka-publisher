using System.Collections.Generic;
using CommandLine;

namespace KafkaPublisher
{
    class Options {
        [Option('b', "brokerlist", HelpText = "bootstrap.servers: A list of host/port pairs to use for establishing the initial connection to the Kafka cluster. The client will make use of all servers irrespective of which servers are specified here for bootstrapping—this list only impacts the initial hosts used to discover the full set of servers. This list should be in the form host1:port1,host2:port2,.... Since these servers are just used for the initial connection to discover the full cluster membership (which may change dynamically), this list need not contain the full set of servers (you may want more than one, though, in case a server is down).")]
        public string BrokerList  { get; set; }

        [Option('t', "topics", Required = true, HelpText = "Kafka cluster topics to subscribe to")]
        public IEnumerable<string> Topics { get; set;}

        [Option('c', "consumergroup", Default = "", HelpText="group.id: A unique string that identifies the consumer group this consumer belongs to. This property is required if the consumer uses either the group management functionality by using subscribe(topic) or the Kafka-based offset management strategy.")]
        public string Consumergroup { get; set;}

        [Option('s', "security.protocol", HelpText="Protocol used to communicate with brokers. Valid values are: PLAINTEXT, SSL, SASL_PLAINTEXT, SASL_SSL.")]
        public string Securityprotocol { get; set; }

        [Option('w', "sasl.mechanisms", HelpText="SASL mechanism used for client connections. This may be any mechanism for which a security provider is available. GSSAPI is the default mechanism.")]
        public string SaslMechanism { get; set; }

        [Option('u', "username", HelpText="Simple Authentication and Security Layer (SASL) username")]
        public string Username { get; set; }

        [Option('p', "password", HelpText="Simple Authentication and Security Layer (SASL) password")]
        public string Password { get; set; }

        [Option('l', "ssl.ca.location", HelpText="SSL CA certificate location")]
        public string SslCaLocation { get; set; }

        [Option('v', "api.version.request", Default = true, HelpText = "Broker version compatibility, see https://github.com/edenhill/librdkafka/wiki/Broker-version-compatibility")]
        public bool DoAPIVersionRequest { get; set; }

        [Option('m', "samplingrate", Default = 1000, HelpText = "Sampling rate in milliseconds used to poll messages from Kafka broker")]
        public int Samplingrate { get; set; }

        [Option('f', "flatten", Default = false)]
        public bool Flatten { get; set; }

        [Option('x', "configFile", HelpText = "Configuration file location for additional Kafka consumer configuration parameters. Must be stored as key value pairs in a json file")]
        public string ConfigFilePath { get; set; }

        [Option("iothubprotocol", Default = "Mqtt_Tcp_Only", HelpText = "the protocol to use for communication with Azure IoT Hub")]
        public string IoTHubProtocol { get; set; }

        [Option("session.timeout.ms", Default = 30000, HelpText = "The timeout used to detect consumer failures when using Kafka's group management facility. The consumer sends periodic heartbeats to indicate its liveness to the broker. If no heartbeats are received by the broker before the expiration of this session timeout, then the broker will remove this consumer from the group and initiate a rebalance. Note that the value must be in the allowable range as configured in the broker configuration by group.min.session.timeout.ms and group.max.session.timeout.ms.")]
        public int SessionTimeout { get; set; }

        [Option("max.poll.records", Default = 100, HelpText = "The maximum number of records returned in a single call to poll().")]
        public int? MaxPollRecords { get; set;}
    }
}