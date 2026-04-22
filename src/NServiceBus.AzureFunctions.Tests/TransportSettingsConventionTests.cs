namespace NServiceBus.AzureFunctions.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Particular.Approvals;

/// <summary>
/// Verifies that every public settable property on <see cref="AzureServiceBusTransport"/> has been
/// explicitly considered for exposure on <see cref="AzureServiceBusServerlessTransport"/>.
/// A mapped property is one that is exposed with the same name on the serverless transport.
/// An unmapped property is one that was deliberately not exposed.
///
/// When a Renovate PR bumps NServiceBus.Transport.AzureServiceBus and the transport gains new properties,
/// this test will fail, forcing someone to make an explicit decision about whether to expose the new
/// property on the serverless transport before approving the update.
/// </summary>
[TestFixture]
public class TransportSettingsConventionTests
{
    [Test]
    public void TransportProperties_AreExplicitlyMappedOrNotMapped()
    {
        var transportProperties = typeof(AzureServiceBusTransport)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => !IsObsolete(p))
            .Where(p => p.SetMethod is not null && p.SetMethod.IsPublic)
            .ToList();

        var serverlessPropertyNames = typeof(AzureServiceBusServerlessTransport)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .ToHashSet();

        var mapped = new List<string>();
        var unmapped = new List<string>();

        foreach (var property in transportProperties)
        {
            if (serverlessPropertyNames.Contains(property.Name))
            {
                mapped.Add(property.Name);
            }
            else
            {
                unmapped.Add(property.Name);
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine("Mapped properties:");
        foreach (var name in mapped.OrderBy(x => x))
        {
            builder.AppendLine($"  - {name}");
        }

        builder.AppendLine();
        builder.AppendLine("Unmapped properties:");
        foreach (var name in unmapped.OrderBy(x => x))
        {
            builder.AppendLine($"  - {name}");
        }

        Approver.Verify(builder.ToString());
    }

    static bool IsObsolete(PropertyInfo property)
    {
        if (property.GetCustomAttribute<ObsoleteAttribute>() is not null)
        {
            return true;
        }

        if (property.GetCustomAttributesData().Any(a => a.AttributeType.Name == "ObsoleteMetadataAttribute"))
        {
            return true;
        }

        if (property.SetMethod?.GetCustomAttribute<ObsoleteAttribute>() is not null)
        {
            return true;
        }

        return false;
    }
}
