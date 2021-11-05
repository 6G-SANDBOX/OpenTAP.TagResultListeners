// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

using System;
using System.Collections.Generic;
using OpenTap;

using OpenTap.Plugins.UMA.Extensions;
using OpenTap.Plugins.UMA.ResultListeners;

namespace OpenTap.Plugins.UMA.Steps
{
    [Display("Set Execution Metadata", Groups: new string[] { "UMA", "Misc" },
             Description: "Sets the Execution ID on compatible result listeners Additional\n"+
                          "metadata will be saved on the 'execution_metadata' result.")]
    public class SetExecutionMetadataStep : SetExecutionIdStep
    {
        #region Settings

        #region Metadata Table

        [Display( "Publish metadata table", Order: 2.1, Group: "Metadata table" )]
        public bool MetadataTable { get; set; }

        [Display("Slice", Order: 2.2, Group: "Metadata table")]
        [EnabledIf("MetadataTable", true, HideIfDisabled = true)]
        public string Slice { get; set; }

        [Display("Scenario", Order: 2.3, Group: "Metadata table" )]
        [EnabledIf( "MetadataTable", true, HideIfDisabled = true )]
        public string Scenario { get; set; }

        [Display("TestCases", Order: 2.4, Group: "Metadata table" )]
        [EnabledIf( "MetadataTable", true, HideIfDisabled = true )]
        public string TestCases { get; set; }

        [Display("Notes", Order: 2.5, Group: "Metadata table" )]
        [EnabledIf( "MetadataTable", true, HideIfDisabled = true )]
        public string Notes { get; set; }

        #endregion

        #region MqttPublisher

        [Display( "Configure MQTT Publisher", Order: 3.1, Group: "MQTT Publisher" )]
        public bool ConfigureMqtt { get; set; }

        [Display( "Use Case", Order: 3.2, Group: "MQTT Publisher" )]
        [EnabledIf( "ConfigureMqtt", true, HideIfDisabled = true )]
        public string MqttUseCase { get; set; }

        [Display( "Testbed Id", Order: 3.3, Group: "MQTT Publisher" )]
        [EnabledIf( "ConfigureMqtt", true, HideIfDisabled = true )]
        public string MqttTestbed { get; set; }

        [Display( "Scenario Id", Order: 3.4, Group: "MQTT Publisher" )]
        [EnabledIf( "ConfigureMqtt", true, HideIfDisabled = true )]
        public string MqttScenario { get; set; }

        [Display( "NetApp Id", Order: 3.5, Group: "MQTT Publisher" )]
        [EnabledIf( "ConfigureMqtt", true, HideIfDisabled = true )]
        public string MqttNetApp { get; set; }

        #endregion

        #endregion

        private static List<string> columns = new List<string> { "Timestamp", "Date", "Time", "Slice", "Scenario", "TestCases", "Notes" };

        public SetExecutionMetadataStep()
        {
            Slice = Scenario = TestCases = string.Empty;
            MqttUseCase = MqttTestbed = MqttScenario = MqttNetApp = string.Empty;
            Notes = "Test execution";
        }

        public override void Run()
        {
            base.Run();

            if ( MetadataTable ) {
                DateTime now = DateTime.UtcNow;
                IConvertible[] values = new IConvertible[] {
                now.ToUnixTimestamp(), now.ToShortDateString(), now.ToShortTimeString(), Slice, Scenario, TestCases, Notes
                };

                Results.Publish( "execution_metadata", columns, values );

                Log.Info( "Experiment metadata: " );
                for ( int i = 0; i < columns.Count; i++ ) {
                    Log.Info( $"  {columns[i]}: {values[i]}" );
                }
            }

            if ( ConfigureMqtt ) {
                bool found = false;
                foreach ( ConfigurableResultListenerBase resultListener in ResultListeners ) {
                    MqttPublisherResultListener mqtt = resultListener as MqttPublisherResultListener;

                    if ( mqtt != null ) {
                        mqtt.UseCase = this.MqttUseCase;
                        mqtt.TestbedId = this.MqttTestbed;
                        mqtt.ScenarioId = this.MqttScenario;
                        mqtt.NetAppId = this.MqttNetApp;

                        Log.Info( $"Setting MQTT Metadata to '{resultListener.Name}'" );
                        found = true;
                    }
                }

                if ( !found ) {
                    Log.Warning( "Could not find any MqttPublisherResultListener instance to configure." );
                } else {
                    Log.Info( "MQTT Metadata:" );
                    Log.Info( $"  'use_case_id': '{this.MqttUseCase}'" );
                    Log.Info( $"  'testbed_id': '{this.MqttTestbed}'" );
                    Log.Info( $"  'scenario_id': '{this.MqttScenario}'" );
                    Log.Info( $"  'netapp_id': '{this.MqttNetApp}'" );
                }
            }
        }
    }
}
