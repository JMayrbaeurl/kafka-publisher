using System.Collections.Generic;
using CommandLine;

namespace KafkaPublisher
{
    class Options {
        [Option('b', "brokerlist", Required = true, HelpText = "bootstrap.servers: A list of host/port pairs to use for establishing the initial connection to the Kafka cluster. The client will make use of all servers irrespective of which servers are specified here for bootstrapping—this list only impacts the initial hosts used to discover the full set of servers. This list should be in the form host1:port1,host2:port2,.... Since these servers are just used for the initial connection to discover the full cluster membership (which may change dynamically), this list need not contain the full set of servers (you may want more than one, though, in case a server is down).")]
        public string BrokerList  { get; set; }

        [Option('t', "topics", Required = true, HelpText = "Topics to subscribe to")]
        public IEnumerable<string> Topics { get; set;}

        [Option('c', "consumergroup", Default = "", HelpText="group.id: A unique string that identifies the consumer group this consumer belongs to. This property is required if the consumer uses either the group management functionality by using subscribe(topic) or the Kafka-based offset management strategy.")]
        public string Consumergroup { get; set;}

        [Option('s', "security.protocol", HelpText="")]
        public string Securityprotocol { get; set; }

        [Option('w', "sasl.mechanisms", HelpText="")]
        public string SaslMechanism { get; set; }

        [Option('u', "username")]
        public string Username { get; set; }

        [Option('p', "password")]
        public string Password { get; set; }

        [Option('l', "ssl.ca.location")]
        public string SslCaLocation { get; set; }

        [Option('v', "api.version.request", Default = true)]
        public bool DoAPIVersionRequest { get; set; }

        [Option('m', "samplingrate", Default = 1000)]
        public int Samplingrate { get; set; }

        [Option('f', "flatten", Default = false)]
        public bool Flatten { get; set; }

        [Option('x', "configFile")]
        public string ConfigFilePath { get; set; }
    }
}