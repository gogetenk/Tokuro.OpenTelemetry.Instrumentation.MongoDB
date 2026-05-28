// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Tests.TestHelpers;

internal sealed class ActivityCollector : IDisposable
{
    private const string ActivitySourceName = "Tokuro.OpenTelemetry.Instrumentation.MongoDB";
    private readonly ActivityListener _listener;
    private readonly List<Activity> _started = new();
    private readonly List<Activity> _stopped = new();

    public ActivityCollector(ActivitySamplingResult sampling = ActivitySamplingResult.AllDataAndRecorded)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => sampling,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => sampling,
            ActivityStarted = activity =>
            {
                lock (_started)
                {
                    _started.Add(activity);
                }
            },
            ActivityStopped = activity =>
            {
                lock (_stopped)
                {
                    _stopped.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyList<Activity> Started
    {
        get
        {
            lock (_started)
            {
                return _started.ToArray();
            }
        }
    }

    public IReadOnlyList<Activity> Stopped
    {
        get
        {
            lock (_stopped)
            {
                return _stopped.ToArray();
            }
        }
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}
