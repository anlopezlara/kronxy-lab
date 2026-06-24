using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Email;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Bookings;
using Kronxy.Domain.Users;
using Kronxy.Domain.Catalogs;
using Kronxy.Infrastructure.Clock;
using Kronxy.Infrastructure.Data;
using Kronxy.Infrastructure.Email;
using Kronxy.Infrastructure.Repositories;
using Kronxy.Domain.Projects;
using Kronxy.Domain.ProjectTasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kronxy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();

        services.AddTransient<IEmailService, EmailService>();

        var connectionString =
            configuration.GetConnectionString("Database") ??
            throw new ArgumentNullException(nameof(configuration));

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });

        //------------ SERVICES------------------------------------------

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();

        services.AddScoped<IApartmentRepository, ApartmentRepository>();

        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectStatusRepository, ProjectStatusRepository>();
        services.AddScoped<IProjectTypeRepository, ProjectTypeRepository>();
        services.AddScoped<IProjectPriorityRepository, ProjectPriorityRepository>();

        services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();

        services.AddScoped<ICatalogRepository, CatalogRepository>();

        //---------------------------------------------------------------

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<ISqlConnectionFactory>(_ =>
            new SqlConnectionFactory(connectionString));

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        return services;
    }
}
