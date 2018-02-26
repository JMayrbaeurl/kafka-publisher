docker run -d `
    --name kafka-iot-publisher `
    -v C:\Dev\iotedge\kafka:/config `
    --env KAFKAPUB_HUB_CS=$env:KAFKAPUB_HUB_CS `
    kafka-iothub-publisher:latest `
    dotnet KafkaPublisher.dll --topics=i31lpvwu-foo --api.version.request=true --configFile=/config/CloudKafkaConfig_Docker.json