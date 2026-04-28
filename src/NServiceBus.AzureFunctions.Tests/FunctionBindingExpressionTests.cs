namespace NServiceBus.AzureFunctions.Tests;

using Microsoft.Extensions.Configuration;
using NUnit.Framework;

[TestFixture]
public class FunctionBindingExpressionTests
{
    [Test]
    public void Resolve_plain_value_returns_as_is()
    {
        var config = new ConfigurationBuilder().Build();

        var result = FunctionBindingExpression.Resolve("billing-api", config);

        Assert.That(result, Is.EqualTo("billing-api"));
    }

    [Test]
    public void Resolve_empty_string_returns_as_is()
    {
        var config = new ConfigurationBuilder().Build();

        var result = FunctionBindingExpression.Resolve("", config);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Resolve_single_token_replaces_with_config_value()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("myQueue", "actual-queue-name")])
            .Build();

        var result = FunctionBindingExpression.Resolve("%myQueue%", config);

        Assert.That(result, Is.EqualTo("actual-queue-name"));
    }

    [Test]
    public void Resolve_embedded_token_surrounded_by_literals()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("billingPrefix", "billing")])
            .Build();

        var result = FunctionBindingExpression.Resolve("%billingPrefix%-api", config);

        Assert.That(result, Is.EqualTo("billing-api"));
    }

    [Test]
    public void Resolve_multiple_tokens()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new("prefix", "myapp"),
                new("env", "prod"),
                new("name", "orders"),
            ])
            .Build();

        var result = FunctionBindingExpression.Resolve("%prefix%-%env%-%name%", config);

        Assert.That(result, Is.EqualTo("myapp-prod-orders"));
    }

    [Test]
    public void Resolve_token_with_slash_separator()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("tenantId", "tenant42")])
            .Build();

        var result = FunctionBindingExpression.Resolve("%tenantId%/queuename", config);

        Assert.That(result, Is.EqualTo("tenant42/queuename"));
    }

    [Test]
    public void Resolve_throws_when_setting_not_found()
    {
        var config = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            FunctionBindingExpression.Resolve("%missingSetting%", config));

        Assert.That(ex.Message, Does.Contain("missingSetting"));
    }

    [Test]
    public void Resolve_throws_when_one_of_multiple_tokens_not_found()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("prefix", "myapp")])
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            FunctionBindingExpression.Resolve("%prefix%-%missing%", config));

        Assert.That(ex.Message, Does.Contain("missing"));
    }

    [Test]
    public void Resolve_null_configuration_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionBindingExpression.Resolve("value", null!));
    }

    [Test]
    public void Resolve_whitespace_setting_name()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("key", "value")])
            .Build();

        // % % with whitespace inside is technically a valid token
        var ex = Assert.Throws<InvalidOperationException>(() =>
            FunctionBindingExpression.Resolve("% %", config));

        Assert.That(ex.Message, Does.Contain(" "));
    }

    [Test]
    public void Resolve_nested_section_with_colon_separator()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ServiceBus:QueueName", "orders")])
            .Build();

        var result = FunctionBindingExpression.Resolve("%ServiceBus:QueueName%", config);

        Assert.That(result, Is.EqualTo("orders"));
    }

    [Test]
    public void Resolve_nested_section_with_double_underscore_separator()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ServiceBus__QueueName", "orders")])
            .Build();

        var result = FunctionBindingExpression.Resolve("%ServiceBus__QueueName%", config);

        Assert.That(result, Is.EqualTo("orders"));
    }

    [Test]
    public void Resolve_nested_section_combined_with_prefix()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new("Connections:ServiceBus", "my-connection"),
                new("prefix", "billing"),
            ])
            .Build();

        var result = FunctionBindingExpression.Resolve("%prefix%-%Connections:ServiceBus%", config);

        Assert.That(result, Is.EqualTo("billing-my-connection"));
    }
}