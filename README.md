# UMA Result Listeners

This repository contains several OpenTAP Result Listeners developed and used by the University of Málaga (MORSE Group).

## InfluxDb Result Listener

Result listener that is able to send all generated TAP results to an InfluxDB instance. 

All columns from the original results are sent as `fields`, while some information about the host machine and TAP instance are sent as `tags`.

The result listener is also able to send the generated TAP logs to InfluxDB, with a format compatible with [Chronograf](https://www.influxdata.com/time-series-platform/chronograf/)'s log viewer.  

It's also compatible with extra metadata (`ExecutionId` and `_iteration_`) for interoperability with other components of the [Open5Genesis Suite](https://github.com/5genesis).

## (RabbitMQ) Publisher Result Listener

This result listener is able to send generated results to a RabbitMQ Publisher (not a RabbitMQ broker) as used in the 5G-Epicentre project.

The result listener supports generating the required timestamps from the contents of certain columns and also defining the extra metadata
required for each separate measurement type.