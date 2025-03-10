﻿// Author:      Alberto Salmerón Moreno <alberto.salmeron@gmail.com>
// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using OpenTap;

namespace OpenTap.TagResultListeners.ResultListeners
{
    public abstract class ConfigurableResultListenerBase : ResultListener
    {
        private static Regex VALID_CHARS = new Regex(@"[^A-Za-z0-9_-]", RegexOptions.Compiled);

        public const string ITERATION_COLUMN_NAME = "_iteration_";

        [Display("Set Execution ID", Group: "Metadata", Order: 99.0,
            Description: "Add an extra 'ExecutionId' identifier to the results. The value for\n" +
                         "this identifier must be set by the 'Set Execution ID' step at some point\n" +
                         "before the end of the testplan run. The 'experiment_id' value on MQTT \n" +
                         "Publisher ResultListeners uses the same 'ExecutionId', and is always \n" +
                         "set, regardless of this configuration.")]
        public bool SetExecutionId { get; set; }

        [Display("Add Iteration Number", Group: "Metadata", Order: 99.1,
            Description: "Add an '_iteration_' identifier to the results. The value for\n" +
                         "this identifier will be set by the 'Mark Start of Iteration' step,\n" +
                         "the iteration value will automatically increase every time this step\n" +
                         "is run.")]
        public bool AddIteration { get; set; }

        [XmlIgnore]
        public string ExecutionId { get; set; }

        protected int iteration { get; set; }

        public override void Open()
        {
            base.Open();

            iteration = 0;
            ExecutionId = string.Empty;
        }

        public override void Close()
        {
            base.Close();

            iteration = 0;
            ExecutionId = string.Empty;
        }

        protected ResultTable ProcessResult(ResultTable results)
        {
            if (processIterationMarkResult(results))
            {
                return null;
            }

            return AddIteration ? InjectColumn(results, ITERATION_COLUMN_NAME, iteration) : results;
        }

        private bool processIterationMarkResult(ResultTable results)
        {
            // Intercept IterationMarkResult and don't publish it
            if (IterationMarkResult.IsIterationMarkResult(results))
            {
                iteration = IterationMarkResult.GetIteration(results);
                return true;
            }
            else
            {
                return false;
            }
        }

        protected ResultTable InjectColumn(ResultTable table, string name, IConvertible value)
        {
            int rows = table.Columns.Length > 0 ? table.Columns[0].Data.Length : 1;

            IConvertible[] values = new IConvertible[rows];
            for (int i = 0; i < rows; i++) { values[i] = value; }

            ResultColumn column = new ResultColumn(name, values);
            ResultColumn[] columns = insert(table.Columns, 0, column);

            return new ResultTable(table.Name, columns);
        }

        private T[] insert<T>(T[] columns, int index, T column)
        {
            List<T> list = new List<T>(columns);
            list.Insert(index, column);
            return list.ToArray();
        }

        public string Sanitize(string value, string replacement)
        {
            return VALID_CHARS.Replace(value, replacement);
        }

        internal DateTime? getDateTime( Dictionary<string, IConvertible> dict ) {
            // Try to find the timestamp key
            List<string> keys = new List<string>( dict.Keys );
            string timestampKey = keys.Find( ( key ) => ( key.ToUpper() == "TIMESTAMP" ) );

            if ( timestampKey != null ) {
                IConvertible value = dict[timestampKey];

                if ( value != null ) {
                    long milliseconds;

                    switch ( value.GetTypeCode() ) {
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            milliseconds = value.ToInt64( null );
                            break;
                        case TypeCode.Double:
                            milliseconds = (long)( value.ToDouble( null ) * 1000 );
                            break;
                        default: return null;
                    }
                    return DateTimeOffset.FromUnixTimeMilliseconds( milliseconds ).UtcDateTime;
                }
            }
            return null;
        }

        internal IEnumerable<Dictionary<string, IConvertible>> getRows( ResultTable table ) {
            for ( int r = 0; r < table.Rows; r++ ) {
                Dictionary<string, IConvertible> res = new Dictionary<string, IConvertible>();
                for ( int c = 0; c < table.Columns.Length; c++ ) {
                    ResultColumn column = table.Columns.ElementAt( c );
                    res[column.Name] = (IConvertible)column.Data.GetValue( r );
                }
                yield return res;
            }
        }
    }
}
