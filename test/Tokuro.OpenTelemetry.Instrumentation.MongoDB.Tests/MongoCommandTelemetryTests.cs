// Copyright (c) Yannis Tocreau and contributors. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net;
using MongoDB.Bson;
using MongoDB.Driver.Core.Events;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Implementation;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Internal;
using Tokuro.OpenTelemetry.Instrumentation.MongoDB.Tests.TestHelpers;

namespace Tokuro.OpenTelemetry.Instrumentation.MongoDB.Tests;

public sealed class MongoCommandTelemetryTests
{
    private const string ExceptionMessageTag = "exception.message";

    private static MongoCommandEventSubscriber CreateSubscriber(MongoDBInstrumentationOptions options)
    {
        var telemetry = new MongoCommandTelemetry(options);
        return new MongoCommandEventSubscriber(telemetry);
    }

    private static void InvokeStarted(MongoCommandEventSubscriber subscriber, CommandStartedEvent @event)
    {
        subscriber.TryGetEventHandler<CommandStartedEvent>(out var handler).Should().BeTrue();
        handler(@event);
    }

    private static void InvokeSucceeded(MongoCommandEventSubscriber subscriber, CommandSucceededEvent @event)
    {
        subscriber.TryGetEventHandler<CommandSucceededEvent>(out var handler).Should().BeTrue();
        handler(@event);
    }

    private static void InvokeFailed(MongoCommandEventSubscriber subscriber, CommandFailedEvent @event)
    {
        subscriber.TryGetEventHandler<CommandFailedEvent>(out var handler).Should().BeTrue();
        handler(@event);
    }

    [Fact]
    public void Started_EmitsLegacyAndStableDbAttributes_WhenBothFlagsTrue()
    {
        using var collector = new ActivityCollector();
        var options = new MongoDBInstrumentationOptions { EmitLegacyAttributes = true, EmitStableAttributes = true };
        var subscriber = CreateSubscriber(options);

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded());

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.DbSystem).Should().Be("mongodb");
        activity.GetTagItem(SemConv.DbSystemName).Should().Be("mongodb");
        activity.GetTagItem(SemConv.DbName).Should().Be("testdb");
        activity.GetTagItem(SemConv.DbNamespace).Should().Be("testdb");
    }

    [Fact]
    public void Started_EmitsOnlyStableAttributes_WhenEmitLegacyAttributesFalse()
    {
        using var collector = new ActivityCollector();
        var options = new MongoDBInstrumentationOptions { EmitLegacyAttributes = false, EmitStableAttributes = true };
        var subscriber = CreateSubscriber(options);

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded());

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.DbSystem).Should().BeNull();
        activity.GetTagItem(SemConv.DbName).Should().BeNull();
        activity.GetTagItem(SemConv.DbSystemName).Should().Be("mongodb");
        activity.GetTagItem(SemConv.DbNamespace).Should().Be("testdb");
    }

    [Fact]
    public void Started_EmitsOnlyLegacyAttributes_WhenEmitStableAttributesFalse()
    {
        using var collector = new ActivityCollector();
        var options = new MongoDBInstrumentationOptions { EmitLegacyAttributes = true, EmitStableAttributes = false };
        var subscriber = CreateSubscriber(options);

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded());

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.DbSystem).Should().Be("mongodb");
        activity.GetTagItem(SemConv.DbName).Should().Be("testdb");
        activity.GetTagItem(SemConv.DbSystemName).Should().BeNull();
        activity.GetTagItem(SemConv.DbNamespace).Should().BeNull();
    }

    [Fact]
    public void Started_RedactsCommandText_WhenCaptureCommandTextTrue()
    {
        using var collector = new ActivityCollector();
        var options = new MongoDBInstrumentationOptions { CaptureCommandText = true };
        var subscriber = CreateSubscriber(options);
        var command = new BsonDocument
        {
            { "find", "users" },
            { "$db", "testdb" },
            { "filter", new BsonDocument("email", "john.doe@example.com") }
        };

        InvokeStarted(subscriber, CommandEventFactory.Started(command: command));
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded());

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        var text = activity.GetTagItem(SemConv.DbQueryText) as string
            ?? activity.GetTagItem(SemConv.DbStatement) as string;
        text.Should().NotBeNullOrEmpty();
        text!.Should().NotContain("john.doe@example.com");
        text.Should().Contain("\"?\"");
    }

    [Fact]
    public void Started_OmitsCommandText_WhenCaptureCommandTextFalse()
    {
        using var collector = new ActivityCollector();
        var options = new MongoDBInstrumentationOptions { CaptureCommandText = false };
        var subscriber = CreateSubscriber(options);

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded());

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.DbQueryText).Should().BeNull();
        activity.GetTagItem(SemConv.DbStatement).Should().BeNull();
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("isMaster")]
    [InlineData("buildInfo")]
    [InlineData("endSessions")]
    [InlineData("saslStart")]
    [InlineData("saslContinue")]
    public void Started_SkipsIgnoredCommands(string commandName)
    {
        using var collector = new ActivityCollector();
        var subscriber = CreateSubscriber(new MongoDBInstrumentationOptions());

        InvokeStarted(subscriber, CommandEventFactory.Started(commandName: commandName, collection: commandName));

        collector.Started.Should().BeEmpty();
    }

    [Fact]
    public void Started_SkipsCommand_WhenFilterCommandReturnsFalse()
    {
        using var collector = new ActivityCollector();
        var options = new MongoDBInstrumentationOptions
        {
            FilterCommand = _ => false
        };
        var subscriber = CreateSubscriber(options);

        InvokeStarted(subscriber, CommandEventFactory.Started());

        collector.Started.Should().BeEmpty();
    }

    [Fact]
    public void Started_SetsServerAddressAndPort_WhenEndpointIsDnsEndPoint()
    {
        using var collector = new ActivityCollector();
        var subscriber = CreateSubscriber(new MongoDBInstrumentationOptions());
        var endpoint = new DnsEndPoint("mongo.example.com", 27018);

        InvokeStarted(subscriber, CommandEventFactory.Started(endpoint: endpoint));
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded(endpoint: endpoint));

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.ServerAddress).Should().Be("mongo.example.com");
        activity.GetTagItem(SemConv.ServerPort).Should().Be(27018);
    }

    [Fact]
    public void Started_SetsServerAddressAndPort_WhenEndpointIsIPEndPoint()
    {
        using var collector = new ActivityCollector();
        var subscriber = CreateSubscriber(new MongoDBInstrumentationOptions());
        var endpoint = new IPEndPoint(IPAddress.Parse("10.0.0.5"), 27019);

        InvokeStarted(subscriber, CommandEventFactory.Started(endpoint: endpoint));
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded(endpoint: endpoint));

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.ServerAddress).Should().Be("10.0.0.5");
        activity.GetTagItem(SemConv.ServerPort).Should().Be(27019);
    }

    [Fact]
    public void Failed_RedactsExceptionMessage_WhenSuppressExceptionMessageTrue()
    {
        using var collector = new ActivityCollector();
        var options = new MongoDBInstrumentationOptions { SuppressExceptionMessage = true };
        var subscriber = CreateSubscriber(options);

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeFailed(subscriber, CommandEventFactory.Failed());

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.ErrorType).Should().Be(typeof(InvalidOperationException).FullName);
        activity.GetTagItem(ExceptionMessageTag).Should().BeNull();
    }

    [Fact]
    public void Failed_EmitsRawExceptionMessage_WhenSuppressExceptionMessageFalse()
    {
        using var collector = new ActivityCollector();
        var options = new MongoDBInstrumentationOptions { SuppressExceptionMessage = false };
        var subscriber = CreateSubscriber(options);
        var exception = new InvalidOperationException("a specific failure");

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeFailed(subscriber, CommandEventFactory.Failed(exception: exception));

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.ErrorType).Should().Be(typeof(InvalidOperationException).FullName);
        (activity.GetTagItem(ExceptionMessageTag) as string).Should().Be("a specific failure");
    }

    [Fact]
    public void Succeeded_SetsActivityStatusOk()
    {
        using var collector = new ActivityCollector();
        var subscriber = CreateSubscriber(new MongoDBInstrumentationOptions());

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded());

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public void Failed_SetsActivityStatusError()
    {
        using var collector = new ActivityCollector();
        var subscriber = CreateSubscriber(new MongoDBInstrumentationOptions());

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeFailed(subscriber, CommandEventFactory.Failed());

        var activity = collector.Stopped.Should().ContainSingle().Subject;
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public void Telemetry_RespectsActivityIsAllDataRequested_DoesNotPopulateTags_WhenNotRequested()
    {
        // PropagationData sampling means activity is created but IsAllDataRequested == false:
        // the telemetry should skip the BSON walker and not set any db.* tags.
        using var propagationCollector = new ActivityCollector(ActivitySamplingResult.PropagationData);
        var options = new MongoDBInstrumentationOptions { CaptureCommandText = true };
        var subscriber = CreateSubscriber(options);

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded());

        var activity = propagationCollector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.DbStatement).Should().BeNull();
        activity.GetTagItem(SemConv.DbQueryText).Should().BeNull();
        activity.GetTagItem(SemConv.DbCollectionName).Should().BeNull();
        activity.GetTagItem(SemConv.DbSystem).Should().BeNull();
        activity.GetTagItem(SemConv.DbSystemName).Should().BeNull();
    }

    [Fact]
    public void Telemetry_PopulatesTags_WhenAllDataRequested()
    {
        // Sanity-check counterpart to the IsAllDataRequested test: with full sampling,
        // db.* tags must be populated.
        using var fullCollector = new ActivityCollector(ActivitySamplingResult.AllDataAndRecorded);
        var options = new MongoDBInstrumentationOptions { CaptureCommandText = true };
        var subscriber = CreateSubscriber(options);

        InvokeStarted(subscriber, CommandEventFactory.Started());
        InvokeSucceeded(subscriber, CommandEventFactory.Succeeded());

        var activity = fullCollector.Stopped.Should().ContainSingle().Subject;
        activity.GetTagItem(SemConv.DbCollectionName).Should().Be("users");
        activity.GetTagItem(SemConv.DbSystem).Should().Be("mongodb");
    }
}
