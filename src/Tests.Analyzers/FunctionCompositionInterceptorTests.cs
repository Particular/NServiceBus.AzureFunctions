namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using Analyzer;
using NUnit.Framework;
using Particular.AnalyzerTesting;

[TestFixture]
public class FunctionCompositionInterceptorTests
{
    [Test]
    public void BasicInvocation() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionCompositionInterceptor>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithSource("""
                        using Microsoft.Azure.Functions.Worker.Builder;
                        using NServiceBus;

                        public class BasicInvocationTest
                        {
                            public void Configure(FunctionsApplicationBuilder builder)
                            {
                                builder.AddNServiceBusFunctions();
                            }
                        }
                        """, "test.cs")
            .WithProperty("build_property.OutputType", "Exe")
            .WithProperty("build_property.FunctionsExecutionModel", "isolated")
            .WithProperty("build_property.RootNamespace", "Test.Host")
            .WithInterceptorNamespace("NServiceBus")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void DuplicateInvocationsAreDeduped() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionCompositionInterceptor>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithSource("""
                        using Microsoft.Azure.Functions.Worker.Builder;
                        using NServiceBus;

                        public class DuplicateInvocationsTest
                        {
                            public void Configure(FunctionsApplicationBuilder builder)
                            {
                                builder.AddNServiceBusFunctions();
                                // Duplicate call, methods should be deduped with 2 InterceptsLocation attributes
                                builder.AddNServiceBusFunctions();
                            }
                        }
                        """, "test.cs")
            .WithProperty("build_property.OutputType", "Exe")
            .WithProperty("build_property.FunctionsExecutionModel", "isolated")
            .WithProperty("build_property.RootNamespace", "Test.Host")
            .WithInterceptorNamespace("NServiceBus")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void NoInvocationStillEmitsCompositionClass() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionCompositionInterceptor>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithSource("""
                        using Microsoft.Azure.Functions.Worker.Builder;
                        using Microsoft.Extensions.Hosting;

                        public class NoInvocationTest
                        {
                            public void Configure(FunctionsApplicationBuilder builder, IHostEnvironment env)
                            {
                                // No call to AddNServiceBusFunctions
                            }
                        }
                        """, "test.cs")
            .WithProperty("build_property.OutputType", "Exe")
            .WithProperty("build_property.FunctionsExecutionModel", "isolated")
            .WithProperty("build_property.RootNamespace", "Test.Host")
            .WithInterceptorNamespace("NServiceBus")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void NonIsolatedHostEmitsNoGeneratedSources() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionCompositionInterceptor>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithSource("""
                        using Microsoft.Azure.Functions.Worker.Builder;
                        using NServiceBus;

                        public class NonIsolatedHostTest
                        {
                            public void Configure(FunctionsApplicationBuilder builder)
                            {
                                builder.AddNServiceBusFunctions();
                            }
                        }
                        """, "test.cs")
            .WithProperty("build_property.OutputType", "Exe")
            // No FunctionsExecutionModel property => not an isolated host project, so both the
            // composition class and the interceptor are suppressed.
            .WithProperty("build_property.RootNamespace", "Test.Host")
            .WithInterceptorNamespace("NServiceBus")
            .Run()
            .Approve()
            .AssertRunsAreEqual();
}