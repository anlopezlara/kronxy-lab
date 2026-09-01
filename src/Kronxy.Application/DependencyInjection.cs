using Kronxy.Application.Jobs;
﻿using Kronxy.Application.Abstractions.Behaviors;
using Kronxy.Domain.Bookings;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Kronxy.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));

            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddTransient<PricingService>();

        services.AddSingleton<IJobStateMachine, JobStateMachine>();
        services.AddSingleton<IJobIdGenerator, DefaultJobIdGenerator>();
        services.AddSingleton<IJobRunIdProvider, DeterministicJobRunIdProvider>();
        services.AddSingleton(JobControlOptions.SafeDefaults);

        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IJobOrchestrator, JobOrchestrator>();

        return services;
    }
}