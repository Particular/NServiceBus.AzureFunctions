namespace NServiceBus.AzureFunctions.Tests;

using AzureServiceBus;
using NUnit.Framework;

public class DeadLetterMessageTests
{
    [Test]
    public void Should_full_control_over_dead_letter_parameters()
    {
        var reason = "reason";
        var description = "description";
        var properties = new Dictionary<string, object> { { "SomeProperty", "SomeValue" } };
        var request = new DeadLetterRequest(reason, description, properties);

        Assert.AreEqual(reason, request.DeadLetterReason, "DeadLetterReason should be set correctly");
        Assert.AreEqual(description, request.DeadLetterErrorDescription, "DeadLetterErrorDescription should be set correctly");
        Assert.IsNotNull(request.PropertiesToModify, "PropertiesToModify should not be null");
        Assert.IsTrue(request.PropertiesToModify!.ContainsKey("SomeProperty"), "PropertiesToModify should contain 'SomeProperty'");
        Assert.AreEqual("SomeValue", request.PropertiesToModify["SomeProperty"], "PropertiesToModify['SomeProperty'] should be set correctly");
    }

    [Test]
    public void Should_convert_exception_to_dead_letter_request()
    {
        Exception exception;

        try
        {
            SimulateException();
        }
        catch (Exception e)
        {
            exception = e;
        }

        var request = new DeadLetterRequest(exception);

        // Make sure we follow microsoft guidance - https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues#application-level-dead-lettering
        Assert.AreEqual("System.InvalidOperationException - Test exception", request.DeadLetterReason, "DeadLetterReason should reflect exception type and message");
        Assert.AreEqual(request.DeadLetterErrorDescription, exception.StackTrace, "DeadLetterErrorDescription should contain stack trace");
        Assert.IsNull(request.PropertiesToModify, "PropertiesToModify should be null for exception-based dead lettering");

        void SimulateException()
        {
            throw new InvalidOperationException("Test exception");
            ;
        }
    }

    [Test]
    public void Should_truncate_dead_letter_reason_and_description_to_1024_characters()
    {
        var longReason = new string('A', 2000);
        var longDescription = new string('B', 3000);
        var request = new DeadLetterRequest(longReason, longDescription);

        Assert.AreEqual(new string('A', 1024), request.DeadLetterReason, "DeadLetterReason should match the first 1024 characters of the input");
        Assert.AreEqual(new string('B', 1024), request.DeadLetterErrorDescription, "DeadLetterErrorDescription should match the first 1024 characters of the input");
    }
}