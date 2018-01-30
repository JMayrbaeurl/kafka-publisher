using System.Collections.Generic;
using CommandLine;

namespace KafkaPublisher
{
    class Options {
        [Option('b', "brokerlist", Required = true, HelpText = "Kafka brokers to use")]
        public string BrokerList  { get; set; }

        [Option('t', "topics", Required = true, HelpText = "Topics to subscribe to")]
        public IEnumerable<string> Topics { get; set;}
    }
}