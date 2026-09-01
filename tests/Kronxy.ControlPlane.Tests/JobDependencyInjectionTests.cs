using System;
using System.Collections.Generic;
using Kronxy.Application;
using Kronxy.Application.Jobs;
using Kronxy.Domain.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class JobDependencyInjectionTests
{
	[Fact]
	public void AddApplication_RegistersControlPlaneServices()
	{
		ServiceCollection serviceCollection = new ServiceCollection();
		serviceCollection.AddApplication();
		Assert.Contains<ServiceDescriptor>((IEnumerable<ServiceDescriptor>)serviceCollection, (Predicate<ServiceDescriptor>)((ServiceDescriptor descriptor) => descriptor.ServiceType == typeof(IJobStateMachine) && descriptor.ImplementationType == typeof(JobStateMachine) && descriptor.Lifetime == ServiceLifetime.Singleton));
		Assert.Contains<ServiceDescriptor>((IEnumerable<ServiceDescriptor>)serviceCollection, (Predicate<ServiceDescriptor>)((ServiceDescriptor descriptor) => descriptor.ServiceType == typeof(IJobIdGenerator) && descriptor.ImplementationType == typeof(DefaultJobIdGenerator) && descriptor.Lifetime == ServiceLifetime.Singleton));
		Assert.Contains<ServiceDescriptor>((IEnumerable<ServiceDescriptor>)serviceCollection, (Predicate<ServiceDescriptor>)((ServiceDescriptor descriptor) => descriptor.ServiceType == typeof(IJobService) && descriptor.ImplementationType == typeof(JobService) && descriptor.Lifetime == ServiceLifetime.Scoped));
		Assert.Contains<ServiceDescriptor>((IEnumerable<ServiceDescriptor>)serviceCollection, (Predicate<ServiceDescriptor>)((ServiceDescriptor descriptor) => descriptor.ServiceType == typeof(IJobOrchestrator) && descriptor.ImplementationType == typeof(JobOrchestrator) && descriptor.Lifetime == ServiceLifetime.Scoped));
	}

	[Fact]
	public void AddApplication_RegistersValidSafeDefaults()
	{
		ServiceCollection serviceCollection = new ServiceCollection();
		serviceCollection.AddApplication();
		ServiceDescriptor serviceDescriptor = Assert.Single<ServiceDescriptor>((IEnumerable<ServiceDescriptor>)serviceCollection, (Predicate<ServiceDescriptor>)((ServiceDescriptor item) => item.ServiceType == typeof(JobControlOptions)));
		JobControlOptions jobControlOptions = Assert.IsType<JobControlOptions>(serviceDescriptor.ImplementationInstance);
		Assert.True(jobControlOptions.TryCreateLimits(out JobLimits limits));
		Assert.NotNull((object)limits);
		Assert.True(limits.MaxJobDuration > TimeSpan.Zero);
		Assert.True(limits.MaxAttempts > 0);
		Assert.True(limits.MaxAgentIterations > 0);
		Assert.True(limits.MaxAiCalls > 0);
	}

	[Fact]
	public void DefaultJobIdGenerator_GeneratesDistinctIds()
	{
		IJobIdGenerator jobIdGenerator = new DefaultJobIdGenerator();
		Guid guid = jobIdGenerator.NewId();
		Guid guid2 = jobIdGenerator.NewId();
		string text = jobIdGenerator.NewExternalId();
		string text2 = jobIdGenerator.NewExternalId();
		Assert.NotEqual<Guid>(Guid.Empty, guid);
		Assert.NotEqual<Guid>(Guid.Empty, guid2);
		Assert.NotEqual<Guid>(guid, guid2);
		Assert.StartsWith("KRX-", text);
		Assert.StartsWith("KRX-", text2);
		Assert.NotEqual<string>(text, text2);
	}
}
