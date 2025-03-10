﻿// Author:      Alberto Salmerón Moreno <alberto.salmeron@gmail.com>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain


using OpenTap;
using System;
using System.Xml.Serialization;
using OpenTap.TagResultListeners.ResultListeners;

namespace OpenTap.TagResultListeners.Steps
{
    [Display("Mark Start of Iteration",
        Group: "Tag Result Listeners",
        Description: "Marks the start of a new iteration")]
    public class NewIterationStartStep : TestStep
    {
        private int iteration;

        [Display("Current iteration",
            Group: "Iteration",
            Description: "Read-only indicator of current iteration number.", Order: 2.0)]
        [EnabledIf("AlwaysFalse", true)]
        public int CurrentIteration { get { return Math.Max(0, iteration - 1); } set { } }

        [XmlIgnore]
        public bool AlwaysFalse { get { return false; } }

        public NewIterationStartStep() { }

        public override void PrePlanRun()
        {
            iteration = 0;
        }

        public override void Run()
        {
            Results.Publish(IterationMarkResult.NAME, new IterationMarkResult(iteration));
            iteration++;
        }

        public override void PostPlanRun()
        {
            iteration = 0;
        }
    }
}
