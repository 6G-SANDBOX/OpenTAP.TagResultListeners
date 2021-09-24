// Author:      Alberto Salmerón Moreno <alberto.salmeron@gmail.com>
// Copyright:   Copyright 2016-2017 Universidad de Málaga (University of Málaga), Spain

using OpenTap;
using System;

namespace Tap.Plugins.UMA.ResultListeners
{
    /// <summary>
    /// Special type of result that marks the start of a new iteration.
    /// </summary>
    public class IterationMarkResult
    {
        public const string NAME = "MarkIterationResult";
        public const string ITERATION_COLUMN = "Iteration";

        public int Iteration { get; private set; }

        public IterationMarkResult(int iteration)
        {
            Iteration = iteration;
        }

        public static bool IsIterationMarkResult(ResultTable results)
        {
            return NAME == results.Name &&
                1 == results.Columns.Length &&
                ITERATION_COLUMN == results.Columns[0].Name;
        }

        public static int GetIteration(ResultTable results)
        {
            if (!IsIterationMarkResult(results))
            {
                throw new ArgumentException($"Expected {NAME} result, but got {results.Name}");
            }

            return (int)results.Columns[0].Data.GetValue(0);
        }
    }
}
