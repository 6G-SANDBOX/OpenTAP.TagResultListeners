# Tag Result Listeners

This repository contains several OpenTAP Result Listeners developed and used by the University of Málaga (MORSE Group).

All included result listeners are able to inject certain tags to every generated result. The purpose of these tags is
to ease the filtering of results, in particular for situations where all results are stored together.

Currently, the supported tags are:

- For interoperability with other components of the [Open5Genesis Suite](https://github.com/5genesis):
  - **Execution ID**: Unique identifier (platform dependent) for each experiment execution. It is added as `ExecutionId` 
  to the results. Can be configured with the `Set Execution ID` and `Set Execution Metadata` steps.
  - **Iteration**: For experiments that include multiple iterations of the same test sequence. It is added as `_iteration_`
  to the results. The iteration value is configured by adding the `Mark start of iteration` test step at the beginning
  of the loop.

## Result Listeners:

### InfluxDb Result Listener

Result listener that is able to send all generated TAP results to an InfluxDB v2.x instance. 

All columns from the original results are sent as `fields`, while some information about the host machine and TAP
instance, as well as the supported tags, are sent as `tags`.

Since InfluxDb requires all results to be timestamped, the result listener will ignore any result where a timestamp
cannot be defined. There are two ways for obtaining such timestamp:
- By default, the result listener expects to find a column named `Timestamp`, that contains the date and time as a
[POSIX timestamp](https://en.wikipedia.org/wiki/Unix_time).
- If this is not available, users can define a `DateTime Override`, specific to any `Result Name`. This functionality
is available in the settings of the result listener. For each kind of result up to two columns (`Column Name 1` and `2`,
for example if the date and time are in separated columns), can be parsed following the format specified in the
`DateTime Format 1` and `2` fields, to generate the required timestamp.

### MultiCSV Result Listener

The MultiCSV Result Listener is similar to the CSV Result Listener included with OpenTAP, but is able to generate
paths and filenames that include the Execution Id (using the `{Identifier}` macro) when storing the CSV files. This
is useful when it is desirable to separate or sort the generated CSVs according to the Execution Id.

## Test Steps:

### Set Execution ID

Used to set, on each of the selected Tag Result Listeners, the Execution ID for every result generated after the
execution of this Test Step. Should be included at the start of a Test Plan.

### Set Execution Metadata

An extension of `Set Execution ID`, that additionally generates a result (`execution_metadata`) that includes additional
information about the experiment execution: Slice, Scenario, TestCases, Notes

### Mark start of iteration

Used to increase the iteration number during an experiment execution (starting at 0). Should be included at the start
of the loop that includes the repeating test sequence.
