# Kafka to Azure IoT Hub Publisher

Sample [.NET Core](https://docs.microsoft.com/en-us/dotnet/core/) console application that acts as a [Kafka](http://kafka.apache.org/) consumer. Any messages found on specified Kafka topics will be immediately forwarded to the configured Azure IoT Hub instance.

Originally this sample was developed at the [Manufacturing Hack Challenge](http://bcw.bosch-si.com/berlin/manufacturing-4-0/) at [Bosch Connected World 2018 in Berlin](http://bcw.bosch-si.com/berlin/). It was part of the following technical architecture:
![alt](./doc/media/Kafka_Publisher_TA_BCW18.png)

Telemetry data, sent by a Bosch Packaging simulator, was gathered by an Bosch IoT gateway and pushed to an Apache Kafka server that was running in the 'Fog' layer of the installation. The message payload data format used for the telemetry data was the [Production Performance Management Protocol](https://www.eclipse.org/unide/specification/), that is part of the [Eclipse Unide project](https://www.eclipse.org/unide/).

## Configuration
Configuration consists of two parts. First part is for the Kafka cluster, the source of the telemetry data, and second is for the Azure IoT Hub, the sink or the target of the telemetry data publishing. 

### Kafka cluster configuration

### Azure IoT Hub configuration

## Run as .NET Core console application

## Run as Docker container

## Run as Azure IoT Edge module