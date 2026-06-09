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
            .WithInterceptorNamespace("NServiceBus")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void LocalRegistrationsWithoutInvocationStillEmitCompositionClass() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionCompositionInterceptor>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource(TestSources.ValidFunction, "test.cs")
            .WithInterceptorNamespace("NServiceBus")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void NoInvocationAndNoRegistrationsEmitNoGeneratedSources() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionCompositionInterceptor>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithSource("""
                        public class NoInvocationOrRegistrationsTest
                        {
                        }
                        """, "test.cs")
            .WithInterceptorNamespace("NServiceBus")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void CopycatMethodDoesNotEmitGeneratedSources() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionCompositionInterceptor>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithSource("""
                        using Microsoft.Azure.Functions.Worker.Builder;

                        namespace Demo;

                        public static class HostApplicationBuilderExtensions
                        {
                            public static void AddNServiceBusFunctions(this FunctionsApplicationBuilder builder)
                            {
                            }
                        }

                        public class CopycatMethodTest
                        {
                            public void Configure(FunctionsApplicationBuilder builder)
                            {
                                builder.AddNServiceBusFunctions();
                            }
                        }
                        """, "test.cs")
            .WithInterceptorNamespace("NServiceBus")
            .Run()
            .Approve()
            .AssertRunsAreEqual();
}