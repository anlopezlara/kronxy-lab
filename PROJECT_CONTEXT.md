# PROJECT CONTEXT - kronxy-lab

Generated: 2026-06-04 10:45:08
Root: E:\dev\kronxy-lab

## TREE

[DIR]  src
[FILE] .dockerignore
[FILE] docker-compose.dcproj
[FILE] docker-compose.override.yml
[FILE] docker-compose.yml
[FILE] export_context.ps1
[FILE] global.json
[FILE] Kronxy.sln
[FILE] Kronxy.sln.DotSettings
[FILE] kronxy_api_full.txt
[FILE] launchSettings.json
[FILE] PROJECT_CONTEXT.md
[DIR]  src\Kronxy.Api
[DIR]  src\Kronxy.Application
[DIR]  src\Kronxy.Domain
[DIR]  src\Kronxy.Infrastructure
[DIR]  src\Kronxy.Api\Controllers
[DIR]  src\Kronxy.Api\Extensions
[DIR]  src\Kronxy.Api\Middleware
[DIR]  src\Kronxy.Api\Properties
[FILE] src\Kronxy.Api\appsettings.Development.json
[FILE] src\Kronxy.Api\appsettings.json
[FILE] src\Kronxy.Api\Bookify.Api.csproj.user
[FILE] src\Kronxy.Api\Dockerfile
[FILE] src\Kronxy.Api\Kronxy.Api.csproj
[FILE] src\Kronxy.Api\Program.cs
[DIR]  src\Kronxy.Api\Controllers\Apartments
[DIR]  src\Kronxy.Api\Controllers\Bookings
[DIR]  src\Kronxy.Api\Controllers\NewFolder
[DIR]  src\Kronxy.Api\Controllers\Projects
[DIR]  src\Kronxy.Api\Controllers\ProjectTasks
[DIR]  src\Kronxy.Api\Controllers\Users
[FILE] src\Kronxy.Api\Controllers\Apartments\ApartmentsController.cs
[FILE] src\Kronxy.Api\Controllers\Bookings\BookingsController.cs
[FILE] src\Kronxy.Api\Controllers\Bookings\ReserveBookingRequest.cs
[FILE] src\Kronxy.Api\Controllers\Projects\CreateProjectRequest.cs
[FILE] src\Kronxy.Api\Controllers\Projects\ProjectsController.cs
[FILE] src\Kronxy.Api\Controllers\Projects\UpdateProjectRequest.cs
[FILE] src\Kronxy.Api\Controllers\ProjectTasks\ChangeProjectTaskStatusRequest.cs
[FILE] src\Kronxy.Api\Controllers\ProjectTasks\CreateProjectTaskRequest.cs
[FILE] src\Kronxy.Api\Controllers\ProjectTasks\ProjectTasksController.cs
[FILE] src\Kronxy.Api\Controllers\ProjectTasks\UpdateProjectTaskRequest.cs
[FILE] src\Kronxy.Api\Controllers\Users\CreateUserRequest.cs
[FILE] src\Kronxy.Api\Controllers\Users\UpdateUserRequest.cs
[FILE] src\Kronxy.Api\Controllers\Users\UsersController.cs
[FILE] src\Kronxy.Api\Extensions\ApplicationBuilderExtensions.cs
[FILE] src\Kronxy.Api\Extensions\SeedDataExtensions.cs
[FILE] src\Kronxy.Api\Middleware\ExceptionHandlingMiddleware.cs
[FILE] src\Kronxy.Api\Properties\launchSettings.json
[DIR]  src\Kronxy.Application\Abstractions
[DIR]  src\Kronxy.Application\Apartments
[DIR]  src\Kronxy.Application\Bookings
[DIR]  src\Kronxy.Application\Exceptions
[DIR]  src\Kronxy.Application\Projects
[DIR]  src\Kronxy.Application\ProjectTasks
[DIR]  src\Kronxy.Application\Users
[FILE] src\Kronxy.Application\DependencyInjection.cs
[FILE] src\Kronxy.Application\Kronxy.Application.csproj
[DIR]  src\Kronxy.Application\Abstractions\Behaviors
[DIR]  src\Kronxy.Application\Abstractions\Clock
[DIR]  src\Kronxy.Application\Abstractions\Data
[DIR]  src\Kronxy.Application\Abstractions\Email
[DIR]  src\Kronxy.Application\Abstractions\Messaging
[FILE] src\Kronxy.Application\Abstractions\Behaviors\LoggingBehavior.cs
[FILE] src\Kronxy.Application\Abstractions\Behaviors\ValidationBehavior.cs
[FILE] src\Kronxy.Application\Abstractions\Clock\IDateTimeProvider.cs
[FILE] src\Kronxy.Application\Abstractions\Data\ISqlConnectionFactory.cs
[FILE] src\Kronxy.Application\Abstractions\Email\IEmailService.cs
[FILE] src\Kronxy.Application\Abstractions\Messaging\ICommand.cs
[FILE] src\Kronxy.Application\Abstractions\Messaging\ICommandHandler.cs
[FILE] src\Kronxy.Application\Abstractions\Messaging\IQuery.cs
[FILE] src\Kronxy.Application\Abstractions\Messaging\IQueryHandler.cs
[DIR]  src\Kronxy.Application\Apartments\SearchApartments
[FILE] src\Kronxy.Application\Apartments\SearchApartments\AddressResponse.cs
[FILE] src\Kronxy.Application\Apartments\SearchApartments\ApartmentResponse.cs
[FILE] src\Kronxy.Application\Apartments\SearchApartments\SearchApartmentsQuery.cs
[FILE] src\Kronxy.Application\Apartments\SearchApartments\SearchApartmentsQueryHandler.cs
[DIR]  src\Kronxy.Application\Bookings\CancelBooking
[DIR]  src\Kronxy.Application\Bookings\CompleteBooking
[DIR]  src\Kronxy.Application\Bookings\ConfirmBooking
[DIR]  src\Kronxy.Application\Bookings\GetBooking
[DIR]  src\Kronxy.Application\Bookings\RejectBooking
[DIR]  src\Kronxy.Application\Bookings\ReserveBooking
[FILE] src\Kronxy.Application\Bookings\CancelBooking\CancelBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\CancelBooking\CancelBookingCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\CompleteBooking\CompleteBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\CompleteBooking\CompleteBookingCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\ConfirmBooking\ConfirmBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\ConfirmBooking\ConfirmBookingCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\GetBooking\BookingResponse.cs
[FILE] src\Kronxy.Application\Bookings\GetBooking\GetBookingQuery.cs
[FILE] src\Kronxy.Application\Bookings\GetBooking\GetBookingQueryHandler.cs
[FILE] src\Kronxy.Application\Bookings\RejectBooking\RejectBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\RejectBooking\RejectBookingCommandCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\ReserveBooking\BookingReservedDomainEventHandler.cs
[FILE] src\Kronxy.Application\Bookings\ReserveBooking\ReserveBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\ReserveBooking\ReserveBookingCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\ReserveBooking\ReserveBookingCommandValidator.cs
[FILE] src\Kronxy.Application\Exceptions\ConcurrencyException.cs
[FILE] src\Kronxy.Application\Exceptions\ValidationError.cs
[FILE] src\Kronxy.Application\Exceptions\ValidationException.cs
[DIR]  src\Kronxy.Application\Projects\ActivateProject
[DIR]  src\Kronxy.Application\Projects\CreateProject
[DIR]  src\Kronxy.Application\Projects\DeactivateProject
[DIR]  src\Kronxy.Application\Projects\GetProject
[DIR]  src\Kronxy.Application\Projects\UpdateProject
[FILE] src\Kronxy.Application\Projects\ActivateProject\ActivateProjectCommand.cs
[FILE] src\Kronxy.Application\Projects\ActivateProject\ActivateProjectCommandHandler.cs
[FILE] src\Kronxy.Application\Projects\CreateProject\CreateProjectCommand.cs
[FILE] src\Kronxy.Application\Projects\CreateProject\CreateProjectCommandHandler.cs
[FILE] src\Kronxy.Application\Projects\DeactivateProject\DeactivateProjectCommand.cs
[FILE] src\Kronxy.Application\Projects\DeactivateProject\DeactivateProjectCommandHandler.cs
[FILE] src\Kronxy.Application\Projects\GetProject\GetProjectQuery.cs
[FILE] src\Kronxy.Application\Projects\GetProject\GetProjectQueryHandler.cs
[FILE] src\Kronxy.Application\Projects\GetProject\GetProjectsQuery.cs
[FILE] src\Kronxy.Application\Projects\GetProject\GetProjectsQueryHandler.cs
[FILE] src\Kronxy.Application\Projects\GetProject\ProjectResponse.cs
[FILE] src\Kronxy.Application\Projects\UpdateProject\UpdateProjectCommand.cs
[FILE] src\Kronxy.Application\Projects\UpdateProject\UpdateProjectCommandHandler.cs
[DIR]  src\Kronxy.Application\ProjectTasks\ActivateProjectTask
[DIR]  src\Kronxy.Application\ProjectTasks\ChangeProjectTaskStatus
[DIR]  src\Kronxy.Application\ProjectTasks\CreateProjectTask
[DIR]  src\Kronxy.Application\ProjectTasks\DeactivateProjectTask
[DIR]  src\Kronxy.Application\ProjectTasks\GetProjectTask
[DIR]  src\Kronxy.Application\ProjectTasks\UpdateProjectTask
[FILE] src\Kronxy.Application\ProjectTasks\ActivateProjectTask\ActivateProjectTaskCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\ActivateProjectTask\ActivateProjectTaskCommandHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\ChangeProjectTaskStatus\ChangeProjectTaskStatusCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\ChangeProjectTaskStatus\ChangeProjectTaskStatusCommandHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\CreateProjectTask\CreateProjectTaskCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\CreateProjectTask\CreateProjectTaskCommandHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\DeactivateProjectTask\DeactivateProjectTaskCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\DeactivateProjectTask\DeactivateProjectTaskCommandHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTaskQuery.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTaskQueryHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByProjectQuery.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByProjectQueryHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByUserQuery.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByUserQueryHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksQuery.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksQueryHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\ProjectTaskResponse.cs
[FILE] src\Kronxy.Application\ProjectTasks\UpdateProjectTask\UpdateProjectTaskCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\UpdateProjectTask\UpdateProjectTaskCommandHandler.cs
[DIR]  src\Kronxy.Application\Users\ActivateUser
[DIR]  src\Kronxy.Application\Users\CreateUser
[DIR]  src\Kronxy.Application\Users\DeactivateUser
[DIR]  src\Kronxy.Application\Users\GetUser
[DIR]  src\Kronxy.Application\Users\UpdateUser
[FILE] src\Kronxy.Application\Users\ActivateUser\ActivateUserCommand.cs
[FILE] src\Kronxy.Application\Users\ActivateUser\ActivateUserCommandHandler.cs
[FILE] src\Kronxy.Application\Users\CreateUser\CreateUserCommand.cs
[FILE] src\Kronxy.Application\Users\CreateUser\CreateUserCommandHandler.cs
[FILE] src\Kronxy.Application\Users\DeactivateUser\DeactivateUserCommand.cs
[FILE] src\Kronxy.Application\Users\DeactivateUser\DeactivateUserCommandHandler.cs
[FILE] src\Kronxy.Application\Users\GetUser\GetUserQuery.cs
[FILE] src\Kronxy.Application\Users\GetUser\GetUserQueryHandler.cs
[FILE] src\Kronxy.Application\Users\GetUser\GetUsersQuery.cs
[FILE] src\Kronxy.Application\Users\GetUser\GetUsersQueryHandler.cs
[FILE] src\Kronxy.Application\Users\GetUser\UserResponse.cs
[FILE] src\Kronxy.Application\Users\UpdateUser\UpdateUserCommand.cs
[FILE] src\Kronxy.Application\Users\UpdateUser\UpdateUserCommandHandler.cs
[FILE] src\Kronxy.Application\Users\UpdateUser\UpdateUserCommandValidator.cs
[DIR]  src\Kronxy.Domain\Abstractions
[DIR]  src\Kronxy.Domain\Apartments
[DIR]  src\Kronxy.Domain\Bookings
[DIR]  src\Kronxy.Domain\Projects
[DIR]  src\Kronxy.Domain\ProjectTasks
[DIR]  src\Kronxy.Domain\Reviews
[DIR]  src\Kronxy.Domain\Shared
[DIR]  src\Kronxy.Domain\Users
[FILE] src\Kronxy.Domain\Kronxy.Domain.csproj
[FILE] src\Kronxy.Domain\Abstractions\Entity.cs
[FILE] src\Kronxy.Domain\Abstractions\Error.cs
[FILE] src\Kronxy.Domain\Abstractions\IDomainEvent.cs
[FILE] src\Kronxy.Domain\Abstractions\IUnitOfWork.cs
[FILE] src\Kronxy.Domain\Abstractions\Result.cs
[FILE] src\Kronxy.Domain\Apartments\Address.cs
[FILE] src\Kronxy.Domain\Apartments\Amenity.cs
[FILE] src\Kronxy.Domain\Apartments\Apartment.cs
[FILE] src\Kronxy.Domain\Apartments\ApartmentErrors.cs
[FILE] src\Kronxy.Domain\Apartments\Description.cs
[FILE] src\Kronxy.Domain\Apartments\IApartmentRepository.cs
[FILE] src\Kronxy.Domain\Apartments\Name.cs
[DIR]  src\Kronxy.Domain\Bookings\Events
[FILE] src\Kronxy.Domain\Bookings\Booking.cs
[FILE] src\Kronxy.Domain\Bookings\BookingErrors.cs
[FILE] src\Kronxy.Domain\Bookings\BookingStatus.cs
[FILE] src\Kronxy.Domain\Bookings\DateRange.cs
[FILE] src\Kronxy.Domain\Bookings\IBookingRepository.cs
[FILE] src\Kronxy.Domain\Bookings\PricingDetails.cs
[FILE] src\Kronxy.Domain\Bookings\PricingService.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingCancelledDomainEvent.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingCompletedDomainEvent.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingConfirmedDomainEvent.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingRejectedDomainEvent.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingReservedDomainEvent.cs
[FILE] src\Kronxy.Domain\Projects\IProjectRepository.cs
[FILE] src\Kronxy.Domain\Projects\Project.cs
[FILE] src\Kronxy.Domain\Projects\ProjectErrors.cs
[FILE] src\Kronxy.Domain\Projects\ProjectPriority.cs
[FILE] src\Kronxy.Domain\Projects\ProjectStatus.cs
[FILE] src\Kronxy.Domain\ProjectTasks\IProjectTaskRepository.cs
[FILE] src\Kronxy.Domain\ProjectTasks\ProjectTask.cs
[FILE] src\Kronxy.Domain\ProjectTasks\ProjectTaskErrors.cs
[FILE] src\Kronxy.Domain\ProjectTasks\ProjectTaskPriority.cs
[FILE] src\Kronxy.Domain\ProjectTasks\ProjectTaskStatus.cs
[DIR]  src\Kronxy.Domain\Reviews\Events
[FILE] src\Kronxy.Domain\Reviews\Comment.cs
[FILE] src\Kronxy.Domain\Reviews\Rating.cs
[FILE] src\Kronxy.Domain\Reviews\Review.cs
[FILE] src\Kronxy.Domain\Reviews\ReviewErrors.cs
[FILE] src\Kronxy.Domain\Reviews\Events\ReviewCreatedDomainEvent.cs
[FILE] src\Kronxy.Domain\Shared\Currency.cs
[FILE] src\Kronxy.Domain\Shared\Money.cs
[DIR]  src\Kronxy.Domain\Users\Events
[FILE] src\Kronxy.Domain\Users\Email.cs
[FILE] src\Kronxy.Domain\Users\FirstName.cs
[FILE] src\Kronxy.Domain\Users\IUserRepository.cs
[FILE] src\Kronxy.Domain\Users\LastName.cs
[FILE] src\Kronxy.Domain\Users\PhoneNumber.cs
[FILE] src\Kronxy.Domain\Users\User.cs
[FILE] src\Kronxy.Domain\Users\UserErrors.cs
[FILE] src\Kronxy.Domain\Users\Username.cs
[FILE] src\Kronxy.Domain\Users\Events\UserCreatedDomainEvent.cs
[DIR]  src\Kronxy.Infrastructure\Clock
[DIR]  src\Kronxy.Infrastructure\Configurations
[DIR]  src\Kronxy.Infrastructure\Data
[DIR]  src\Kronxy.Infrastructure\Email
[DIR]  src\Kronxy.Infrastructure\Migrations
[DIR]  src\Kronxy.Infrastructure\Repositories
[FILE] src\Kronxy.Infrastructure\ApplicationDbContext.cs
[FILE] src\Kronxy.Infrastructure\DependencyInjection.cs
[FILE] src\Kronxy.Infrastructure\Kronxy.Infrastructure.csproj
[FILE] src\Kronxy.Infrastructure\Clock\DateTimeProvider.cs
[FILE] src\Kronxy.Infrastructure\Configurations\ApartmentConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\BookingConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\ProjectConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\ProjectTaskConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\ReviewConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\UserConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Data\DateOnlyTypeHandler.cs
[FILE] src\Kronxy.Infrastructure\Data\SqlConnectionFactory.cs
[FILE] src\Kronxy.Infrastructure\Email\EmailService.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20240104150149_Create_Database.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20240104150149_Create_Database.Designer.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260428145405_Add_User_Management_Fields.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260428145405_Add_User_Management_Fields.Designer.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260603180007_Add_Projects.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260603180007_Add_Projects.Designer.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260603182025_Add_ProjectTasks.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260603182025_Add_ProjectTasks.Designer.cs
[FILE] src\Kronxy.Infrastructure\Migrations\ApplicationDbContextModelSnapshot.cs
[FILE] src\Kronxy.Infrastructure\Repositories\ApartmentRepository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\BookingRepository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\ProjectRepository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\ProjectTaskRepository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\Repository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\UserRepository.cs

## FILE CONTENTS


---

### FILE: docker-compose.override.yml

~~~text
version: '3.4'

services:
  bookify.api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_HTTP_PORTS=5000
      - ASPNETCORE_HTTPS_PORTS=5001
    ports:
      - 5000:5000
      - 5001:5001
    volumes:
      - ${APPDATA}/Microsoft/UserSecrets:/root/.microsoft/usersecrets:ro
      - ${APPDATA}/ASP.NET/Https:/root/.aspnet/https:ro
~~~

---

### FILE: docker-compose.yml

~~~text
version: '3.4'

services:
  bookify.api:
    image: ${DOCKER_REGISTRY-}bookifyapi
    container_name: Bookify.Api
    build:
      context: .
      dockerfile: src/Bookify.Api/Dockerfile
    depends_on:
      - bookify-db

  bookify-db:
    image: postgres:latest
    container_name: Bookify.Db
    environment:
      - POSTGRES_DB=bookify
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
    volumes:
      - ./.containers/database:/var/lib/postgresql/data
    ports:
      - 5432:5432

~~~

---

### FILE: export_context.ps1

~~~text
$Root = "E:\dev\kronxy-lab"
$Out  = Join-Path $Root "PROJECT_CONTEXT.md"

$ExcludeDirs = @(".git",".vs",".idea","bin","obj","node_modules","dist","build","target",".gradle",".mvn","__pycache__")
$IncludeExt = @(".cs",".csproj",".sln",".json",".xml",".config",".yml",".yaml",".md",".txt",".ps1",".bat",".cmd",".sql",".js",".ts",".html",".css",".java",".properties")

function IsExcluded($Path) {
    foreach ($d in $ExcludeDirs) {
        if ($Path -like "*\$d\*" -or $Path -like "*\$d") {
            return $true
        }
    }
    return $false
}

Set-Content -Path $Out -Value "# PROJECT CONTEXT - kronxy-lab" -Encoding UTF8
Add-Content -Path $Out -Value ""
Add-Content -Path $Out -Value "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Content -Path $Out -Value "Root: $Root"
Add-Content -Path $Out -Value ""
Add-Content -Path $Out -Value "## TREE"
Add-Content -Path $Out -Value ""

Get-ChildItem $Root -Recurse -Force |
    Where-Object { -not (IsExcluded $_.FullName) } |
    ForEach-Object {
        $rel = $_.FullName.Replace($Root, "").TrimStart("\")
        if ($_.PSIsContainer) {
            Add-Content -Path $Out -Value "[DIR]  $rel"
        } else {
            Add-Content -Path $Out -Value "[FILE] $rel"
        }
    }

Add-Content -Path $Out -Value ""
Add-Content -Path $Out -Value "## FILE CONTENTS"
Add-Content -Path $Out -Value ""

Get-ChildItem $Root -Recurse -File -Force |
    Where-Object {
        -not (IsExcluded $_.FullName) -and
        $IncludeExt -contains $_.Extension.ToLower()
    } |
    Sort-Object FullName |
    ForEach-Object {
        $rel = $_.FullName.Replace($Root, "").TrimStart("\")

        Add-Content -Path $Out -Value ""
        Add-Content -Path $Out -Value "---"
        Add-Content -Path $Out -Value ""
        Add-Content -Path $Out -Value "### FILE: $rel"
        Add-Content -Path $Out -Value ""
        Add-Content -Path $Out -Value "~~~text"

        try {
            $content = Get-Content $_.FullName -Raw -ErrorAction Stop
            Add-Content -Path $Out -Value $content
        } catch {
            Add-Content -Path $Out -Value "[ERROR leyendo archivo]"
        }

        Add-Content -Path $Out -Value "~~~"
    }

Write-Host ("OK generado: " + $Out)
~~~

---

### FILE: global.json

~~~text
{
  "sdk": {
    "version": "8.0.418"
  }
}

~~~

---

### FILE: Kronxy.sln

~~~text

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Kronxy.Domain", "src\Kronxy.Domain\Kronxy.Domain.csproj", "{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Kronxy.Application", "src\Kronxy.Application\Kronxy.Application.csproj", "{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Kronxy.Infrastructure", "src\Kronxy.Infrastructure\Kronxy.Infrastructure.csproj", "{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Kronxy.Api", "src\Kronxy.Api\Kronxy.Api.csproj", "{79E174F1-8A7B-4067-9671-31D3E330A2F0}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}.Release|Any CPU.Build.0 = Release|Any CPU
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}.Release|Any CPU.Build.0 = Release|Any CPU
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}.Release|Any CPU.Build.0 = Release|Any CPU
		{79E174F1-8A7B-4067-9671-31D3E330A2F0}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{79E174F1-8A7B-4067-9671-31D3E330A2F0}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{79E174F1-8A7B-4067-9671-31D3E330A2F0}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{79E174F1-8A7B-4067-9671-31D3E330A2F0}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE} = {D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3} = {D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4} = {D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}
		{79E174F1-8A7B-4067-9671-31D3E330A2F0} = {D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}
	EndGlobalSection
EndGlobal

~~~

---

### FILE: kronxy_api_full.txt

~~~text

### FILE: src/Kronxy.Api/Controllers/Apartments/ApartmentsController.cs ###

using Kronxy.Application.Apartments.SearchApartments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Apartments;

[ApiController]
[Route("api/apartments")]
public class ApartmentsController : ControllerBase
{
    private readonly ISender _sender;

    public ApartmentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> SearchApartments(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var query = new SearchApartmentsQuery(startDate, endDate);

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Controllers/Bookings/BookingsController.cs ###

using Kronxy.Application.Bookings.GetBooking;
using Kronxy.Application.Bookings.ReserveBooking;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Bookings;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly ISender _sender;

    public BookingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetBookingQuery(id);

        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> ReserveBooking(
        ReserveBookingRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReserveBookingCommand(
            request.ApartmentId,
            request.UserId,
            request.StartDate,
            request.EndDate);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetBooking), new { id = result.Value }, result.Value);
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Controllers/Users/UsersController.cs ###

using Kronxy.Application.Users.CreateUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Users;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            request.Username,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(CreateUser), new { id = result.Value }, result.Value);
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Program.cs ###

using Kronxy.Api.Extensions;
using Kronxy.Application;
using Kronxy.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger (opcional, no afecta endpoints)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Kronxy API",
        Version = "v1"
    });
});

// Clean Architecture layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Swagger (opcional)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kronxy API v1");
});

// Solo DEV: migraciones/seed
if (app.Environment.IsDevelopment())
{
    app.ApplyMigrations();
    // app.SeedData();
}

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCustomExceptionHandler();

app.MapControllers();

app.Run();

### END FILE ###


### FILE: src/Kronxy.Api/Extensions/ApplicationBuilderExtensions.cs ###

using Kronxy.Api.Middleware;
using Kronxy.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Database.Migrate();
    }

    public static void UseCustomExceptionHandler(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Extensions/SeedDataExtensions.cs ###

using Bogus;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Domain.Apartments;
using Dapper;

namespace Kronxy.Api.Extensions;

public static class SeedDataExtensions
{
    public static void SeedData(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        var sqlConnectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        using var connection = sqlConnectionFactory.CreateConnection();

        var faker = new Faker();

        List<object> apartments = new();
        for (var i = 0; i < 100; i++)
        {
            apartments.Add(new
            {
                Id = Guid.NewGuid(),
                Name = faker.Company.CompanyName(),
                Description = "Amazing view",
                Country = faker.Address.Country(),
                State = faker.Address.State(),
                ZipCode = faker.Address.ZipCode(),
                City = faker.Address.City(),
                Street = faker.Address.StreetAddress(),
                PriceAmount = faker.Random.Decimal(50, 1000),
                PriceCurrency = "USD",
                CleaningFeeAmount = faker.Random.Decimal(25, 200),
                CleaningFeeCurrency = "USD",
                Amenities = new List<int> { (int)Amenity.Parking, (int)Amenity.MountainView },
                LastBookedOn = DateTime.MinValue
            });
        }

        const string sql = """
            INSERT INTO public.apartments
            (id, "name", description, address_country, address_state, address_zip_code, address_city, address_street, price_amount, price_currency, cleaning_fee_amount, cleaning_fee_currency, amenities, last_booked_on_utc)
            VALUES(@Id, @Name, @Description, @Country, @State, @ZipCode, @City, @Street, @PriceAmount, @PriceCurrency, @CleaningFeeAmount, @CleaningFeeCurrency, @Amenities, @LastBookedOn);
            """;

        connection.Execute(sql, apartments);
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Middleware/ExceptionHandlingMiddleware.cs ###

using Kronxy.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

            var exceptionDetails = GetExceptionDetails(exception);

            var problemDetails = new ProblemDetails
            {
                Status = exceptionDetails.Status,
                Type = exceptionDetails.Type,
                Title = exceptionDetails.Title,
                Detail = exceptionDetails.Detail,
            };

            if (exceptionDetails.Errors is not null)
            {
                problemDetails.Extensions["errors"] = exceptionDetails.Errors;
            }

            context.Response.StatusCode = exceptionDetails.Status;

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }

    private static ExceptionDetails GetExceptionDetails(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => new ExceptionDetails(
                StatusCodes.Status400BadRequest,
                "ValidationFailure",
                "Validation error",
                "One or more validation errors has occurred",
                validationException.Errors),
            _ => new ExceptionDetails(
                StatusCodes.Status500InternalServerError,
                "ServerError",
                "Server error",
                "An unexpected error has occurred",
                null)
        };
    }

    internal record ExceptionDetails(
        int Status,
        string Type,
        string Title,
        string Detail,
        IEnumerable<object>? Errors);
}

### END FILE ###


~~~

---

### FILE: launchSettings.json

~~~text
{
  "profiles": {
    "Docker Compose": {
      "commandName": "DockerCompose",
      "commandVersion": "1.0",
      "serviceActions": {
        "bookify.api": "StartDebugging"
      }
    }
  }
}
~~~

---

### FILE: PROJECT_CONTEXT.md

~~~text
# PROJECT CONTEXT - kronxy-lab

Generated: 2026-06-04 10:45:08
Root: E:\dev\kronxy-lab

## TREE

[DIR]  src
[FILE] .dockerignore
[FILE] docker-compose.dcproj
[FILE] docker-compose.override.yml
[FILE] docker-compose.yml
[FILE] export_context.ps1
[FILE] global.json
[FILE] Kronxy.sln
[FILE] Kronxy.sln.DotSettings
[FILE] kronxy_api_full.txt
[FILE] launchSettings.json
[FILE] PROJECT_CONTEXT.md
[DIR]  src\Kronxy.Api
[DIR]  src\Kronxy.Application
[DIR]  src\Kronxy.Domain
[DIR]  src\Kronxy.Infrastructure
[DIR]  src\Kronxy.Api\Controllers
[DIR]  src\Kronxy.Api\Extensions
[DIR]  src\Kronxy.Api\Middleware
[DIR]  src\Kronxy.Api\Properties
[FILE] src\Kronxy.Api\appsettings.Development.json
[FILE] src\Kronxy.Api\appsettings.json
[FILE] src\Kronxy.Api\Bookify.Api.csproj.user
[FILE] src\Kronxy.Api\Dockerfile
[FILE] src\Kronxy.Api\Kronxy.Api.csproj
[FILE] src\Kronxy.Api\Program.cs
[DIR]  src\Kronxy.Api\Controllers\Apartments
[DIR]  src\Kronxy.Api\Controllers\Bookings
[DIR]  src\Kronxy.Api\Controllers\NewFolder
[DIR]  src\Kronxy.Api\Controllers\Projects
[DIR]  src\Kronxy.Api\Controllers\ProjectTasks
[DIR]  src\Kronxy.Api\Controllers\Users
[FILE] src\Kronxy.Api\Controllers\Apartments\ApartmentsController.cs
[FILE] src\Kronxy.Api\Controllers\Bookings\BookingsController.cs
[FILE] src\Kronxy.Api\Controllers\Bookings\ReserveBookingRequest.cs
[FILE] src\Kronxy.Api\Controllers\Projects\CreateProjectRequest.cs
[FILE] src\Kronxy.Api\Controllers\Projects\ProjectsController.cs
[FILE] src\Kronxy.Api\Controllers\Projects\UpdateProjectRequest.cs
[FILE] src\Kronxy.Api\Controllers\ProjectTasks\ChangeProjectTaskStatusRequest.cs
[FILE] src\Kronxy.Api\Controllers\ProjectTasks\CreateProjectTaskRequest.cs
[FILE] src\Kronxy.Api\Controllers\ProjectTasks\ProjectTasksController.cs
[FILE] src\Kronxy.Api\Controllers\ProjectTasks\UpdateProjectTaskRequest.cs
[FILE] src\Kronxy.Api\Controllers\Users\CreateUserRequest.cs
[FILE] src\Kronxy.Api\Controllers\Users\UpdateUserRequest.cs
[FILE] src\Kronxy.Api\Controllers\Users\UsersController.cs
[FILE] src\Kronxy.Api\Extensions\ApplicationBuilderExtensions.cs
[FILE] src\Kronxy.Api\Extensions\SeedDataExtensions.cs
[FILE] src\Kronxy.Api\Middleware\ExceptionHandlingMiddleware.cs
[FILE] src\Kronxy.Api\Properties\launchSettings.json
[DIR]  src\Kronxy.Application\Abstractions
[DIR]  src\Kronxy.Application\Apartments
[DIR]  src\Kronxy.Application\Bookings
[DIR]  src\Kronxy.Application\Exceptions
[DIR]  src\Kronxy.Application\Projects
[DIR]  src\Kronxy.Application\ProjectTasks
[DIR]  src\Kronxy.Application\Users
[FILE] src\Kronxy.Application\DependencyInjection.cs
[FILE] src\Kronxy.Application\Kronxy.Application.csproj
[DIR]  src\Kronxy.Application\Abstractions\Behaviors
[DIR]  src\Kronxy.Application\Abstractions\Clock
[DIR]  src\Kronxy.Application\Abstractions\Data
[DIR]  src\Kronxy.Application\Abstractions\Email
[DIR]  src\Kronxy.Application\Abstractions\Messaging
[FILE] src\Kronxy.Application\Abstractions\Behaviors\LoggingBehavior.cs
[FILE] src\Kronxy.Application\Abstractions\Behaviors\ValidationBehavior.cs
[FILE] src\Kronxy.Application\Abstractions\Clock\IDateTimeProvider.cs
[FILE] src\Kronxy.Application\Abstractions\Data\ISqlConnectionFactory.cs
[FILE] src\Kronxy.Application\Abstractions\Email\IEmailService.cs
[FILE] src\Kronxy.Application\Abstractions\Messaging\ICommand.cs
[FILE] src\Kronxy.Application\Abstractions\Messaging\ICommandHandler.cs
[FILE] src\Kronxy.Application\Abstractions\Messaging\IQuery.cs
[FILE] src\Kronxy.Application\Abstractions\Messaging\IQueryHandler.cs
[DIR]  src\Kronxy.Application\Apartments\SearchApartments
[FILE] src\Kronxy.Application\Apartments\SearchApartments\AddressResponse.cs
[FILE] src\Kronxy.Application\Apartments\SearchApartments\ApartmentResponse.cs
[FILE] src\Kronxy.Application\Apartments\SearchApartments\SearchApartmentsQuery.cs
[FILE] src\Kronxy.Application\Apartments\SearchApartments\SearchApartmentsQueryHandler.cs
[DIR]  src\Kronxy.Application\Bookings\CancelBooking
[DIR]  src\Kronxy.Application\Bookings\CompleteBooking
[DIR]  src\Kronxy.Application\Bookings\ConfirmBooking
[DIR]  src\Kronxy.Application\Bookings\GetBooking
[DIR]  src\Kronxy.Application\Bookings\RejectBooking
[DIR]  src\Kronxy.Application\Bookings\ReserveBooking
[FILE] src\Kronxy.Application\Bookings\CancelBooking\CancelBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\CancelBooking\CancelBookingCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\CompleteBooking\CompleteBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\CompleteBooking\CompleteBookingCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\ConfirmBooking\ConfirmBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\ConfirmBooking\ConfirmBookingCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\GetBooking\BookingResponse.cs
[FILE] src\Kronxy.Application\Bookings\GetBooking\GetBookingQuery.cs
[FILE] src\Kronxy.Application\Bookings\GetBooking\GetBookingQueryHandler.cs
[FILE] src\Kronxy.Application\Bookings\RejectBooking\RejectBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\RejectBooking\RejectBookingCommandCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\ReserveBooking\BookingReservedDomainEventHandler.cs
[FILE] src\Kronxy.Application\Bookings\ReserveBooking\ReserveBookingCommand.cs
[FILE] src\Kronxy.Application\Bookings\ReserveBooking\ReserveBookingCommandHandler.cs
[FILE] src\Kronxy.Application\Bookings\ReserveBooking\ReserveBookingCommandValidator.cs
[FILE] src\Kronxy.Application\Exceptions\ConcurrencyException.cs
[FILE] src\Kronxy.Application\Exceptions\ValidationError.cs
[FILE] src\Kronxy.Application\Exceptions\ValidationException.cs
[DIR]  src\Kronxy.Application\Projects\ActivateProject
[DIR]  src\Kronxy.Application\Projects\CreateProject
[DIR]  src\Kronxy.Application\Projects\DeactivateProject
[DIR]  src\Kronxy.Application\Projects\GetProject
[DIR]  src\Kronxy.Application\Projects\UpdateProject
[FILE] src\Kronxy.Application\Projects\ActivateProject\ActivateProjectCommand.cs
[FILE] src\Kronxy.Application\Projects\ActivateProject\ActivateProjectCommandHandler.cs
[FILE] src\Kronxy.Application\Projects\CreateProject\CreateProjectCommand.cs
[FILE] src\Kronxy.Application\Projects\CreateProject\CreateProjectCommandHandler.cs
[FILE] src\Kronxy.Application\Projects\DeactivateProject\DeactivateProjectCommand.cs
[FILE] src\Kronxy.Application\Projects\DeactivateProject\DeactivateProjectCommandHandler.cs
[FILE] src\Kronxy.Application\Projects\GetProject\GetProjectQuery.cs
[FILE] src\Kronxy.Application\Projects\GetProject\GetProjectQueryHandler.cs
[FILE] src\Kronxy.Application\Projects\GetProject\GetProjectsQuery.cs
[FILE] src\Kronxy.Application\Projects\GetProject\GetProjectsQueryHandler.cs
[FILE] src\Kronxy.Application\Projects\GetProject\ProjectResponse.cs
[FILE] src\Kronxy.Application\Projects\UpdateProject\UpdateProjectCommand.cs
[FILE] src\Kronxy.Application\Projects\UpdateProject\UpdateProjectCommandHandler.cs
[DIR]  src\Kronxy.Application\ProjectTasks\ActivateProjectTask
[DIR]  src\Kronxy.Application\ProjectTasks\ChangeProjectTaskStatus
[DIR]  src\Kronxy.Application\ProjectTasks\CreateProjectTask
[DIR]  src\Kronxy.Application\ProjectTasks\DeactivateProjectTask
[DIR]  src\Kronxy.Application\ProjectTasks\GetProjectTask
[DIR]  src\Kronxy.Application\ProjectTasks\UpdateProjectTask
[FILE] src\Kronxy.Application\ProjectTasks\ActivateProjectTask\ActivateProjectTaskCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\ActivateProjectTask\ActivateProjectTaskCommandHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\ChangeProjectTaskStatus\ChangeProjectTaskStatusCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\ChangeProjectTaskStatus\ChangeProjectTaskStatusCommandHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\CreateProjectTask\CreateProjectTaskCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\CreateProjectTask\CreateProjectTaskCommandHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\DeactivateProjectTask\DeactivateProjectTaskCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\DeactivateProjectTask\DeactivateProjectTaskCommandHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTaskQuery.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTaskQueryHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByProjectQuery.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByProjectQueryHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByUserQuery.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByUserQueryHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksQuery.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksQueryHandler.cs
[FILE] src\Kronxy.Application\ProjectTasks\GetProjectTask\ProjectTaskResponse.cs
[FILE] src\Kronxy.Application\ProjectTasks\UpdateProjectTask\UpdateProjectTaskCommand.cs
[FILE] src\Kronxy.Application\ProjectTasks\UpdateProjectTask\UpdateProjectTaskCommandHandler.cs
[DIR]  src\Kronxy.Application\Users\ActivateUser
[DIR]  src\Kronxy.Application\Users\CreateUser
[DIR]  src\Kronxy.Application\Users\DeactivateUser
[DIR]  src\Kronxy.Application\Users\GetUser
[DIR]  src\Kronxy.Application\Users\UpdateUser
[FILE] src\Kronxy.Application\Users\ActivateUser\ActivateUserCommand.cs
[FILE] src\Kronxy.Application\Users\ActivateUser\ActivateUserCommandHandler.cs
[FILE] src\Kronxy.Application\Users\CreateUser\CreateUserCommand.cs
[FILE] src\Kronxy.Application\Users\CreateUser\CreateUserCommandHandler.cs
[FILE] src\Kronxy.Application\Users\DeactivateUser\DeactivateUserCommand.cs
[FILE] src\Kronxy.Application\Users\DeactivateUser\DeactivateUserCommandHandler.cs
[FILE] src\Kronxy.Application\Users\GetUser\GetUserQuery.cs
[FILE] src\Kronxy.Application\Users\GetUser\GetUserQueryHandler.cs
[FILE] src\Kronxy.Application\Users\GetUser\GetUsersQuery.cs
[FILE] src\Kronxy.Application\Users\GetUser\GetUsersQueryHandler.cs
[FILE] src\Kronxy.Application\Users\GetUser\UserResponse.cs
[FILE] src\Kronxy.Application\Users\UpdateUser\UpdateUserCommand.cs
[FILE] src\Kronxy.Application\Users\UpdateUser\UpdateUserCommandHandler.cs
[FILE] src\Kronxy.Application\Users\UpdateUser\UpdateUserCommandValidator.cs
[DIR]  src\Kronxy.Domain\Abstractions
[DIR]  src\Kronxy.Domain\Apartments
[DIR]  src\Kronxy.Domain\Bookings
[DIR]  src\Kronxy.Domain\Projects
[DIR]  src\Kronxy.Domain\ProjectTasks
[DIR]  src\Kronxy.Domain\Reviews
[DIR]  src\Kronxy.Domain\Shared
[DIR]  src\Kronxy.Domain\Users
[FILE] src\Kronxy.Domain\Kronxy.Domain.csproj
[FILE] src\Kronxy.Domain\Abstractions\Entity.cs
[FILE] src\Kronxy.Domain\Abstractions\Error.cs
[FILE] src\Kronxy.Domain\Abstractions\IDomainEvent.cs
[FILE] src\Kronxy.Domain\Abstractions\IUnitOfWork.cs
[FILE] src\Kronxy.Domain\Abstractions\Result.cs
[FILE] src\Kronxy.Domain\Apartments\Address.cs
[FILE] src\Kronxy.Domain\Apartments\Amenity.cs
[FILE] src\Kronxy.Domain\Apartments\Apartment.cs
[FILE] src\Kronxy.Domain\Apartments\ApartmentErrors.cs
[FILE] src\Kronxy.Domain\Apartments\Description.cs
[FILE] src\Kronxy.Domain\Apartments\IApartmentRepository.cs
[FILE] src\Kronxy.Domain\Apartments\Name.cs
[DIR]  src\Kronxy.Domain\Bookings\Events
[FILE] src\Kronxy.Domain\Bookings\Booking.cs
[FILE] src\Kronxy.Domain\Bookings\BookingErrors.cs
[FILE] src\Kronxy.Domain\Bookings\BookingStatus.cs
[FILE] src\Kronxy.Domain\Bookings\DateRange.cs
[FILE] src\Kronxy.Domain\Bookings\IBookingRepository.cs
[FILE] src\Kronxy.Domain\Bookings\PricingDetails.cs
[FILE] src\Kronxy.Domain\Bookings\PricingService.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingCancelledDomainEvent.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingCompletedDomainEvent.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingConfirmedDomainEvent.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingRejectedDomainEvent.cs
[FILE] src\Kronxy.Domain\Bookings\Events\BookingReservedDomainEvent.cs
[FILE] src\Kronxy.Domain\Projects\IProjectRepository.cs
[FILE] src\Kronxy.Domain\Projects\Project.cs
[FILE] src\Kronxy.Domain\Projects\ProjectErrors.cs
[FILE] src\Kronxy.Domain\Projects\ProjectPriority.cs
[FILE] src\Kronxy.Domain\Projects\ProjectStatus.cs
[FILE] src\Kronxy.Domain\ProjectTasks\IProjectTaskRepository.cs
[FILE] src\Kronxy.Domain\ProjectTasks\ProjectTask.cs
[FILE] src\Kronxy.Domain\ProjectTasks\ProjectTaskErrors.cs
[FILE] src\Kronxy.Domain\ProjectTasks\ProjectTaskPriority.cs
[FILE] src\Kronxy.Domain\ProjectTasks\ProjectTaskStatus.cs
[DIR]  src\Kronxy.Domain\Reviews\Events
[FILE] src\Kronxy.Domain\Reviews\Comment.cs
[FILE] src\Kronxy.Domain\Reviews\Rating.cs
[FILE] src\Kronxy.Domain\Reviews\Review.cs
[FILE] src\Kronxy.Domain\Reviews\ReviewErrors.cs
[FILE] src\Kronxy.Domain\Reviews\Events\ReviewCreatedDomainEvent.cs
[FILE] src\Kronxy.Domain\Shared\Currency.cs
[FILE] src\Kronxy.Domain\Shared\Money.cs
[DIR]  src\Kronxy.Domain\Users\Events
[FILE] src\Kronxy.Domain\Users\Email.cs
[FILE] src\Kronxy.Domain\Users\FirstName.cs
[FILE] src\Kronxy.Domain\Users\IUserRepository.cs
[FILE] src\Kronxy.Domain\Users\LastName.cs
[FILE] src\Kronxy.Domain\Users\PhoneNumber.cs
[FILE] src\Kronxy.Domain\Users\User.cs
[FILE] src\Kronxy.Domain\Users\UserErrors.cs
[FILE] src\Kronxy.Domain\Users\Username.cs
[FILE] src\Kronxy.Domain\Users\Events\UserCreatedDomainEvent.cs
[DIR]  src\Kronxy.Infrastructure\Clock
[DIR]  src\Kronxy.Infrastructure\Configurations
[DIR]  src\Kronxy.Infrastructure\Data
[DIR]  src\Kronxy.Infrastructure\Email
[DIR]  src\Kronxy.Infrastructure\Migrations
[DIR]  src\Kronxy.Infrastructure\Repositories
[FILE] src\Kronxy.Infrastructure\ApplicationDbContext.cs
[FILE] src\Kronxy.Infrastructure\DependencyInjection.cs
[FILE] src\Kronxy.Infrastructure\Kronxy.Infrastructure.csproj
[FILE] src\Kronxy.Infrastructure\Clock\DateTimeProvider.cs
[FILE] src\Kronxy.Infrastructure\Configurations\ApartmentConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\BookingConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\ProjectConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\ProjectTaskConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\ReviewConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Configurations\UserConfiguration.cs
[FILE] src\Kronxy.Infrastructure\Data\DateOnlyTypeHandler.cs
[FILE] src\Kronxy.Infrastructure\Data\SqlConnectionFactory.cs
[FILE] src\Kronxy.Infrastructure\Email\EmailService.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20240104150149_Create_Database.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20240104150149_Create_Database.Designer.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260428145405_Add_User_Management_Fields.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260428145405_Add_User_Management_Fields.Designer.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260603180007_Add_Projects.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260603180007_Add_Projects.Designer.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260603182025_Add_ProjectTasks.cs
[FILE] src\Kronxy.Infrastructure\Migrations\20260603182025_Add_ProjectTasks.Designer.cs
[FILE] src\Kronxy.Infrastructure\Migrations\ApplicationDbContextModelSnapshot.cs
[FILE] src\Kronxy.Infrastructure\Repositories\ApartmentRepository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\BookingRepository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\ProjectRepository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\ProjectTaskRepository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\Repository.cs
[FILE] src\Kronxy.Infrastructure\Repositories\UserRepository.cs

## FILE CONTENTS


---

### FILE: docker-compose.override.yml

~~~text
version: '3.4'

services:
  bookify.api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_HTTP_PORTS=5000
      - ASPNETCORE_HTTPS_PORTS=5001
    ports:
      - 5000:5000
      - 5001:5001
    volumes:
      - ${APPDATA}/Microsoft/UserSecrets:/root/.microsoft/usersecrets:ro
      - ${APPDATA}/ASP.NET/Https:/root/.aspnet/https:ro
~~~

---

### FILE: docker-compose.yml

~~~text
version: '3.4'

services:
  bookify.api:
    image: ${DOCKER_REGISTRY-}bookifyapi
    container_name: Bookify.Api
    build:
      context: .
      dockerfile: src/Bookify.Api/Dockerfile
    depends_on:
      - bookify-db

  bookify-db:
    image: postgres:latest
    container_name: Bookify.Db
    environment:
      - POSTGRES_DB=bookify
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
    volumes:
      - ./.containers/database:/var/lib/postgresql/data
    ports:
      - 5432:5432

~~~

---

### FILE: export_context.ps1

~~~text
$Root = "E:\dev\kronxy-lab"
$Out  = Join-Path $Root "PROJECT_CONTEXT.md"

$ExcludeDirs = @(".git",".vs",".idea","bin","obj","node_modules","dist","build","target",".gradle",".mvn","__pycache__")
$IncludeExt = @(".cs",".csproj",".sln",".json",".xml",".config",".yml",".yaml",".md",".txt",".ps1",".bat",".cmd",".sql",".js",".ts",".html",".css",".java",".properties")

function IsExcluded($Path) {
    foreach ($d in $ExcludeDirs) {
        if ($Path -like "*\$d\*" -or $Path -like "*\$d") {
            return $true
        }
    }
    return $false
}

Set-Content -Path $Out -Value "# PROJECT CONTEXT - kronxy-lab" -Encoding UTF8
Add-Content -Path $Out -Value ""
Add-Content -Path $Out -Value "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Content -Path $Out -Value "Root: $Root"
Add-Content -Path $Out -Value ""
Add-Content -Path $Out -Value "## TREE"
Add-Content -Path $Out -Value ""

Get-ChildItem $Root -Recurse -Force |
    Where-Object { -not (IsExcluded $_.FullName) } |
    ForEach-Object {
        $rel = $_.FullName.Replace($Root, "").TrimStart("\")
        if ($_.PSIsContainer) {
            Add-Content -Path $Out -Value "[DIR]  $rel"
        } else {
            Add-Content -Path $Out -Value "[FILE] $rel"
        }
    }

Add-Content -Path $Out -Value ""
Add-Content -Path $Out -Value "## FILE CONTENTS"
Add-Content -Path $Out -Value ""

Get-ChildItem $Root -Recurse -File -Force |
    Where-Object {
        -not (IsExcluded $_.FullName) -and
        $IncludeExt -contains $_.Extension.ToLower()
    } |
    Sort-Object FullName |
    ForEach-Object {
        $rel = $_.FullName.Replace($Root, "").TrimStart("\")

        Add-Content -Path $Out -Value ""
        Add-Content -Path $Out -Value "---"
        Add-Content -Path $Out -Value ""
        Add-Content -Path $Out -Value "### FILE: $rel"
        Add-Content -Path $Out -Value ""
        Add-Content -Path $Out -Value "~~~text"

        try {
            $content = Get-Content $_.FullName -Raw -ErrorAction Stop
            Add-Content -Path $Out -Value $content
        } catch {
            Add-Content -Path $Out -Value "[ERROR leyendo archivo]"
        }

        Add-Content -Path $Out -Value "~~~"
    }

Write-Host ("OK generado: " + $Out)
~~~

---

### FILE: global.json

~~~text
{
  "sdk": {
    "version": "8.0.418"
  }
}

~~~

---

### FILE: Kronxy.sln

~~~text

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Kronxy.Domain", "src\Kronxy.Domain\Kronxy.Domain.csproj", "{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Kronxy.Application", "src\Kronxy.Application\Kronxy.Application.csproj", "{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Kronxy.Infrastructure", "src\Kronxy.Infrastructure\Kronxy.Infrastructure.csproj", "{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Kronxy.Api", "src\Kronxy.Api\Kronxy.Api.csproj", "{79E174F1-8A7B-4067-9671-31D3E330A2F0}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE}.Release|Any CPU.Build.0 = Release|Any CPU
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3}.Release|Any CPU.Build.0 = Release|Any CPU
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4}.Release|Any CPU.Build.0 = Release|Any CPU
		{79E174F1-8A7B-4067-9671-31D3E330A2F0}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{79E174F1-8A7B-4067-9671-31D3E330A2F0}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{79E174F1-8A7B-4067-9671-31D3E330A2F0}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{79E174F1-8A7B-4067-9671-31D3E330A2F0}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{D44DC3AE-D9A0-45C7-980D-45B8081A0BDE} = {D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}
		{9A9BC2ED-7D77-4EDC-B282-6DA1A8DF33D3} = {D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}
		{05C4E6AA-F7B1-493A-87AA-ADE51677AFA4} = {D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}
		{79E174F1-8A7B-4067-9671-31D3E330A2F0} = {D1DE7A82-FA55-49D0-B3DC-6AD9503FE472}
	EndGlobalSection
EndGlobal

~~~

---

### FILE: kronxy_api_full.txt

~~~text

### FILE: src/Kronxy.Api/Controllers/Apartments/ApartmentsController.cs ###

using Kronxy.Application.Apartments.SearchApartments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Apartments;

[ApiController]
[Route("api/apartments")]
public class ApartmentsController : ControllerBase
{
    private readonly ISender _sender;

    public ApartmentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> SearchApartments(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var query = new SearchApartmentsQuery(startDate, endDate);

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Controllers/Bookings/BookingsController.cs ###

using Kronxy.Application.Bookings.GetBooking;
using Kronxy.Application.Bookings.ReserveBooking;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Bookings;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly ISender _sender;

    public BookingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetBookingQuery(id);

        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> ReserveBooking(
        ReserveBookingRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReserveBookingCommand(
            request.ApartmentId,
            request.UserId,
            request.StartDate,
            request.EndDate);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetBooking), new { id = result.Value }, result.Value);
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Controllers/Users/UsersController.cs ###

using Kronxy.Application.Users.CreateUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Users;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            request.Username,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(CreateUser), new { id = result.Value }, result.Value);
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Program.cs ###

using Kronxy.Api.Extensions;
using Kronxy.Application;
using Kronxy.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger (opcional, no afecta endpoints)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Kronxy API",
        Version = "v1"
    });
});

// Clean Architecture layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Swagger (opcional)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kronxy API v1");
});

// Solo DEV: migraciones/seed
if (app.Environment.IsDevelopment())
{
    app.ApplyMigrations();
    // app.SeedData();
}

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCustomExceptionHandler();

app.MapControllers();

app.Run();

### END FILE ###


### FILE: src/Kronxy.Api/Extensions/ApplicationBuilderExtensions.cs ###

using Kronxy.Api.Middleware;
using Kronxy.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Database.Migrate();
    }

    public static void UseCustomExceptionHandler(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Extensions/SeedDataExtensions.cs ###

using Bogus;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Domain.Apartments;
using Dapper;

namespace Kronxy.Api.Extensions;

public static class SeedDataExtensions
{
    public static void SeedData(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        var sqlConnectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        using var connection = sqlConnectionFactory.CreateConnection();

        var faker = new Faker();

        List<object> apartments = new();
        for (var i = 0; i < 100; i++)
        {
            apartments.Add(new
            {
                Id = Guid.NewGuid(),
                Name = faker.Company.CompanyName(),
                Description = "Amazing view",
                Country = faker.Address.Country(),
                State = faker.Address.State(),
                ZipCode = faker.Address.ZipCode(),
                City = faker.Address.City(),
                Street = faker.Address.StreetAddress(),
                PriceAmount = faker.Random.Decimal(50, 1000),
                PriceCurrency = "USD",
                CleaningFeeAmount = faker.Random.Decimal(25, 200),
                CleaningFeeCurrency = "USD",
                Amenities = new List<int> { (int)Amenity.Parking, (int)Amenity.MountainView },
                LastBookedOn = DateTime.MinValue
            });
        }

        const string sql = """
            INSERT INTO public.apartments
            (id, "name", description, address_country, address_state, address_zip_code, address_city, address_street, price_amount, price_currency, cleaning_fee_amount, cleaning_fee_currency, amenities, last_booked_on_utc)
            VALUES(@Id, @Name, @Description, @Country, @State, @ZipCode, @City, @Street, @PriceAmount, @PriceCurrency, @CleaningFeeAmount, @CleaningFeeCurrency, @Amenities, @LastBookedOn);
            """;

        connection.Execute(sql, apartments);
    }
}

### END FILE ###


### FILE: src/Kronxy.Api/Middleware/ExceptionHandlingMiddleware.cs ###

using Kronxy.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

            var exceptionDetails = GetExceptionDetails(exception);

            var problemDetails = new ProblemDetails
            {
                Status = exceptionDetails.Status,
                Type = exceptionDetails.Type,
                Title = exceptionDetails.Title,
                Detail = exceptionDetails.Detail,
            };

            if (exceptionDetails.Errors is not null)
            {
                problemDetails.Extensions["errors"] = exceptionDetails.Errors;
            }

            context.Response.StatusCode = exceptionDetails.Status;

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }

    private static ExceptionDetails GetExceptionDetails(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => new ExceptionDetails(
                StatusCodes.Status400BadRequest,
                "ValidationFailure",
                "Validation error",
                "One or more validation errors has occurred",
                validationException.Errors),
            _ => new ExceptionDetails(
                StatusCodes.Status500InternalServerError,
                "ServerError",
                "Server error",
                "An unexpected error has occurred",
                null)
        };
    }

    internal record ExceptionDetails(
        int Status,
        string Type,
        string Title,
        string Detail,
        IEnumerable<object>? Errors);
}

### END FILE ###


~~~

---

### FILE: launchSettings.json

~~~text
{
  "profiles": {
    "Docker Compose": {
      "commandName": "DockerCompose",
      "commandVersion": "1.0",
      "serviceActions": {
        "bookify.api": "StartDebugging"
      }
    }
  }
}
~~~

---

### FILE: PROJECT_CONTEXT.md

~~~text

~~~

---

### FILE: src\Kronxy.Api\appsettings.Development.json

~~~text
{
  "ConnectionStrings": {
    "Database": "Host=192.168.100.17;Port=5432;Database=kronxy;Username=app;Password=app"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}

~~~

---

### FILE: src\Kronxy.Api\appsettings.json

~~~text
{
  "ConnectionStrings": {
    "Database": "Host=192.168.100.17;Port=5432;Database=kronxy;Username=app;Password=app"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}


~~~

---

### FILE: src\Kronxy.Api\Controllers\Apartments\ApartmentsController.cs

~~~text
using Kronxy.Application.Apartments.SearchApartments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Apartments;

[ApiController]
[Route("api/apartments")]
public class ApartmentsController : ControllerBase
{
    private readonly ISender _sender;

    public ApartmentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> SearchApartments(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var query = new SearchApartmentsQuery(startDate, endDate);

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }
}

~~~

---

### FILE: src\Kronxy.Api\Controllers\Bookings\BookingsController.cs

~~~text
using Kronxy.Application.Bookings.GetBooking;
using Kronxy.Application.Bookings.ReserveBooking;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Bookings;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly ISender _sender;

    public BookingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetBookingQuery(id);

        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> ReserveBooking(
        ReserveBookingRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReserveBookingCommand(
            request.ApartmentId,
            request.UserId,
            request.StartDate,
            request.EndDate);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetBooking), new { id = result.Value }, result.Value);
    }
}

~~~

---

### FILE: src\Kronxy.Api\Controllers\Bookings\ReserveBookingRequest.cs

~~~text
namespace Kronxy.Api.Controllers.Bookings;

public sealed record ReserveBookingRequest(
    Guid ApartmentId,
    Guid UserId,
    DateOnly StartDate,
    DateOnly EndDate);
~~~

---

### FILE: src\Kronxy.Api\Controllers\Projects\CreateProjectRequest.cs

~~~text
namespace Kronxy.Api.Controllers.Projects;
public sealed record CreateProjectRequest(
    string Code,
    string Name,
    string? Description,
    Guid OwnerId);

~~~

---

### FILE: src\Kronxy.Api\Controllers\Projects\ProjectsController.cs

~~~text
using Kronxy.Application.Projects.ActivateProject;
using Kronxy.Application.Projects.CreateProject;
using Kronxy.Application.Projects.DeactivateProject;
using Kronxy.Application.Projects.GetProject;
using Kronxy.Application.Projects.UpdateProject;
using MediatR;
using Microsoft.AspNetCore.Mvc;
namespace Kronxy.Api.Controllers.Projects;
[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ISender _sender;
    public ProjectsController(ISender sender)
    {
        _sender = sender;
    }
    [HttpPost]
    public async Task<IActionResult> CreateProject(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProjectCommand(
            request.Code,
            request.Name,
            request.Description,
            request.OwnerId);
        var result = await _sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return CreatedAtAction(nameof(GetProject), new { id = result.Value }, result.Value);
    }
    [HttpGet]
    public async Task<IActionResult> GetProjects(
        CancellationToken cancellationToken)
    {
        var query = new GetProjectsQuery();
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result.Value);
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetProjectQuery(id);
        var result = await _sender.Send(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProjectCommand(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.OwnerId);
        var result = await _sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return NoContent();
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateProject(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateProjectCommand(id);
        var result = await _sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return NoContent();
    }
    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateProject(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ActivateProjectCommand(id);
        var result = await _sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return NoContent();
    }
}

~~~

---

### FILE: src\Kronxy.Api\Controllers\Projects\UpdateProjectRequest.cs

~~~text
namespace Kronxy.Api.Controllers.Projects;
public sealed record UpdateProjectRequest(
    string Code,
    string Name,
    string? Description,
    Guid OwnerId);

~~~

---

### FILE: src\Kronxy.Api\Controllers\ProjectTasks\ChangeProjectTaskStatusRequest.cs

~~~text
using Kronxy.Domain.ProjectTasks;

namespace Kronxy.Api.Controllers.ProjectTasks;

public sealed record ChangeProjectTaskStatusRequest(
    ProjectTaskStatus Status);
~~~

---

### FILE: src\Kronxy.Api\Controllers\ProjectTasks\CreateProjectTaskRequest.cs

~~~text
namespace Kronxy.Api.Controllers.ProjectTasks;

public sealed record CreateProjectTaskRequest(
    Guid ProjectId,
    Guid AssignedUserId,
    string Title,
    string? Description);
~~~

---

### FILE: src\Kronxy.Api\Controllers\ProjectTasks\ProjectTasksController.cs

~~~text
using Kronxy.Application.ProjectTasks.ActivateProjectTask;
using Kronxy.Application.ProjectTasks.ChangeProjectTaskStatus;
using Kronxy.Application.ProjectTasks.CreateProjectTask;
using Kronxy.Application.ProjectTasks.DeactivateProjectTask;
using Kronxy.Application.ProjectTasks.GetProjectTask;
using Kronxy.Application.ProjectTasks.UpdateProjectTask;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.ProjectTasks;

[ApiController]
[Route("api/tasks")]
public class ProjectTasksController : ControllerBase
{
    private readonly ISender _sender;

    public ProjectTasksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(
        CreateProjectTaskRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProjectTaskCommand(
            request.ProjectId,
            request.AssignedUserId,
            request.Title,
            request.Description);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetProjectTasksQuery(),
            cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTask(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetProjectTaskQuery(id),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(
        Guid id,
        UpdateProjectTaskRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProjectTaskCommand(
            id,
            request.AssignedUserId,
            request.Title,
            request.Description);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateTask(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new DeactivateProjectTaskCommand(id),
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateTask(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ActivateProjectTaskCommand(id),
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        ChangeProjectTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ChangeProjectTaskStatusCommand(
                id,
                request.Status),
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }
}
~~~

---

### FILE: src\Kronxy.Api\Controllers\ProjectTasks\UpdateProjectTaskRequest.cs

~~~text
namespace Kronxy.Api.Controllers.ProjectTasks;

public sealed record UpdateProjectTaskRequest(
    Guid AssignedUserId,
    string Title,
    string? Description);
~~~

---

### FILE: src\Kronxy.Api\Controllers\Users\CreateUserRequest.cs

~~~text
namespace Kronxy.Api.Controllers.Users;

public sealed record CreateUserRequest(
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber);
~~~

---

### FILE: src\Kronxy.Api\Controllers\Users\UpdateUserRequest.cs

~~~text
namespace Kronxy.Api.Controllers.Users;

public sealed record UpdateUserRequest(
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber);
~~~

---

### FILE: src\Kronxy.Api\Controllers\Users\UsersController.cs

~~~text
using Kronxy.Application.Users.CreateUser;
using Kronxy.Application.Users.DeactivateUser;
using Kronxy.Application.Users.GetUser;
using Kronxy.Application.Users.GetUsers;
using Kronxy.Application.Users.UpdateUser;
using Kronxy.Application.Users.ActivateUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Users;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            request.Username,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(Guid id,CancellationToken cancellationToken)
    {
        var query = new GetUserQuery(id);

        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var query = new GetUsersQuery();

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(
    Guid id,
    UpdateUserRequest request,
    CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(
            id,
            request.Username,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateUser(
    Guid id,
    CancellationToken cancellationToken)
    {
        var command = new DeactivateUserCommand(id);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(result.Error);
        }

        return NoContent();
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateUser(
    Guid id,
    CancellationToken cancellationToken)
    {
        var command = new ActivateUserCommand(id);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

}
~~~

---

### FILE: src\Kronxy.Api\Extensions\ApplicationBuilderExtensions.cs

~~~text
using Kronxy.Api.Middleware;
using Kronxy.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Database.Migrate();
    }

    public static void UseCustomExceptionHandler(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}

~~~

---

### FILE: src\Kronxy.Api\Extensions\SeedDataExtensions.cs

~~~text
using Bogus;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Domain.Apartments;
using Dapper;

namespace Kronxy.Api.Extensions;

public static class SeedDataExtensions
{
    public static void SeedData(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        var sqlConnectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        using var connection = sqlConnectionFactory.CreateConnection();

        var faker = new Faker();

        List<object> apartments = new();
        for (var i = 0; i < 100; i++)
        {
            apartments.Add(new
            {
                Id = Guid.NewGuid(),
                Name = faker.Company.CompanyName(),
                Description = "Amazing view",
                Country = faker.Address.Country(),
                State = faker.Address.State(),
                ZipCode = faker.Address.ZipCode(),
                City = faker.Address.City(),
                Street = faker.Address.StreetAddress(),
                PriceAmount = faker.Random.Decimal(50, 1000),
                PriceCurrency = "USD",
                CleaningFeeAmount = faker.Random.Decimal(25, 200),
                CleaningFeeCurrency = "USD",
                Amenities = new List<int> { (int)Amenity.Parking, (int)Amenity.MountainView },
                LastBookedOn = DateTime.MinValue
            });
        }

        const string sql = """
            INSERT INTO public.apartments
            (id, "name", description, address_country, address_state, address_zip_code, address_city, address_street, price_amount, price_currency, cleaning_fee_amount, cleaning_fee_currency, amenities, last_booked_on_utc)
            VALUES(@Id, @Name, @Description, @Country, @State, @ZipCode, @City, @Street, @PriceAmount, @PriceCurrency, @CleaningFeeAmount, @CleaningFeeCurrency, @Amenities, @LastBookedOn);
            """;

        connection.Execute(sql, apartments);
    }
}

~~~

---

### FILE: src\Kronxy.Api\Kronxy.Api.csproj

~~~text
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>d7b01bfb-1058-41e5-a81b-cb54458b5a3c</UserSecretsId>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
    <DockerfileContext>..\..</DockerfileContext>
    <DockerComposeProjectPath>..\..\docker-compose.dcproj</DockerComposeProjectPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Bogus" Version="35.4.0" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.VisualStudio.Azure.Containers.Tools.Targets" Version="1.19.6" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Kronxy.Application\Kronxy.Application.csproj" />
    <ProjectReference Include="..\Kronxy.Infrastructure\Kronxy.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Folder Include="Controllers\NewFolder\" />
  </ItemGroup>

</Project>

~~~

---

### FILE: src\Kronxy.Api\Middleware\ExceptionHandlingMiddleware.cs

~~~text
using Kronxy.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

            var exceptionDetails = GetExceptionDetails(exception);

            var problemDetails = new ProblemDetails
            {
                Status = exceptionDetails.Status,
                Type = exceptionDetails.Type,
                Title = exceptionDetails.Title,
                Detail = exceptionDetails.Detail,
            };

            if (exceptionDetails.Errors is not null)
            {
                problemDetails.Extensions["errors"] = exceptionDetails.Errors;
            }

            context.Response.StatusCode = exceptionDetails.Status;

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }

    private static ExceptionDetails GetExceptionDetails(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => new ExceptionDetails(
                StatusCodes.Status400BadRequest,
                "ValidationFailure",
                "Validation error",
                "One or more validation errors has occurred",
                validationException.Errors),
            _ => new ExceptionDetails(
                StatusCodes.Status500InternalServerError,
                "ServerError",
                "Server error",
                "An unexpected error has occurred",
                null)
        };
    }

    internal record ExceptionDetails(
        int Status,
        string Type,
        string Title,
        string Detail,
        IEnumerable<object>? Errors);
}
~~~

---

### FILE: src\Kronxy.Api\Program.cs

~~~text
using Kronxy.Api.Extensions;
using Kronxy.Application;
using Kronxy.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger (opcional, no afecta endpoints)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Kronxy API",
        Version = "v1"
    });
});

// Clean Architecture layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Swagger (opcional)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kronxy API v1");
});

// Solo DEV: migraciones/seed
if (app.Environment.IsDevelopment())
{
    app.ApplyMigrations();
    // app.SeedData();
}

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCustomExceptionHandler();

app.MapControllers();

app.Run();

~~~

---

### FILE: src\Kronxy.Api\Properties\launchSettings.json

~~~text
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": true,
      "launchUrl": "swagger",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "dotnetRunMessages": true,
      "applicationUrl": "http://localhost:5000"
    },
    "https": {
      "commandName": "Project",
      "launchBrowser": true,
      "launchUrl": "swagger",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "dotnetRunMessages": true,
      "applicationUrl": "https://localhost:5001;http://localhost:5000"
    },
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
      "launchUrl": "swagger",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "Docker": {
      "commandName": "Docker",
      "launchBrowser": true,
      "launchUrl": "{Scheme}://{ServiceHost}:{ServicePort}/swagger",
      "publishAllPorts": true,
      "useSSL": true
    }
  },
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:5000",
      "sslPort": 5001
    }
  }
}
~~~

---

### FILE: src\Kronxy.Application\Abstractions\Behaviors\LoggingBehavior.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kronxy.Application.Abstractions.Behaviors;

public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    private readonly ILogger<TRequest> _logger;

    public LoggingBehavior(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = request.GetType().Name;

        try
        {
            _logger.LogInformation("Executing command {Command}", name);

            var result = await next();

            _logger.LogInformation("Command {Command} processed successfully", name);

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Command {Command} processing failed", name);

            throw;
        }
    }
}
~~~

---

### FILE: src\Kronxy.Application\Abstractions\Behaviors\ValidationBehavior.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Application.Exceptions;
using FluentValidation;
using MediatR;

namespace Kronxy.Application.Abstractions.Behaviors;

public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationErrors = _validators
            .Select(validator => validator.Validate(context))
            .Where(validationResult => validationResult.Errors.Any())
            .SelectMany(validationResult => validationResult.Errors)
            .Select(validationFailure => new ValidationError(
                validationFailure.PropertyName,
                validationFailure.ErrorMessage))
            .ToList();

        if (validationErrors.Any())
        {
            throw new Exceptions.ValidationException(validationErrors);
        }

        return await next();
    }
}
~~~

---

### FILE: src\Kronxy.Application\Abstractions\Clock\IDateTimeProvider.cs

~~~text
namespace Kronxy.Application.Abstractions.Clock;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
~~~

---

### FILE: src\Kronxy.Application\Abstractions\Data\ISqlConnectionFactory.cs

~~~text
using System.Data;

namespace Kronxy.Application.Abstractions.Data;

public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}
~~~

---

### FILE: src\Kronxy.Application\Abstractions\Email\IEmailService.cs

~~~text
namespace Kronxy.Application.Abstractions.Email;

public interface IEmailService
{
    Task SendAsync(Domain.Users.Email recipient, string subject, string body);
}
~~~

---

### FILE: src\Kronxy.Application\Abstractions\Messaging\ICommand.cs

~~~text
using Kronxy.Domain.Abstractions;
using MediatR;

namespace Kronxy.Application.Abstractions.Messaging;

public interface ICommand : IRequest<Result>, IBaseCommand
{
}

public interface ICommand<TReponse> : IRequest<Result<TReponse>>, IBaseCommand
{
}

public interface IBaseCommand
{
}
~~~

---

### FILE: src\Kronxy.Application\Abstractions\Messaging\ICommandHandler.cs

~~~text
using Kronxy.Domain.Abstractions;
using MediatR;

namespace Kronxy.Application.Abstractions.Messaging;

public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand
{
}

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{
}
~~~

---

### FILE: src\Kronxy.Application\Abstractions\Messaging\IQuery.cs

~~~text
using Kronxy.Domain.Abstractions;
using MediatR;

namespace Kronxy.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
~~~

---

### FILE: src\Kronxy.Application\Abstractions\Messaging\IQueryHandler.cs

~~~text
using Kronxy.Domain.Abstractions;
using MediatR;

namespace Kronxy.Application.Abstractions.Messaging;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
~~~

---

### FILE: src\Kronxy.Application\Apartments\SearchApartments\AddressResponse.cs

~~~text
namespace Kronxy.Application.Apartments.SearchApartments;

public sealed class AddressResponse
{
    public string Country { get; init; }

    public string State { get; init; }

    public string ZipCode { get; init; }

    public string City { get; init; }

    public string Street { get; init; }
}
~~~

---

### FILE: src\Kronxy.Application\Apartments\SearchApartments\ApartmentResponse.cs

~~~text
namespace Kronxy.Application.Apartments.SearchApartments;

public sealed class ApartmentResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string Description { get; init; }

    public decimal Price { get; init; }

    public string Currency { get; init; }

    public AddressResponse Address { get; set; }
}
~~~

---

### FILE: src\Kronxy.Application\Apartments\SearchApartments\SearchApartmentsQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Apartments.SearchApartments;

public sealed record SearchApartmentsQuery(
    DateOnly StartDate,
    DateOnly EndDate) : IQuery<IReadOnlyList<ApartmentResponse>>;
~~~

---

### FILE: src\Kronxy.Application\Apartments\SearchApartments\SearchApartmentsQueryHandler.cs

~~~text
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Bookings;
using Dapper;

namespace Kronxy.Application.Apartments.SearchApartments;

internal sealed class SearchApartmentsQueryHandler
    : IQueryHandler<SearchApartmentsQuery, IReadOnlyList<ApartmentResponse>>
{
    private static readonly int[] ActiveBookingStatuses =
    {
        (int)BookingStatus.Reserved,
        (int)BookingStatus.Confirmed,
        (int)BookingStatus.Completed
    };

    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public SearchApartmentsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<IReadOnlyList<ApartmentResponse>>> Handle(SearchApartmentsQuery request, CancellationToken cancellationToken)
    {
        if (request.StartDate > request.EndDate)
        {
            return new List<ApartmentResponse>();
        }

        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                a.id AS Id,
                a.name AS Name,
                a.description AS Description,
                a.price_amount AS Price,
                a.price_currency AS Currency,
                a.address_country AS Country,
                a.address_state AS State,
                a.address_zip_code AS ZipCode,
                a.address_city AS City,
                a.address_street AS Street
            FROM apartments AS a
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM bookings AS b
                WHERE
                    b.apartment_id = a.id AND
                    b.duration_start <= @EndDate AND
                    b.duration_end >= @StartDate AND
                    b.status = ANY(@ActiveBookingStatuses)
            )
            """;

        var apartments = await connection
            .QueryAsync<ApartmentResponse, AddressResponse, ApartmentResponse>(
                sql,
                (apartment, address) =>
                {
                    apartment.Address = address;

                    return apartment;
                },
                new
                {
                    request.StartDate,
                    request.EndDate,
                    ActiveBookingStatuses
                },
                splitOn: "Country");

        return apartments.ToList();
    }
}
~~~

---

### FILE: src\Kronxy.Application\Bookings\CancelBooking\CancelBookingCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.CancelBooking;

public record CancelBookingCommand(Guid BookingId) : ICommand;
~~~

---

### FILE: src\Kronxy.Application\Bookings\CancelBooking\CancelBookingCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Bookings;

namespace Kronxy.Application.Bookings.CancelBooking;

internal sealed class CancelBookingCommandHandler : ICommandHandler<CancelBookingCommand>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelBookingCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _dateTimeProvider = dateTimeProvider;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        CancelBookingCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        var result = booking.Cancel(_dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
~~~

---

### FILE: src\Kronxy.Application\Bookings\CompleteBooking\CompleteBookingCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.CompleteBooking;

public record CompleteBookingCommand(Guid BookingId) : ICommand;
~~~

---

### FILE: src\Kronxy.Application\Bookings\CompleteBooking\CompleteBookingCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Bookings;

namespace Kronxy.Application.Bookings.CompleteBooking;

internal sealed class CompleteBookingCommandHandler : ICommandHandler<CompleteBookingCommand>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteBookingCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _dateTimeProvider = dateTimeProvider;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CompleteBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        var result = booking.Complete(_dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
~~~

---

### FILE: src\Kronxy.Application\Bookings\ConfirmBooking\ConfirmBookingCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.ConfirmBooking;

public sealed record ConfirmBookingCommand(Guid BookingId) : ICommand;
~~~

---

### FILE: src\Kronxy.Application\Bookings\ConfirmBooking\ConfirmBookingCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Bookings;

namespace Kronxy.Application.Bookings.ConfirmBooking;

internal sealed class ConfirmBookingCommandHandler : ICommandHandler<ConfirmBookingCommand>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmBookingCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _dateTimeProvider = dateTimeProvider;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ConfirmBookingCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        var result = booking.Confirm(_dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
~~~

---

### FILE: src\Kronxy.Application\Bookings\GetBooking\BookingResponse.cs

~~~text
namespace Kronxy.Application.Bookings.GetBooking;

public sealed class BookingResponse
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid ApartmentId { get; init; }
    
    public int Status { get; init; }

    public decimal PriceAmount { get; init; }

    public string PriceCurrency { get; init; }

    public decimal CleaningFeeAmount { get; init; }

    public string CleaningFeeCurrency { get; init; }

    public decimal AmenitiesUpChargeAmount { get; init; }

    public string AmenitiesUpChargeCurrency { get; init; }

    public decimal TotalPriceAmount { get; init; }

    public string TotalPriceCurrency { get; init; }

    public DateOnly DurationStart { get; init; }

    public DateOnly DurationEnd { get; init; }

    public DateTime CreatedOnUtc { get; init; }
}
~~~

---

### FILE: src\Kronxy.Application\Bookings\GetBooking\GetBookingQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.GetBooking;

public sealed record GetBookingQuery(Guid BookingId) : IQuery<BookingResponse>;
~~~

---

### FILE: src\Kronxy.Application\Bookings\GetBooking\GetBookingQueryHandler.cs

~~~text
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Dapper;

namespace Kronxy.Application.Bookings.GetBooking;

internal sealed class GetBookingQueryHandler : IQueryHandler<GetBookingQuery, BookingResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetBookingQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<BookingResponse>> Handle(GetBookingQuery request, CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                id AS Id,
                apartment_id AS ApartmentId,
                user_id AS UserId,
                status AS Status,
                price_for_period_amount AS PriceAmount,
                price_for_period_currency AS PriceCurrency,
                cleaning_fee_amount AS CleaningFeeAmount,
                cleaning_fee_currency AS CleaningFeeCurrency,
                amenities_up_charge_amount AS AmenitiesUpChargeAmount,
                amenities_up_charge_currency AS AmenitiesUpChargeCurrency,
                total_price_amount AS TotalPriceAmount,
                total_price_currency AS TotalPriceCurrency,
                duration_start AS DurationStart,
                duration_end AS DurationEnd,
                created_on_utc AS CreatedOnUtc
            FROM bookings
            WHERE id = @BookingId
            """;

        var booking = await connection.QueryFirstOrDefaultAsync<BookingResponse>(
            sql,
            new
            {
                request.BookingId
            });

        return booking;
    }
}
~~~

---

### FILE: src\Kronxy.Application\Bookings\RejectBooking\RejectBookingCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.RejectBooking;

public sealed record RejectBookingCommand(Guid BookingId) : ICommand;
~~~

---

### FILE: src\Kronxy.Application\Bookings\RejectBooking\RejectBookingCommandCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Bookings;

namespace Kronxy.Application.Bookings.RejectBooking;

internal sealed class RejectBookingCommandCommandHandler : ICommandHandler<RejectBookingCommand>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectBookingCommandCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        RejectBookingCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        var result = booking.Reject(_dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
~~~

---

### FILE: src\Kronxy.Application\Bookings\ReserveBooking\BookingReservedDomainEventHandler.cs

~~~text
using Kronxy.Application.Abstractions.Email;
using Kronxy.Domain.Bookings;
using Kronxy.Domain.Bookings.Events;
using Kronxy.Domain.Users;
using MediatR;

namespace Kronxy.Application.Bookings.ReserveBooking;

internal sealed class BookingReservedDomainEventHandler : INotificationHandler<BookingReservedDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;

    public BookingReservedDomainEventHandler(
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IEmailService emailService)
    {
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _emailService = emailService;
    }

    public async Task Handle(BookingReservedDomainEvent notification, CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);

        if (booking is null)
        {
            return;
        }

        var user = await _userRepository.GetByIdAsync(booking.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        await _emailService.SendAsync(
            user.Email,
            "Booking reserved!",
            "You have 10 minutes to confirm this booking");
    }
}
~~~

---

### FILE: src\Kronxy.Application\Bookings\ReserveBooking\ReserveBookingCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Bookings.ReserveBooking;

public record ReserveBookingCommand(
    Guid ApartmentId,
    Guid UserId,
    DateOnly StartDate,
    DateOnly EndDate) : ICommand<Guid>;
~~~

---

### FILE: src\Kronxy.Application\Bookings\ReserveBooking\ReserveBookingCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Application.Exceptions;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Bookings;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Bookings.ReserveBooking;

internal sealed class ReserveBookingCommandHandler : ICommandHandler<ReserveBookingCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PricingService _pricingService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReserveBookingCommandHandler(
        IUserRepository userRepository,
        IApartmentRepository apartmentRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        PricingService pricingService,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _apartmentRepository = apartmentRepository;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _pricingService = pricingService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(ReserveBookingCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<Guid>(UserErrors.NotFound);
        }

        var apartment = await _apartmentRepository.GetByIdAsync(request.ApartmentId, cancellationToken);

        if (apartment is null)
        {
            return Result.Failure<Guid>(ApartmentErrors.NotFound);
        }

        var duration = DateRange.Create(request.StartDate, request.EndDate);

        if (await _bookingRepository.IsOverlappingAsync(apartment, duration, cancellationToken))
        {
            return Result.Failure<Guid>(BookingErrors.Overlap);
        }

        try
        {
            var booking = Booking.Reserve(
                apartment,
                user.Id,
                duration,
                _dateTimeProvider.UtcNow,
                _pricingService);

            _bookingRepository.Add(booking);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return booking.Id;
        }
        catch (ConcurrencyException)
        {
            return Result.Failure<Guid>(BookingErrors.Overlap);
        }
    }
}
~~~

---

### FILE: src\Kronxy.Application\Bookings\ReserveBooking\ReserveBookingCommandValidator.cs

~~~text
using FluentValidation;

namespace Kronxy.Application.Bookings.ReserveBooking;

public class ReserveBookingCommandValidator : AbstractValidator<ReserveBookingCommand>
{
    public ReserveBookingCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();

        RuleFor(c => c.ApartmentId).NotEmpty();

        RuleFor(c => c.StartDate).LessThan(c => c.EndDate);
    }
}
~~~

---

### FILE: src\Kronxy.Application\DependencyInjection.cs

~~~text
using Kronxy.Application.Abstractions.Behaviors;
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

        return services;
    }
}
~~~

---

### FILE: src\Kronxy.Application\Exceptions\ConcurrencyException.cs

~~~text
namespace Kronxy.Application.Exceptions;

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
~~~

---

### FILE: src\Kronxy.Application\Exceptions\ValidationError.cs

~~~text
namespace Kronxy.Application.Exceptions;

public sealed record ValidationError(string PropertyName, string ErrorMessage);
~~~

---

### FILE: src\Kronxy.Application\Exceptions\ValidationException.cs

~~~text
namespace Kronxy.Application.Exceptions;

public sealed class ValidationException : Exception
{
    public ValidationException(IEnumerable<ValidationError> errors)
    {
        Errors = errors;
    }

    public IEnumerable<ValidationError> Errors { get; }
}
~~~

---

### FILE: src\Kronxy.Application\Kronxy.Application.csproj

~~~text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Dapper" Version="2.1.28" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Kronxy.Domain\Kronxy.Domain.csproj" />
  </ItemGroup>

</Project>

~~~

---

### FILE: src\Kronxy.Application\Projects\ActivateProject\ActivateProjectCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.ActivateProject;
public sealed record ActivateProjectCommand(Guid ProjectId) : ICommand;

~~~

---

### FILE: src\Kronxy.Application\Projects\ActivateProject\ActivateProjectCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Projects;
namespace Kronxy.Application.Projects.ActivateProject;
internal sealed class ActivateProjectCommandHandler
    : ICommandHandler<ActivateProjectCommand>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public ActivateProjectCommandHandler(
        IProjectRepository projectRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectRepository = projectRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        ActivateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(
            request.ProjectId,
            cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }
        project.Activate(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

~~~

---

### FILE: src\Kronxy.Application\Projects\CreateProject\CreateProjectCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.CreateProject;
public sealed record CreateProjectCommand(
    string Code,
    string Name,
    string? Description,
    Guid OwnerId) : ICommand<Guid>;

~~~

---

### FILE: src\Kronxy.Application\Projects\CreateProject\CreateProjectCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Projects;
using Kronxy.Domain.Users;
namespace Kronxy.Application.Projects.CreateProject;
internal sealed class CreateProjectCommandHandler
    : ICommandHandler<CreateProjectCommand, Guid>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public CreateProjectCommandHandler(
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result<Guid>> Handle(
        CreateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var owner = await _userRepository.GetByIdAsync(
            request.OwnerId,
            cancellationToken);
        if (owner is null || !owner.IsActive)
        {
            return Result.Failure<Guid>(UserErrors.NotFound);
        }
        var existingProject = await _projectRepository.GetByCodeAsync(
            request.Code,
            cancellationToken);
        if (existingProject is not null)
        {
            return Result.Failure<Guid>(ProjectErrors.CodeAlreadyInUse);
        }
        var project = Project.Create(
            request.Code,
            request.Name,
            request.Description,
            request.OwnerId,
            _dateTimeProvider.UtcNow);
        _projectRepository.Add(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return project.Id;
    }
}

~~~

---

### FILE: src\Kronxy.Application\Projects\DeactivateProject\DeactivateProjectCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.DeactivateProject;
public sealed record DeactivateProjectCommand(Guid ProjectId) : ICommand;

~~~

---

### FILE: src\Kronxy.Application\Projects\DeactivateProject\DeactivateProjectCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Projects;
namespace Kronxy.Application.Projects.DeactivateProject;
internal sealed class DeactivateProjectCommandHandler
    : ICommandHandler<DeactivateProjectCommand>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public DeactivateProjectCommandHandler(
        IProjectRepository projectRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectRepository = projectRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        DeactivateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(
            request.ProjectId,
            cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }
        project.Deactivate(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

~~~

---

### FILE: src\Kronxy.Application\Projects\GetProject\GetProjectQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.GetProject;
public sealed record GetProjectQuery(Guid ProjectId) : IQuery<ProjectResponse>;

~~~

---

### FILE: src\Kronxy.Application\Projects\GetProject\GetProjectQueryHandler.cs

~~~text
using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
namespace Kronxy.Application.Projects.GetProject;
internal sealed class GetProjectQueryHandler
    : IQueryHandler<GetProjectQuery, ProjectResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<ProjectResponse>> Handle(
        GetProjectQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT
                p.id AS Id,
                p.code AS Code,
                p.name AS Name,
                p.description AS Description,
                p.owner_id AS OwnerId,
                u.username AS OwnerUsername,
                CONCAT(u.first_name, ' ', u.last_name) AS OwnerFullName,
                p.status AS Status,
                p.priority AS Priority,
                p.start_date AS StartDate,
                p.end_date AS EndDate,
                p.is_active AS IsActive,
                p.created_on_utc AS CreatedOnUtc,
                p.updated_on_utc AS UpdatedOnUtc,
                p.deleted_on_utc AS DeletedOnUtc
            FROM projects p
            INNER JOIN users u ON u.id = p.owner_id
            WHERE p.id = @ProjectId
            """;
        var project = await connection.QueryFirstOrDefaultAsync<ProjectResponse>(
            sql,
            new
            {
                request.ProjectId
            });
        return project;
    }
}

~~~

---

### FILE: src\Kronxy.Application\Projects\GetProject\GetProjectsQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.GetProject;
public sealed record GetProjectsQuery
    : IQuery<IReadOnlyList<ProjectResponse>>;

~~~

---

### FILE: src\Kronxy.Application\Projects\GetProject\GetProjectsQueryHandler.cs

~~~text
using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
namespace Kronxy.Application.Projects.GetProject;
internal sealed class GetProjectsQueryHandler
    : IQueryHandler<GetProjectsQuery, IReadOnlyList<ProjectResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<IReadOnlyList<ProjectResponse>>> Handle(
        GetProjectsQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT
                p.id AS Id,
                p.code AS Code,
                p.name AS Name,
                p.description AS Description,
                p.owner_id AS OwnerId,
                u.username AS OwnerUsername,
                CONCAT(u.first_name, ' ', u.last_name) AS OwnerFullName,
                p.status AS Status,
                p.priority AS Priority,
                p.start_date AS StartDate,
                p.end_date AS EndDate,
                p.is_active AS IsActive,
                p.created_on_utc AS CreatedOnUtc,
                p.updated_on_utc AS UpdatedOnUtc,
                p.deleted_on_utc AS DeletedOnUtc
            FROM projects p
            INNER JOIN users u ON u.id = p.owner_id
            WHERE p.is_active = TRUE
            ORDER BY p.created_on_utc DESC
            """;
        var projects = await connection.QueryAsync<ProjectResponse>(sql);
        return projects.ToList();
    }
}

~~~

---

### FILE: src\Kronxy.Application\Projects\GetProject\ProjectResponse.cs

~~~text
namespace Kronxy.Application.Projects.GetProject;
public sealed class ProjectResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid OwnerId { get; init; }
    public string OwnerUsername { get; init; } = string.Empty;
    public string OwnerFullName { get; init; } = string.Empty;
    public int Status { get; init; }
    public int Priority { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedOnUtc { get; init; }
    public DateTime? UpdatedOnUtc { get; init; }
    public DateTime? DeletedOnUtc { get; init; }
}

~~~

---

### FILE: src\Kronxy.Application\Projects\UpdateProject\UpdateProjectCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.Projects.UpdateProject;
public sealed record UpdateProjectCommand(
    Guid ProjectId,
    string Code,
    string Name,
    string? Description,
    Guid OwnerId) : ICommand;

~~~

---

### FILE: src\Kronxy.Application\Projects\UpdateProject\UpdateProjectCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Projects;
using Kronxy.Domain.Users;
namespace Kronxy.Application.Projects.UpdateProject;
internal sealed class UpdateProjectCommandHandler
    : ICommandHandler<UpdateProjectCommand>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public UpdateProjectCommandHandler(
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        UpdateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(
            request.ProjectId,
            cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }
        var owner = await _userRepository.GetByIdAsync(
            request.OwnerId,
            cancellationToken);
        if (owner is null || !owner.IsActive)
        {
            return Result.Failure(UserErrors.NotFound);
        }
        var existingProject = await _projectRepository.GetByCodeAsync(
            request.Code,
            cancellationToken);
        if (existingProject is not null &&
            existingProject.Id != project.Id)
        {
            return Result.Failure(ProjectErrors.CodeAlreadyInUse);
        }
        project.Update(
            request.Code,
            request.Name,
            request.Description,
            request.OwnerId,
            _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\ActivateProjectTask\ActivateProjectTaskCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.ActivateProjectTask;
public sealed record ActivateProjectTaskCommand(Guid ProjectTaskId) : ICommand;

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\ActivateProjectTask\ActivateProjectTaskCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.ProjectTasks;
namespace Kronxy.Application.ProjectTasks.ActivateProjectTask;
internal sealed class ActivateProjectTaskCommandHandler
    : ICommandHandler<ActivateProjectTaskCommand>
{
    private readonly IProjectTaskRepository _projectTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public ActivateProjectTaskCommandHandler(
        IProjectTaskRepository projectTaskRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectTaskRepository = projectTaskRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        ActivateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var projectTask = await _projectTaskRepository.GetByIdAsync(
            request.ProjectTaskId,
            cancellationToken);
        if (projectTask is null)
        {
            return Result.Failure(ProjectTaskErrors.NotFound);
        }
        projectTask.Activate(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\ChangeProjectTaskStatus\ChangeProjectTaskStatusCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.ProjectTasks;
namespace Kronxy.Application.ProjectTasks.ChangeProjectTaskStatus;
public sealed record ChangeProjectTaskStatusCommand(
    Guid ProjectTaskId,
    ProjectTaskStatus Status) : ICommand;

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\ChangeProjectTaskStatus\ChangeProjectTaskStatusCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.ProjectTasks;
namespace Kronxy.Application.ProjectTasks.ChangeProjectTaskStatus;
internal sealed class ChangeProjectTaskStatusCommandHandler
    : ICommandHandler<ChangeProjectTaskStatusCommand>
{
    private readonly IProjectTaskRepository _projectTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public ChangeProjectTaskStatusCommandHandler(
        IProjectTaskRepository projectTaskRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectTaskRepository = projectTaskRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        ChangeProjectTaskStatusCommand request,
        CancellationToken cancellationToken)
    {
        var projectTask = await _projectTaskRepository.GetByIdAsync(
            request.ProjectTaskId,
            cancellationToken);
        if (projectTask is null)
        {
            return Result.Failure(ProjectTaskErrors.NotFound);
        }
        projectTask.ChangeStatus(
            request.Status,
            _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\CreateProjectTask\CreateProjectTaskCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.CreateProjectTask;
public sealed record CreateProjectTaskCommand(
    Guid ProjectId,
    Guid AssignedUserId,
    string Title,
    string? Description) : ICommand<Guid>;

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\CreateProjectTask\CreateProjectTaskCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Projects;
using Kronxy.Domain.ProjectTasks;
using Kronxy.Domain.Users;
namespace Kronxy.Application.ProjectTasks.CreateProjectTask;
internal sealed class CreateProjectTaskCommandHandler
    : ICommandHandler<CreateProjectTaskCommand, Guid>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectTaskRepository _projectTaskRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public CreateProjectTaskCommandHandler(
        IProjectRepository projectRepository,
        IProjectTaskRepository projectTaskRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectRepository = projectRepository;
        _projectTaskRepository = projectTaskRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result<Guid>> Handle(
        CreateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null || !project.IsActive)
        {
            return Result.Failure<Guid>(ProjectErrors.NotFound);
        }
        var user = await _userRepository.GetByIdAsync(request.AssignedUserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<Guid>(UserErrors.NotFound);
        }
        var projectTask = ProjectTask.Create(
            request.ProjectId,
            request.AssignedUserId,
            request.Title,
            request.Description,
            _dateTimeProvider.UtcNow);
        _projectTaskRepository.Add(projectTask);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return projectTask.Id;
    }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\DeactivateProjectTask\DeactivateProjectTaskCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.DeactivateProjectTask;
public sealed record DeactivateProjectTaskCommand(Guid ProjectTaskId) : ICommand;

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\DeactivateProjectTask\DeactivateProjectTaskCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.ProjectTasks;
namespace Kronxy.Application.ProjectTasks.DeactivateProjectTask;
internal sealed class DeactivateProjectTaskCommandHandler
    : ICommandHandler<DeactivateProjectTaskCommand>
{
    private readonly IProjectTaskRepository _projectTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public DeactivateProjectTaskCommandHandler(
        IProjectTaskRepository projectTaskRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectTaskRepository = projectTaskRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        DeactivateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var projectTask = await _projectTaskRepository.GetByIdAsync(
            request.ProjectTaskId,
            cancellationToken);
        if (projectTask is null)
        {
            return Result.Failure(ProjectTaskErrors.NotFound);
        }
        projectTask.Deactivate(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTaskQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed record GetProjectTaskQuery(Guid ProjectTaskId) : IQuery<ProjectTaskResponse>;

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTaskQueryHandler.cs

~~~text
using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
internal sealed class GetProjectTaskQueryHandler
    : IQueryHandler<GetProjectTaskQuery, ProjectTaskResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectTaskQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<ProjectTaskResponse>> Handle(
        GetProjectTaskQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT
                pt.id AS Id,
                pt.project_id AS ProjectId,
                p.code AS ProjectCode,
                p.name AS ProjectName,
                pt.assigned_user_id AS AssignedUserId,
                u.username AS AssignedUsername,
                CONCAT(u.first_name, ' ', u.last_name) AS AssignedFullName,
                pt.title AS Title,
                pt.description AS Description,
                pt.status AS Status,
                pt.priority AS Priority,
                pt.due_date AS DueDate,
                pt.estimated_hours AS EstimatedHours,
                pt.worked_hours AS WorkedHours,
                pt.is_active AS IsActive,
                pt.created_on_utc AS CreatedOnUtc,
                pt.updated_on_utc AS UpdatedOnUtc,
                pt.deleted_on_utc AS DeletedOnUtc
            FROM project_tasks pt
            INNER JOIN projects p ON p.id = pt.project_id
            INNER JOIN users u ON u.id = pt.assigned_user_id
            WHERE pt.id = @ProjectTaskId
            """;
        var projectTask = await connection.QueryFirstOrDefaultAsync<ProjectTaskResponse>(
            sql,
            new { request.ProjectTaskId });
        return projectTask;
    }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByProjectQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed record GetProjectTasksByProjectQuery(Guid ProjectId)
    : IQuery<IReadOnlyList<ProjectTaskResponse>>;

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByProjectQueryHandler.cs

~~~text
using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
internal sealed class GetProjectTasksByProjectQueryHandler
    : IQueryHandler<GetProjectTasksByProjectQuery, IReadOnlyList<ProjectTaskResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectTasksByProjectQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<IReadOnlyList<ProjectTaskResponse>>> Handle(
        GetProjectTasksByProjectQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT
                pt.id AS Id,
                pt.project_id AS ProjectId,
                p.code AS ProjectCode,
                p.name AS ProjectName,
                pt.assigned_user_id AS AssignedUserId,
                u.username AS AssignedUsername,
                CONCAT(u.first_name, ' ', u.last_name) AS AssignedFullName,
                pt.title AS Title,
                pt.description AS Description,
                pt.status AS Status,
                pt.priority AS Priority,
                pt.due_date AS DueDate,
                pt.estimated_hours AS EstimatedHours,
                pt.worked_hours AS WorkedHours,
                pt.is_active AS IsActive,
                pt.created_on_utc AS CreatedOnUtc,
                pt.updated_on_utc AS UpdatedOnUtc,
                pt.deleted_on_utc AS DeletedOnUtc
            FROM project_tasks pt
            INNER JOIN projects p ON p.id = pt.project_id
            INNER JOIN users u ON u.id = pt.assigned_user_id
            WHERE pt.project_id = @ProjectId
              AND pt.is_active = TRUE
            ORDER BY pt.created_on_utc DESC
            """;
        var projectTasks = await connection.QueryAsync<ProjectTaskResponse>(
            sql,
            new { request.ProjectId });
        return projectTasks.ToList();
    }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByUserQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed record GetProjectTasksByUserQuery(Guid UserId)
    : IQuery<IReadOnlyList<ProjectTaskResponse>>;

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksByUserQueryHandler.cs

~~~text
using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
internal sealed class GetProjectTasksByUserQueryHandler
    : IQueryHandler<GetProjectTasksByUserQuery, IReadOnlyList<ProjectTaskResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectTasksByUserQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<IReadOnlyList<ProjectTaskResponse>>> Handle(
        GetProjectTasksByUserQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT
                pt.id AS Id,
                pt.project_id AS ProjectId,
                p.code AS ProjectCode,
                p.name AS ProjectName,
                pt.assigned_user_id AS AssignedUserId,
                u.username AS AssignedUsername,
                CONCAT(u.first_name, ' ', u.last_name) AS AssignedFullName,
                pt.title AS Title,
                pt.description AS Description,
                pt.status AS Status,
                pt.priority AS Priority,
                pt.due_date AS DueDate,
                pt.estimated_hours AS EstimatedHours,
                pt.worked_hours AS WorkedHours,
                pt.is_active AS IsActive,
                pt.created_on_utc AS CreatedOnUtc,
                pt.updated_on_utc AS UpdatedOnUtc,
                pt.deleted_on_utc AS DeletedOnUtc
            FROM project_tasks pt
            INNER JOIN projects p ON p.id = pt.project_id
            INNER JOIN users u ON u.id = pt.assigned_user_id
            WHERE pt.assigned_user_id = @UserId
              AND pt.is_active = TRUE
            ORDER BY pt.created_on_utc DESC
            """;
        var projectTasks = await connection.QueryAsync<ProjectTaskResponse>(
            sql,
            new { request.UserId });
        return projectTasks.ToList();
    }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed record GetProjectTasksQuery : IQuery<IReadOnlyList<ProjectTaskResponse>>;

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\GetProjectTask\GetProjectTasksQueryHandler.cs

~~~text
using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
internal sealed class GetProjectTasksQueryHandler
    : IQueryHandler<GetProjectTasksQuery, IReadOnlyList<ProjectTaskResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    public GetProjectTasksQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }
    public async Task<Result<IReadOnlyList<ProjectTaskResponse>>> Handle(
        GetProjectTasksQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT
                pt.id AS Id,
                pt.project_id AS ProjectId,
                p.code AS ProjectCode,
                p.name AS ProjectName,
                pt.assigned_user_id AS AssignedUserId,
                u.username AS AssignedUsername,
                CONCAT(u.first_name, ' ', u.last_name) AS AssignedFullName,
                pt.title AS Title,
                pt.description AS Description,
                pt.status AS Status,
                pt.priority AS Priority,
                pt.due_date AS DueDate,
                pt.estimated_hours AS EstimatedHours,
                pt.worked_hours AS WorkedHours,
                pt.is_active AS IsActive,
                pt.created_on_utc AS CreatedOnUtc,
                pt.updated_on_utc AS UpdatedOnUtc,
                pt.deleted_on_utc AS DeletedOnUtc
            FROM project_tasks pt
            INNER JOIN projects p ON p.id = pt.project_id
            INNER JOIN users u ON u.id = pt.assigned_user_id
            WHERE pt.is_active = TRUE
            ORDER BY pt.created_on_utc DESC
            """;
        var projectTasks = await connection.QueryAsync<ProjectTaskResponse>(sql);
        return projectTasks.ToList();
    }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\GetProjectTask\ProjectTaskResponse.cs

~~~text
namespace Kronxy.Application.ProjectTasks.GetProjectTask;
public sealed class ProjectTaskResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public Guid AssignedUserId { get; init; }
    public string AssignedUsername { get; init; } = string.Empty;
    public string AssignedFullName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Status { get; init; }
    public int Priority { get; init; }
    public DateOnly? DueDate { get; init; }
    public decimal? EstimatedHours { get; init; }
    public decimal? WorkedHours { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedOnUtc { get; init; }
    public DateTime? UpdatedOnUtc { get; init; }
    public DateTime? DeletedOnUtc { get; init; }
}

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\UpdateProjectTask\UpdateProjectTaskCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
namespace Kronxy.Application.ProjectTasks.UpdateProjectTask;
public sealed record UpdateProjectTaskCommand(
    Guid ProjectTaskId,
    Guid AssignedUserId,
    string Title,
    string? Description) : ICommand;

~~~

---

### FILE: src\Kronxy.Application\ProjectTasks\UpdateProjectTask\UpdateProjectTaskCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.ProjectTasks;
using Kronxy.Domain.Users;
namespace Kronxy.Application.ProjectTasks.UpdateProjectTask;
internal sealed class UpdateProjectTaskCommandHandler
    : ICommandHandler<UpdateProjectTaskCommand>
{
    private readonly IProjectTaskRepository _projectTaskRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public UpdateProjectTaskCommandHandler(
        IProjectTaskRepository projectTaskRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectTaskRepository = projectTaskRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        UpdateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var projectTask = await _projectTaskRepository.GetByIdAsync(
            request.ProjectTaskId,
            cancellationToken);
        if (projectTask is null)
        {
            return Result.Failure(ProjectTaskErrors.NotFound);
        }
        var user = await _userRepository.GetByIdAsync(
            request.AssignedUserId,
            cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure(UserErrors.NotFound);
        }
        projectTask.Update(
            request.AssignedUserId,
            request.Title,
            request.Description,
            _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

~~~

---

### FILE: src\Kronxy.Application\Users\ActivateUser\ActivateUserCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.ActivateUser;

public sealed record ActivateUserCommand(Guid UserId) : ICommand;

~~~

---

### FILE: src\Kronxy.Application\Users\ActivateUser\ActivateUserCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Users.ActivateUser;

internal sealed class ActivateUserCommandHandler : ICommandHandler<ActivateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ActivateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        ActivateUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        user.Activate(_dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

~~~

---

### FILE: src\Kronxy.Application\Users\CreateUser\CreateUserCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber
) : ICommand<Guid>;
~~~

---

### FILE: src\Kronxy.Application\Users\CreateUser\CreateUserCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Users.CreateUser;

internal sealed class CreateUserCommandHandler
    : ICommandHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var email = new Email(request.Email);
        var username = new Username(request.Username);

        var existingEmail = await _userRepository
            .GetByEmailAsync(email, cancellationToken);

        if (existingEmail is not null)
        {
            return Result.Failure<Guid>(UserErrors.EmailAlreadyExists);
        }

        var existingUsername = await _userRepository
            .GetByUsernameAsync(username, cancellationToken);

        if (existingUsername is not null)
        {
            return Result.Failure<Guid>(UserErrors.UsernameAlreadyExists);
        }

        var user = User.Create(
            username,
            new FirstName(request.FirstName),
            new LastName(request.LastName),
            email,
            string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : new PhoneNumber(request.PhoneNumber),
            _dateTimeProvider.UtcNow);

        _userRepository.Add(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}

~~~

---

### FILE: src\Kronxy.Application\Users\DeactivateUser\DeactivateUserCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.DeactivateUser;

public sealed record DeactivateUserCommand(Guid UserId) : ICommand;
~~~

---

### FILE: src\Kronxy.Application\Users\DeactivateUser\DeactivateUserCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Users.DeactivateUser;

internal sealed class DeactivateUserCommandHandler
    : ICommandHandler<DeactivateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeactivateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        DeactivateUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(
            request.UserId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        user.Deactivate(_dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
~~~

---

### FILE: src\Kronxy.Application\Users\GetUser\GetUserQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.GetUser;

public sealed record GetUserQuery(Guid UserId) : IQuery<UserResponse>;
~~~

---

### FILE: src\Kronxy.Application\Users\GetUser\GetUserQueryHandler.cs

~~~text
using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;

namespace Kronxy.Application.Users.GetUser;

internal sealed class GetUserQueryHandler : IQueryHandler<GetUserQuery, UserResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetUserQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<UserResponse>> Handle(
        GetUserQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                id AS Id,
                username AS Username,
                first_name AS FirstName,
                last_name AS LastName,
                email AS Email,
                phone_number AS PhoneNumber,
                is_active AS IsActive,
                created_on_utc AS CreatedOnUtc,
                updated_on_utc AS UpdatedOnUtc,
                deleted_on_utc AS DeletedOnUtc
            FROM users
            WHERE id = @UserId
            """;

        var user = await connection.QueryFirstOrDefaultAsync<UserResponse>(
            sql,
            new
            {
                request.UserId
            });

        return user;
    }
}
~~~

---

### FILE: src\Kronxy.Application\Users\GetUser\GetUsersQuery.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Application.Users.GetUser;

namespace Kronxy.Application.Users.GetUsers;

public sealed record GetUsersQuery
    : IQuery<IReadOnlyList<UserResponse>>;
~~~

---

### FILE: src\Kronxy.Application\Users\GetUser\GetUsersQueryHandler.cs

~~~text
using Dapper;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Application.Users.GetUser;
using Kronxy.Domain.Abstractions;

namespace Kronxy.Application.Users.GetUsers;

internal sealed class GetUsersQueryHandler
    : IQueryHandler<GetUsersQuery, IReadOnlyList<UserResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetUsersQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<IReadOnlyList<UserResponse>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                id AS Id,
                username AS Username,
                first_name AS FirstName,
                last_name AS LastName,
                email AS Email,
                phone_number AS PhoneNumber,
                is_active AS IsActive,
                created_on_utc AS CreatedOnUtc,
                updated_on_utc AS UpdatedOnUtc,
                deleted_on_utc AS DeletedOnUtc
            FROM users
            WHERE is_active = TRUE
            ORDER BY created_on_utc DESC
            """;

        var users = await connection.QueryAsync<UserResponse>(sql);

        return users.ToList();
    }
}
~~~

---

### FILE: src\Kronxy.Application\Users\GetUser\UserResponse.cs

~~~text
namespace Kronxy.Application.Users.GetUser;

public sealed class UserResponse
{
    public Guid Id { get; init; }
    public string Username { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public string Email { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedOnUtc { get; init; }
    public DateTime? UpdatedOnUtc { get; init; }
    public DateTime? DeletedOnUtc { get; init; }
}
~~~

---

### FILE: src\Kronxy.Application\Users\UpdateUser\UpdateUserCommand.cs

~~~text
using Kronxy.Application.Abstractions.Messaging;

namespace Kronxy.Application.Users.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber) : ICommand;
~~~

---

### FILE: src\Kronxy.Application\Users\UpdateUser\UpdateUserCommandHandler.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Users.UpdateUser;

internal sealed class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(
            request.UserId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        var email = new Email(request.Email);
        var username = new Username(request.Username);

        var existingUserByEmail = await _userRepository.GetByEmailAsync(
            email,
            cancellationToken);

        if (existingUserByEmail is not null &&
            existingUserByEmail.Id != user.Id)
        {
            return Result.Failure(UserErrors.EmailAlreadyExists);
        }

        var existingUserByUsername = await _userRepository.GetByUsernameAsync(
            username,
            cancellationToken);

        if (existingUserByUsername is not null &&
            existingUserByUsername.Id != user.Id)
        {
            return Result.Failure(UserErrors.UsernameAlreadyExists);
        }

        user.Update(
            username,
            new FirstName(request.FirstName),
            new LastName(request.LastName),
            email,
            string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : new PhoneNumber(request.PhoneNumber),
            _dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
~~~

---

### FILE: src\Kronxy.Application\Users\UpdateUser\UpdateUserCommandValidator.cs

~~~text
using FluentValidation;

namespace Kronxy.Application.Users.UpdateUser;

internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(400);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30);
    }
}
~~~

---

### FILE: src\Kronxy.Domain\Abstractions\Entity.cs

~~~text
namespace Kronxy.Domain.Abstractions;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    protected Entity(Guid id)
    {
        Id = id;
    }

    protected Entity()
    {
    }

    public Guid Id { get; init; }

    public IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        return _domainEvents.ToList();
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
~~~

---

### FILE: src\Kronxy.Domain\Abstractions\Error.cs

~~~text
namespace Kronxy.Domain.Abstractions;

public record Error(string Code, string Name)
{
    public static Error None = new(string.Empty, string.Empty);

    public static Error NullValue = new("Error.NullValue", "Null value was provided");
}
~~~

---

### FILE: src\Kronxy.Domain\Abstractions\IDomainEvent.cs

~~~text
using MediatR;

namespace Kronxy.Domain.Abstractions;

public interface IDomainEvent : INotification
{
}
~~~

---

### FILE: src\Kronxy.Domain\Abstractions\IUnitOfWork.cs

~~~text
namespace Kronxy.Domain.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
~~~

---

### FILE: src\Kronxy.Domain\Abstractions\Result.cs

~~~text
using System.Diagnostics.CodeAnalysis;

namespace Kronxy.Domain.Abstractions;

public class Result
{
    protected internal Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException();
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException();
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    public static Result<TValue> Create<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.NullValue);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result can not be accessed.");

    public static implicit operator Result<TValue>(TValue? value) => Create(value);
}
~~~

---

### FILE: src\Kronxy.Domain\Apartments\Address.cs

~~~text
namespace Kronxy.Domain.Apartments;

public record Address(
    string Country,
    string State,
    string ZipCode,
    string City,
    string Street);
~~~

---

### FILE: src\Kronxy.Domain\Apartments\Amenity.cs

~~~text
namespace Kronxy.Domain.Apartments;

public enum Amenity
{
    WiFi = 1,
    AirConditioning = 2,
    Parking = 3,
    PetFriendly = 4,
    SwimmingPool = 5,
    Gym = 6,
    Spa = 7,
    Terrace = 8,
    MountainView = 9,
    GardenView = 10
}
~~~

---

### FILE: src\Kronxy.Domain\Apartments\Apartment.cs

~~~text
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Shared;

namespace Kronxy.Domain.Apartments;

public sealed class Apartment : Entity
{
    public Apartment(
        Guid id,
        Name name,
        Description description,
        Address address,
        Money price,
        Money cleaningFee,
        List<Amenity> amenities)
        : base(id)
    {
        Name = name;
        Description = description;
        Address = address;
        Price = price;
        CleaningFee = cleaningFee;
        Amenities = amenities;
    }

    private Apartment()
    {
    }

    public Name Name { get; private set; }

    public Description Description { get; private set; }

    public Address Address { get; private set; }

    public Money Price { get; private set; }

    public Money CleaningFee { get; private set; }

    public DateTime? LastBookedOnUtc { get; internal set; }

    public List<Amenity> Amenities { get; private set; } = new();
}
~~~

---

### FILE: src\Kronxy.Domain\Apartments\ApartmentErrors.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Apartments;

public static class ApartmentErrors
{
    public static Error NotFound = new(
        "Apartment.NotFound",
        "The apartment with the specified identifier was not found");
}
~~~

---

### FILE: src\Kronxy.Domain\Apartments\Description.cs

~~~text
namespace Kronxy.Domain.Apartments;

public record Description(string Value);
~~~

---

### FILE: src\Kronxy.Domain\Apartments\IApartmentRepository.cs

~~~text
namespace Kronxy.Domain.Apartments;

public interface IApartmentRepository
{
    Task<Apartment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
~~~

---

### FILE: src\Kronxy.Domain\Apartments\Name.cs

~~~text
namespace Kronxy.Domain.Apartments;

public record Name(string Value);
~~~

---

### FILE: src\Kronxy.Domain\Bookings\Booking.cs

~~~text
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Bookings.Events;
using Kronxy.Domain.Shared;

namespace Kronxy.Domain.Bookings;

public sealed class Booking : Entity
{
    private Booking(
        Guid id,
        Guid apartmentId,
        Guid userId,
        DateRange duration,
        Money priceForPeriod,
        Money cleaningFee,
        Money amenitiesUpCharge,
        Money totalPrice,
        BookingStatus status,
        DateTime createdOnUtc)
        : base(id)
    {
        ApartmentId = apartmentId;
        UserId = userId;
        Duration = duration;
        PriceForPeriod = priceForPeriod;
        CleaningFee = cleaningFee;
        AmenitiesUpCharge = amenitiesUpCharge;
        TotalPrice = totalPrice;
        Status = status;
        CreatedOnUtc = createdOnUtc;
    }

    private Booking()
    {
    }

    public Guid ApartmentId { get; private set; }

    public Guid UserId { get; private set; }

    public DateRange Duration { get; private set; }

    public Money PriceForPeriod { get; private set; }

    public Money CleaningFee { get; private set; }

    public Money AmenitiesUpCharge { get; private set; }

    public Money TotalPrice { get; private set; }

    public BookingStatus Status { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? ConfirmedOnUtc { get; private set; }

    public DateTime? RejectedOnUtc { get; private set; }

    public DateTime? CompletedOnUtc { get; private set; }

    public DateTime? CancelledOnUtc { get; private set; }

    public static Booking Reserve(
        Apartment apartment,
        Guid userId,
        DateRange duration,
        DateTime utcNow,
        PricingService pricingService)
    {
        var pricingDetails = pricingService.CalculatePrice(apartment, duration);

        var booking = new Booking(
            Guid.NewGuid(),
            apartment.Id,
            userId,
            duration,
            pricingDetails.PriceForPeriod,
            pricingDetails.CleaningFee,
            pricingDetails.AmenitiesUpCharge,
            pricingDetails.TotalPrice,
            BookingStatus.Reserved,
            utcNow);

        booking.RaiseDomainEvent(new BookingReservedDomainEvent(booking.Id));

        apartment.LastBookedOnUtc = utcNow;

        return booking;
    }

    public Result Confirm(DateTime utcNow)
    {
        if (Status != BookingStatus.Reserved)
        {
            return Result.Failure(BookingErrors.NotReserved);
        }

        Status = BookingStatus.Confirmed;
        ConfirmedOnUtc = utcNow;

        RaiseDomainEvent(new BookingConfirmedDomainEvent(Id));

        return Result.Success();
    }

    public Result Reject(DateTime utcNow)
    {
        if (Status != BookingStatus.Reserved)
        {
            return Result.Failure(BookingErrors.NotReserved);
        }

        Status = BookingStatus.Rejected;
        RejectedOnUtc = utcNow;

        RaiseDomainEvent(new BookingRejectedDomainEvent(Id));

        return Result.Success();
    }

    public Result Complete(DateTime utcNow)
    {
        if (Status != BookingStatus.Confirmed)
        {
            return Result.Failure(BookingErrors.NotConfirmed);
        }

        Status = BookingStatus.Completed;
        CompletedOnUtc = utcNow;

        RaiseDomainEvent(new BookingCompletedDomainEvent(Id));

        return Result.Success();
    }

    public Result Cancel(DateTime utcNow)
    {
        if (Status != BookingStatus.Confirmed)
        {
            return Result.Failure(BookingErrors.NotConfirmed);
        }

        var currentDate = DateOnly.FromDateTime(utcNow);

        if (currentDate > Duration.Start)
        {
            return Result.Failure(BookingErrors.AlreadyStarted);
        }

        Status = BookingStatus.Cancelled;
        CancelledOnUtc = utcNow;

        RaiseDomainEvent(new BookingCancelledDomainEvent(Id));

        return Result.Success();
    }
}
~~~

---

### FILE: src\Kronxy.Domain\Bookings\BookingErrors.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings;

public static class BookingErrors
{
    public static Error NotFound = new(
        "Booking.Found",
        "The booking with the specified identifier was not found");

    public static Error Overlap = new(
        "Booking.Overlap",
        "The current booking is overlapping with an existing one");

    public static Error NotReserved = new(
        "Booking.NotReserved",
        "The booking is not pending");

    public static Error NotConfirmed = new(
        "Booking.NotReserved",
        "The booking is not confirmed");

    public static Error AlreadyStarted = new(
        "Booking.AlreadyStarted",
        "The booking has already started");
}
~~~

---

### FILE: src\Kronxy.Domain\Bookings\BookingStatus.cs

~~~text
namespace Kronxy.Domain.Bookings;

public enum BookingStatus
{
    Reserved = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5
}
~~~

---

### FILE: src\Kronxy.Domain\Bookings\DateRange.cs

~~~text
namespace Kronxy.Domain.Bookings;

public record DateRange
{
    private DateRange()
    {
    }

    public DateOnly Start { get; init; }

    public DateOnly End { get; init; }

    public int LengthInDays => End.DayNumber - Start.DayNumber;

    public static DateRange Create(DateOnly start, DateOnly end)
    {
        if (start > end)
        {
            throw new ApplicationException("End date precedes start date");
        }

        return new DateRange
        {
            Start = start,
            End = end
        };
    }
}
~~~

---

### FILE: src\Kronxy.Domain\Bookings\Events\BookingCancelledDomainEvent.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingCancelledDomainEvent(Guid BookingId) : IDomainEvent;
~~~

---

### FILE: src\Kronxy.Domain\Bookings\Events\BookingCompletedDomainEvent.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingCompletedDomainEvent(Guid BookingId) : IDomainEvent;
~~~

---

### FILE: src\Kronxy.Domain\Bookings\Events\BookingConfirmedDomainEvent.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingConfirmedDomainEvent(Guid BookingId) : IDomainEvent;
~~~

---

### FILE: src\Kronxy.Domain\Bookings\Events\BookingRejectedDomainEvent.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingRejectedDomainEvent(Guid BookingId) : IDomainEvent;
~~~

---

### FILE: src\Kronxy.Domain\Bookings\Events\BookingReservedDomainEvent.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Bookings.Events;

public sealed record BookingReservedDomainEvent(Guid BookingId) : IDomainEvent;
~~~

---

### FILE: src\Kronxy.Domain\Bookings\IBookingRepository.cs

~~~text
using Kronxy.Domain.Apartments;

namespace Kronxy.Domain.Bookings;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> IsOverlappingAsync(
        Apartment apartment,
        DateRange duration,
        CancellationToken cancellationToken = default);

    void Add(Booking booking);
}
~~~

---

### FILE: src\Kronxy.Domain\Bookings\PricingDetails.cs

~~~text
using Kronxy.Domain.Shared;

namespace Kronxy.Domain.Bookings;

public record PricingDetails(
    Money PriceForPeriod,
    Money CleaningFee,
    Money AmenitiesUpCharge,
    Money TotalPrice);
~~~

---

### FILE: src\Kronxy.Domain\Bookings\PricingService.cs

~~~text
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Shared;

namespace Kronxy.Domain.Bookings;

public class PricingService
{
    public PricingDetails CalculatePrice(Apartment apartment, DateRange period)
    {
        var currency = apartment.Price.Currency;

        var priceForPeriod = new Money(
            apartment.Price.Amount * period.LengthInDays,
            currency);

        decimal percentageUpCharge = 0;
        foreach (var amenity in apartment.Amenities)
        {
            percentageUpCharge += amenity switch
            {
                Amenity.GardenView or Amenity.MountainView => 0.05m,
                Amenity.AirConditioning => 0.01m,
                Amenity.Parking => 0.01m,
                _ => 0
            };
        }

        var amenitiesUpCharge = Money.Zero(currency);
        if (percentageUpCharge > 0)
        {
            amenitiesUpCharge = new Money(
                priceForPeriod.Amount * percentageUpCharge,
                currency);
        }

        var totalPrice = Money.Zero(currency);

        totalPrice += priceForPeriod;

        if (!apartment.CleaningFee.IsZero())
        {
            totalPrice += apartment.CleaningFee;
        }

        totalPrice += amenitiesUpCharge;

        return new PricingDetails(priceForPeriod, apartment.CleaningFee, amenitiesUpCharge, totalPrice);
    }
}
~~~

---

### FILE: src\Kronxy.Domain\Kronxy.Domain.csproj

~~~text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR.Contracts" Version="2.0.1" />
  </ItemGroup>

</Project>

~~~

---

### FILE: src\Kronxy.Domain\Projects\IProjectRepository.cs

~~~text
namespace Kronxy.Domain.Projects;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Project?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    void Add(Project project);

    void Remove(Project project);
}
~~~

---

### FILE: src\Kronxy.Domain\Projects\Project.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Projects;

public sealed class Project : Entity
{
    private Project()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Guid OwnerId { get; private set; }

    public ProjectStatus Status { get; private set; }

    public ProjectPriority Priority { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? UpdatedOnUtc { get; private set; }

    public DateTime? DeletedOnUtc { get; private set; }

    public static Project Create(
        string code,
        string name,
        string? description,
        Guid ownerId,
        DateTime utcNow)
    {
        return new Project
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = description,
            OwnerId = ownerId,
            Status = ProjectStatus.Draft,
            Priority = ProjectPriority.Medium,
            IsActive = true,
            CreatedOnUtc = utcNow
        };
    }

    public void Update(
        string code,
        string name,
        string? description,
        Guid ownerId,
        DateTime utcNow)
    {
        Code = code;
        Name = name;
        Description = description;
        OwnerId = ownerId;

        UpdatedOnUtc = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        IsActive = true;
        DeletedOnUtc = null;
        UpdatedOnUtc = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        DeletedOnUtc = utcNow;
        UpdatedOnUtc = utcNow;
    }

    public void ChangeStatus(
        ProjectStatus status,
        DateTime utcNow)
    {
        Status = status;
        UpdatedOnUtc = utcNow;
    }

    public void ChangePriority(
        ProjectPriority priority,
        DateTime utcNow)
    {
        Priority = priority;
        UpdatedOnUtc = utcNow;
    }
}
~~~

---

### FILE: src\Kronxy.Domain\Projects\ProjectErrors.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Projects;

public static class ProjectErrors
{
    public static readonly Error NotFound =
        new(
            "Project.NotFound",
            "The project was not found.");

    public static readonly Error CodeAlreadyInUse =
        new(
            "Project.CodeAlreadyInUse",
            "The project code is already in use.");
}
~~~

---

### FILE: src\Kronxy.Domain\Projects\ProjectPriority.cs

~~~text
namespace Kronxy.Domain.Projects;

public enum ProjectPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
~~~

---

### FILE: src\Kronxy.Domain\Projects\ProjectStatus.cs

~~~text
namespace Kronxy.Domain.Projects;

public enum ProjectStatus
{
    Draft = 1,
    Active = 2,
    OnHold = 3,
    Completed = 4,
    Cancelled = 5
}
~~~

---

### FILE: src\Kronxy.Domain\ProjectTasks\IProjectTaskRepository.cs

~~~text
namespace Kronxy.Domain.ProjectTasks;
public interface IProjectTaskRepository
{
    Task<ProjectTask?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    void Add(ProjectTask projectTask);
    void Remove(ProjectTask projectTask);
}

~~~

---

### FILE: src\Kronxy.Domain\ProjectTasks\ProjectTask.cs

~~~text
using Kronxy.Domain.Abstractions;
namespace Kronxy.Domain.ProjectTasks;
public sealed class ProjectTask : Entity
{
    private ProjectTask()
    {
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid AssignedUserId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public ProjectTaskStatus Status { get; private set; }
    public ProjectTaskPriority Priority { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public decimal? EstimatedHours { get; private set; }
    public decimal? WorkedHours { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }
    public DateTime? DeletedOnUtc { get; private set; }
    public static ProjectTask Create(
        Guid projectId,
        Guid assignedUserId,
        string title,
        string? description,
        DateTime utcNow)
    {
        return new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AssignedUserId = assignedUserId,
            Title = title,
            Description = description,
            Status = ProjectTaskStatus.Pending,
            Priority = ProjectTaskPriority.Medium,
            IsActive = true,
            CreatedOnUtc = utcNow
        };
    }
    public void Update(
        Guid assignedUserId,
        string title,
        string? description,
        DateTime utcNow)
    {
        AssignedUserId = assignedUserId;
        Title = title;
        Description = description;
        UpdatedOnUtc = utcNow;
    }
    public void Activate(DateTime utcNow)
    {
        IsActive = true;
        DeletedOnUtc = null;
        UpdatedOnUtc = utcNow;
    }
    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        DeletedOnUtc = utcNow;
        UpdatedOnUtc = utcNow;
    }
    public void ChangeStatus(
        ProjectTaskStatus status,
        DateTime utcNow)
    {
        Status = status;
        UpdatedOnUtc = utcNow;
    }
    public void ChangePriority(
        ProjectTaskPriority priority,
        DateTime utcNow)
    {
        Priority = priority;
        UpdatedOnUtc = utcNow;
    }
}

~~~

---

### FILE: src\Kronxy.Domain\ProjectTasks\ProjectTaskErrors.cs

~~~text
using Kronxy.Domain.Abstractions;
namespace Kronxy.Domain.ProjectTasks;
public static class ProjectTaskErrors
{
    public static readonly Error NotFound =
        new(
            "ProjectTask.NotFound",
            "The project task was not found.");
}

~~~

---

### FILE: src\Kronxy.Domain\ProjectTasks\ProjectTaskPriority.cs

~~~text
namespace Kronxy.Domain.ProjectTasks;
public enum ProjectTaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

~~~

---

### FILE: src\Kronxy.Domain\ProjectTasks\ProjectTaskStatus.cs

~~~text
namespace Kronxy.Domain.ProjectTasks;
public enum ProjectTaskStatus
{
    Pending = 1,
    InProgress = 2,
    Review = 3,
    Done = 4,
    Cancelled = 5,
    Blocked = 6
}

~~~

---

### FILE: src\Kronxy.Domain\Reviews\Comment.cs

~~~text
namespace Kronxy.Domain.Reviews;

public record Comment(string Value);
~~~

---

### FILE: src\Kronxy.Domain\Reviews\Events\ReviewCreatedDomainEvent.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Reviews.Events;

public sealed record ReviewCreatedDomainEvent(Guid ReviewId) : IDomainEvent;
~~~

---

### FILE: src\Kronxy.Domain\Reviews\Rating.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Reviews;

public sealed record Rating
{
    public static readonly Error Invalid = new("Rating.Invalid", "The rating is invalid");

    private Rating(int value) => Value = value;

    public int Value { get; init; }

    public static Result<Rating> Create(int value)
    {
        if (value < 1 || value > 5)
        {
            return Result.Failure<Rating>(Invalid);
        }

        return new Rating(value);
    }
}
~~~

---

### FILE: src\Kronxy.Domain\Reviews\Review.cs

~~~text
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Bookings;
using Kronxy.Domain.Reviews.Events;

namespace Kronxy.Domain.Reviews;

public sealed class Review : Entity
{
    private Review(
        Guid id,
        Guid apartmentId,
        Guid bookingId,
        Guid userId,
        Rating rating,
        Comment comment,
        DateTime createdOnUtc)
        : base(id)
    {
        ApartmentId = apartmentId;
        BookingId = bookingId;
        UserId = userId;
        Rating = rating;
        Comment = comment;
        CreatedOnUtc = createdOnUtc;
    }

    private Review()
    {
    }

    public Guid ApartmentId { get; private set; }

    public Guid BookingId { get; private set; }

    public Guid UserId { get; private set; }

    public Rating Rating { get; private set; }

    public Comment Comment { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public static Result<Review> Create(
        Booking booking,
        Rating rating,
        Comment comment,
        DateTime createdOnUtc)
    {
        if (booking.Status != BookingStatus.Completed)
        {
            return Result.Failure<Review>(ReviewErrors.NotEligible);
        }

        var review = new Review(
            Guid.NewGuid(),
            booking.ApartmentId,
            booking.Id,
            booking.UserId,
            rating,
            comment,
            createdOnUtc);

        review.RaiseDomainEvent(new ReviewCreatedDomainEvent(review.Id));

        return review;
    }
}
~~~

---

### FILE: src\Kronxy.Domain\Reviews\ReviewErrors.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Reviews;

public static class ReviewErrors
{
    public static readonly Error NotEligible = new(
        "Review.NotEligible",
        "The review is not eligible because the booking is not yet completed");
}
~~~

---

### FILE: src\Kronxy.Domain\Shared\Currency.cs

~~~text
namespace Kronxy.Domain.Shared;

public record Currency
{
    internal static readonly Currency None = new("");
    public static readonly Currency Usd = new("USD");
    public static readonly Currency Eur = new("EUR");

    private Currency(string code) => Code = code;

    public string Code { get; init; }

    public static Currency FromCode(string code)
    {
        return All.FirstOrDefault(c => c.Code == code) ??
               throw new ApplicationException("The currency code is invalid");
    }

    public static readonly IReadOnlyCollection<Currency> All = new[]
    {
        Usd,
        Eur
    };
}
~~~

---

### FILE: src\Kronxy.Domain\Shared\Money.cs

~~~text
namespace Kronxy.Domain.Shared;

public record Money(decimal Amount, Currency Currency)
{
    public static Money operator +(Money first, Money second)
    {
        if (first.Currency != second.Currency)
        {
            throw new InvalidOperationException("Currencies have to be equal");
        }

        return new Money(first.Amount + second.Amount, first.Currency);
    }

    public static Money Zero() => new(0, Currency.None);

    public static Money Zero(Currency currency) => new(0, currency);

    public bool IsZero() => this == Zero(Currency);
}
~~~

---

### FILE: src\Kronxy.Domain\Users\Email.cs

~~~text
namespace Kronxy.Domain.Users;

public record Email(string Value);
~~~

---

### FILE: src\Kronxy.Domain\Users\Events\UserCreatedDomainEvent.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Users.Events;

public sealed record UserCreatedDomainEvent(Guid UserId) : IDomainEvent;
~~~

---

### FILE: src\Kronxy.Domain\Users\FirstName.cs

~~~text
namespace Kronxy.Domain.Users;

public record FirstName(string Value);
~~~

---

### FILE: src\Kronxy.Domain\Users\IUserRepository.cs

~~~text
using UserEntity = Kronxy.Domain.Users.User;
using UserEmail = Kronxy.Domain.Users.Email;
using UserUsername = Kronxy.Domain.Users.Username;

namespace Kronxy.Domain.Users;

public interface IUserRepository
{
    Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserEntity?> GetByEmailAsync(UserEmail email, CancellationToken cancellationToken = default);

    Task<UserEntity?> GetByUsernameAsync(UserUsername username, CancellationToken cancellationToken = default);

    void Add(UserEntity user);
}
~~~

---

### FILE: src\Kronxy.Domain\Users\LastName.cs

~~~text
namespace Kronxy.Domain.Users;

public record LastName(string Value);
~~~

---

### FILE: src\Kronxy.Domain\Users\PhoneNumber.cs

~~~text
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kronxy.Domain.Users;

public record PhoneNumber(string Value);

~~~

---

### FILE: src\Kronxy.Domain\Users\User.cs

~~~text
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Users.Events;

namespace Kronxy.Domain.Users;

public sealed class User : Entity
{
    private User(
        Guid id,
        Username username,
        FirstName firstName,
        LastName lastName,
        Email email,
        PhoneNumber? phoneNumber,
        DateTime createdOnUtc)
        : base(id)
    {
        Username = username;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        IsActive = true;
        CreatedOnUtc = createdOnUtc;
    }

    private User()
    {
    }

    public Username Username { get; private set; }

    public FirstName FirstName { get; private set; }

    public LastName LastName { get; private set; }

    public Email Email { get; private set; }

    public PhoneNumber? PhoneNumber { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? UpdatedOnUtc { get; private set; }

    public DateTime? DeletedOnUtc { get; private set; }

    public static User Create(
        Username username,
        FirstName firstName,
        LastName lastName,
        Email email,
        PhoneNumber? phoneNumber,
        DateTime createdOnUtc)
    {
        var user = new User(
            Guid.NewGuid(),
            username,
            firstName,
            lastName,
            email,
            phoneNumber,
            createdOnUtc);

        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id));

        return user;
    }

    public void Update(
        Username username,
        FirstName firstName,
        LastName lastName,
        Email email,
        PhoneNumber? phoneNumber,
        DateTime updatedOnUtc)
    {
        Username = username;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        UpdatedOnUtc = updatedOnUtc;
    }

    public void Deactivate(DateTime deletedOnUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DeletedOnUtc = deletedOnUtc;
        UpdatedOnUtc = deletedOnUtc;
    }

    public void Activate(DateTime utcNow)
    {
        IsActive = true;
        UpdatedOnUtc = utcNow;
        DeletedOnUtc = null;
    }

}
~~~

---

### FILE: src\Kronxy.Domain\Users\UserErrors.cs

~~~text
using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Users;

public static class UserErrors
{
    public static Error NotFound = new(
        "User.Found",
        "The user with the specified identifier was not found");

    public static Error InvalidCredentials = new(
        "User.InvalidCredentials",
        "The provided credentials were invalid");

    public static Error EmailAlreadyExists = new(
    "User.EmailAlreadyExists",
    "The specified email already exists");

    public static Error UsernameAlreadyExists = new(
        "User.UsernameAlreadyExists",
        "The specified username already exists");
}
~~~

---

### FILE: src\Kronxy.Domain\Users\Username.cs

~~~text
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kronxy.Domain.Users;

public record Username(string Value);

~~~

---

### FILE: src\Kronxy.Infrastructure\ApplicationDbContext.cs

~~~text
using Kronxy.Application.Exceptions;
using Kronxy.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure;

public sealed class ApplicationDbContext : DbContext, IUnitOfWork
{
    private readonly IPublisher _publisher;

    public ApplicationDbContext(DbContextOptions options, IPublisher publisher)
        : base(options)
    {
        _publisher = publisher;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);

            await PublishDomainEventsAsync();

            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Concurrency exception occurred.", ex);
        }
    }

    private async Task PublishDomainEventsAsync()
    {
        var domainEvents = ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                var domainEvents = entity.GetDomainEvents();

                entity.ClearDomainEvents();

                return domainEvents;
            })
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent);
        }
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Clock\DateTimeProvider.cs

~~~text
using Kronxy.Application.Abstractions.Clock;

namespace Kronxy.Infrastructure.Clock;

internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Configurations\ApartmentConfiguration.cs

~~~text
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kronxy.Infrastructure.Configurations;

internal sealed class ApartmentConfiguration : IEntityTypeConfiguration<Apartment>
{
    public void Configure(EntityTypeBuilder<Apartment> builder)
    {
        builder.ToTable("apartments");

        builder.HasKey(apartment => apartment.Id);

        builder.OwnsOne(apartment => apartment.Address);

        builder.Property(apartment => apartment.Name)
            .HasMaxLength(200)
            .HasConversion(name => name.Value, value => new Name(value));

        builder.Property(apartment => apartment.Description)
            .HasMaxLength(2000)
            .HasConversion(description => description.Value, value => new Description(value));

        builder.OwnsOne(apartment => apartment.Price, priceBuilder =>
        {
            priceBuilder.Property(money => money.Currency)
                .HasConversion(currency => currency.Code, code => Currency.FromCode(code));
        });

        builder.OwnsOne(apartment => apartment.CleaningFee, priceBuilder =>
        {
            priceBuilder.Property(money => money.Currency)
                .HasConversion(currency => currency.Code, code => Currency.FromCode(code));
        });

        builder.Property<uint>("Version").IsRowVersion();
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Configurations\BookingConfiguration.cs

~~~text
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Bookings;
using Kronxy.Domain.Shared;
using Kronxy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kronxy.Infrastructure.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(booking => booking.Id);

        builder.OwnsOne(booking => booking.PriceForPeriod, priceBuilder =>
        {
            priceBuilder.Property(money => money.Currency)
                .HasConversion(currency => currency.Code, code => Currency.FromCode(code));
        });

        builder.OwnsOne(booking => booking.CleaningFee, priceBuilder =>
        {
            priceBuilder.Property(money => money.Currency)
                .HasConversion(currency => currency.Code, code => Currency.FromCode(code));
        });

        builder.OwnsOne(booking => booking.AmenitiesUpCharge, priceBuilder =>
        {
            priceBuilder.Property(money => money.Currency)
                .HasConversion(currency => currency.Code, code => Currency.FromCode(code));
        });

        builder.OwnsOne(booking => booking.TotalPrice, priceBuilder =>
        {
            priceBuilder.Property(money => money.Currency)
                .HasConversion(currency => currency.Code, code => Currency.FromCode(code));
        });

        builder.OwnsOne(booking => booking.Duration);

        builder.HasOne<Apartment>()
            .WithMany()
            .HasForeignKey(booking => booking.ApartmentId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(booking => booking.UserId);
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Configurations\ProjectConfiguration.cs

~~~text
using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kronxy.Infrastructure.Configurations;
internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id)
            .HasColumnName("id");
        builder.Property(project => project.Code)
            .HasMaxLength(50)
            .HasColumnName("code");
        builder.Property(project => project.Name)
            .HasMaxLength(200)
            .HasColumnName("name");
        builder.Property(project => project.Description)
            .HasMaxLength(2000)
            .HasColumnName("description");
        builder.Property(project => project.OwnerId)
            .HasColumnName("owner_id");
        builder.Property(project => project.Status)
            .HasConversion<int>()
            .HasColumnName("status");
        builder.Property(project => project.Priority)
            .HasConversion<int>()
            .HasColumnName("priority");
        builder.Property(project => project.StartDate)
            .HasColumnName("start_date");
        builder.Property(project => project.EndDate)
            .HasColumnName("end_date");
        builder.Property(project => project.IsActive)
            .HasColumnName("is_active");
        builder.Property(project => project.CreatedOnUtc)
            .HasColumnName("created_on_utc");
        builder.Property(project => project.UpdatedOnUtc)
            .HasColumnName("updated_on_utc");
        builder.Property(project => project.DeletedOnUtc)
            .HasColumnName("deleted_on_utc");
        builder.HasIndex(project => project.Code)
            .IsUnique();
        builder.HasIndex(project => project.OwnerId);
        builder.HasOne<Kronxy.Domain.Users.User>()
            .WithMany()
            .HasForeignKey(project => project.OwnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_projects_users_owner_id");
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Configurations\ProjectTaskConfiguration.cs

~~~text
using Kronxy.Domain.ProjectTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kronxy.Infrastructure.Configurations;
internal sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("project_tasks");
        builder.HasKey(projectTask => projectTask.Id);
        builder.Property(projectTask => projectTask.Id)
            .HasColumnName("id");
        builder.Property(projectTask => projectTask.ProjectId)
            .HasColumnName("project_id");
        builder.Property(projectTask => projectTask.AssignedUserId)
            .HasColumnName("assigned_user_id");
        builder.Property(projectTask => projectTask.Title)
            .HasMaxLength(200)
            .HasColumnName("title");
        builder.Property(projectTask => projectTask.Description)
            .HasMaxLength(2000)
            .HasColumnName("description");
        builder.Property(projectTask => projectTask.Status)
            .HasConversion<int>()
            .HasColumnName("status");
        builder.Property(projectTask => projectTask.Priority)
            .HasConversion<int>()
            .HasColumnName("priority");
        builder.Property(projectTask => projectTask.DueDate)
            .HasColumnName("due_date");
        builder.Property(projectTask => projectTask.EstimatedHours)
            .HasPrecision(10, 2)
            .HasColumnName("estimated_hours");
        builder.Property(projectTask => projectTask.WorkedHours)
            .HasPrecision(10, 2)
            .HasColumnName("worked_hours");
        builder.Property(projectTask => projectTask.IsActive)
            .HasColumnName("is_active");
        builder.Property(projectTask => projectTask.CreatedOnUtc)
            .HasColumnName("created_on_utc");
        builder.Property(projectTask => projectTask.UpdatedOnUtc)
            .HasColumnName("updated_on_utc");
        builder.Property(projectTask => projectTask.DeletedOnUtc)
            .HasColumnName("deleted_on_utc");
        builder.HasIndex(projectTask => projectTask.ProjectId);
        builder.HasIndex(projectTask => projectTask.AssignedUserId);
        builder.HasOne<Kronxy.Domain.Projects.Project>()
            .WithMany()
            .HasForeignKey(projectTask => projectTask.ProjectId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_project_tasks_projects_project_id");
        builder.HasOne<Kronxy.Domain.Users.User>()
            .WithMany()
            .HasForeignKey(projectTask => projectTask.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_project_tasks_users_assigned_user_id");
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Configurations\ReviewConfiguration.cs

~~~text
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Bookings;
using Kronxy.Domain.Reviews;
using Kronxy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kronxy.Infrastructure.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Rating)
            .HasConversion(rating => rating.Value, value => Rating.Create(value).Value);

        builder.Property(review => review.Comment)
            .HasMaxLength(200)
            .HasConversion(comment => comment.Value, value => new Comment(value));

        builder.HasOne<Apartment>()
            .WithMany()
            .HasForeignKey(review => review.ApartmentId);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(review => review.BookingId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(review => review.UserId);
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Configurations\UserConfiguration.cs

~~~text
using Kronxy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kronxy.Infrastructure.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .HasMaxLength(100)
            .HasConversion(
                username => username.Value,
                value => new Username(value));

        builder.HasIndex(user => user.Username).IsUnique();

        builder.Property(user => user.FirstName)
            .HasMaxLength(200)
            .HasConversion(firstName => firstName.Value, value => new FirstName(value));

        builder.Property(user => user.LastName)
            .HasMaxLength(200)
            .HasConversion(firstName => firstName.Value, value => new LastName(value));

        builder.Property(user => user.Email)
            .HasMaxLength(400)
            .HasConversion(email => email.Value, value => new Domain.Users.Email(value)); ;

        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(30)
            .HasConversion(
                phone => phone == null ? null : phone.Value,
                value => value == null ? null : new PhoneNumber(value)
            );

        builder.Property(user => user.Username)
            .HasMaxLength(100)
            .HasConversion(
                username => username.Value,
                value => new Username(value)
            );

        builder.HasIndex(user => user.Username).IsUnique();

    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Data\DateOnlyTypeHandler.cs

~~~text
using Dapper;
using System.Data;

namespace Kronxy.Infrastructure.Data;

internal sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Data\SqlConnectionFactory.cs

~~~text
using System.Data;
using Kronxy.Application.Abstractions.Data;
using Npgsql;

namespace Kronxy.Infrastructure.Data;

internal sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        return connection;
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\DependencyInjection.cs

~~~text
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Email;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Bookings;
using Kronxy.Domain.Users;
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

        services.AddScoped<IApartmentRepository, ApartmentRepository>();

        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddScoped<IProjectRepository, ProjectRepository>();

        services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();

        //---------------------------------------------------------------

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<ISqlConnectionFactory>(_ =>
            new SqlConnectionFactory(connectionString));

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        return services;
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Email\EmailService.cs

~~~text
using Kronxy.Application.Abstractions.Email;

namespace Kronxy.Infrastructure.Email;

internal sealed class EmailService : IEmailService
{
    public Task SendAsync(Domain.Users.Email recipient, string subject, string body)
    {
        return Task.CompletedTask;
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Kronxy.Infrastructure.csproj

~~~text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" Version="8.0.3" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Kronxy.Application\Kronxy.Application.csproj" />
  </ItemGroup>

</Project>

~~~

---

### FILE: src\Kronxy.Infrastructure\Migrations\20240104150149_Create_Database.cs

~~~text
// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Create_Database : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "apartments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    address_country = table.Column<string>(type: "text", nullable: false),
                    address_state = table.Column<string>(type: "text", nullable: false),
                    address_zip_code = table.Column<string>(type: "text", nullable: false),
                    address_city = table.Column<string>(type: "text", nullable: false),
                    address_street = table.Column<string>(type: "text", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    price_currency = table.Column<string>(type: "text", nullable: false),
                    cleaning_fee_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    cleaning_fee_currency = table.Column<string>(type: "text", nullable: false),
                    last_booked_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    amenities = table.Column<int[]>(type: "integer[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_apartments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    apartment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    duration_start = table.Column<DateOnly>(type: "date", nullable: false),
                    duration_end = table.Column<DateOnly>(type: "date", nullable: false),
                    price_for_period_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    price_for_period_currency = table.Column<string>(type: "text", nullable: false),
                    cleaning_fee_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    cleaning_fee_currency = table.Column<string>(type: "text", nullable: false),
                    amenities_up_charge_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    amenities_up_charge_currency = table.Column<string>(type: "text", nullable: false),
                    total_price_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    total_price_currency = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    confirmed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.ForeignKey(
                        name: "fk_bookings_apartments_apartment_id",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bookings_user_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    apartment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_reviews_apartments_apartment_id",
                        column: x => x.apartment_id,
                        principalTable: "apartments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_user_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_apartment_id",
                table: "bookings",
                column: "apartment_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_user_id",
                table: "bookings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_apartment_id",
                table: "reviews",
                column: "apartment_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_booking_id",
                table: "reviews",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_user_id",
                table: "reviews",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "apartments");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Migrations\20240104150149_Create_Database.Designer.cs

~~~text
// <auto-generated />
using System;
using Kronxy.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20240104150149_Create_Database")]
    partial class Create_Database
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int[]>("Amenities")
                        .IsRequired()
                        .HasColumnType("integer[]")
                        .HasColumnName("amenities");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateTime?>("LastBookedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_booked_on_utc");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id")
                        .HasName("pk_apartments");

                    b.ToTable("apartments", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<DateTime?>("CancelledOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("cancelled_on_utc");

                    b.Property<DateTime?>("CompletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("completed_on_utc");

                    b.Property<DateTime?>("ConfirmedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("confirmed_on_utc");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("RejectedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("rejected_on_utc");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_bookings");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_bookings_apartment_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_bookings_user_id");

                    b.ToTable("bookings", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<Guid>("BookingId")
                        .HasColumnType("uuid")
                        .HasColumnName("booking_id");

                    b.Property<string>("Comment")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("comment");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<int>("Rating")
                        .HasColumnType("integer")
                        .HasColumnName("rating");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_reviews");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_reviews_apartment_id");

                    b.HasIndex("BookingId")
                        .HasDatabaseName("ix_reviews_booking_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_reviews_user_id");

                    b.ToTable("reviews", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Users.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(400)
                        .HasColumnType("character varying(400)")
                        .HasColumnName("email");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("first_name");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("last_name");

                    b.HasKey("Id")
                        .HasName("pk_users");

                    b.HasIndex("Email")
                        .IsUnique()
                        .HasDatabaseName("ix_users_email");

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.OwnsOne("Kronxy.Domain.Apartments.Address", "Address", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<string>("City")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_city");

                            b1.Property<string>("Country")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_country");

                            b1.Property<string>("State")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_state");

                            b1.Property<string>("Street")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_street");

                            b1.Property<string>("ZipCode")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_zip_code");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "Price", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.Navigation("Address")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Price")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_user_user_id");

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "AmenitiesUpCharge", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("amenities_up_charge_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("amenities_up_charge_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "PriceForPeriod", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_for_period_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_for_period_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "TotalPrice", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("total_price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("total_price_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Bookings.DateRange", "Duration", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<DateOnly>("End")
                                .HasColumnType("date")
                                .HasColumnName("duration_end");

                            b1.Property<DateOnly>("Start")
                                .HasColumnType("date")
                                .HasColumnName("duration_start");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.Navigation("AmenitiesUpCharge")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Duration")
                        .IsRequired();

                    b.Navigation("PriceForPeriod")
                        .IsRequired();

                    b.Navigation("TotalPrice")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Bookings.Booking", null)
                        .WithMany()
                        .HasForeignKey("BookingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_bookings_booking_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_user_user_id");
                });
#pragma warning restore 612, 618
        }
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Migrations\20260428145405_Add_User_Management_Fields.cs

~~~text
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_User_Management_Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_on_utc",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_on_utc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_on_utc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_username",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_on_utc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "deleted_on_utc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "updated_on_utc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "username",
                table: "users");
        }
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Migrations\20260428145405_Add_User_Management_Fields.Designer.cs

~~~text
// <auto-generated />
using System;
using Kronxy.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260428145405_Add_User_Management_Fields")]
    partial class Add_User_Management_Fields
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int[]>("Amenities")
                        .IsRequired()
                        .HasColumnType("integer[]")
                        .HasColumnName("amenities");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateTime?>("LastBookedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_booked_on_utc");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id")
                        .HasName("pk_apartments");

                    b.ToTable("apartments", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<DateTime?>("CancelledOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("cancelled_on_utc");

                    b.Property<DateTime?>("CompletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("completed_on_utc");

                    b.Property<DateTime?>("ConfirmedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("confirmed_on_utc");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("RejectedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("rejected_on_utc");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_bookings");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_bookings_apartment_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_bookings_user_id");

                    b.ToTable("bookings", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<Guid>("BookingId")
                        .HasColumnType("uuid")
                        .HasColumnName("booking_id");

                    b.Property<string>("Comment")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("comment");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<int>("Rating")
                        .HasColumnType("integer")
                        .HasColumnName("rating");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_reviews");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_reviews_apartment_id");

                    b.HasIndex("BookingId")
                        .HasDatabaseName("ix_reviews_booking_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_reviews_user_id");

                    b.ToTable("reviews", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Users.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("DeletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_on_utc");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(400)
                        .HasColumnType("character varying(400)")
                        .HasColumnName("email");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("first_name");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("last_name");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasColumnName("phone_number");

                    b.Property<DateTime?>("UpdatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_on_utc");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("username");

                    b.HasKey("Id")
                        .HasName("pk_users");

                    b.HasIndex("Email")
                        .IsUnique()
                        .HasDatabaseName("ix_users_email");

                    b.HasIndex("Username")
                        .IsUnique()
                        .HasDatabaseName("ix_users_username");

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.OwnsOne("Kronxy.Domain.Apartments.Address", "Address", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<string>("City")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_city");

                            b1.Property<string>("Country")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_country");

                            b1.Property<string>("State")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_state");

                            b1.Property<string>("Street")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_street");

                            b1.Property<string>("ZipCode")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_zip_code");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "Price", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.Navigation("Address")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Price")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_user_user_id");

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "AmenitiesUpCharge", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("amenities_up_charge_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("amenities_up_charge_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "PriceForPeriod", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_for_period_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_for_period_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "TotalPrice", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("total_price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("total_price_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Bookings.DateRange", "Duration", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<DateOnly>("End")
                                .HasColumnType("date")
                                .HasColumnName("duration_end");

                            b1.Property<DateOnly>("Start")
                                .HasColumnType("date")
                                .HasColumnName("duration_start");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.Navigation("AmenitiesUpCharge")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Duration")
                        .IsRequired();

                    b.Navigation("PriceForPeriod")
                        .IsRequired();

                    b.Navigation("TotalPrice")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Bookings.Booking", null)
                        .WithMany()
                        .HasForeignKey("BookingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_bookings_booking_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_user_user_id");
                });
#pragma warning restore 612, 618
        }
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Migrations\20260603180007_Add_Projects.cs

~~~text
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Projects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                    table.ForeignKey(
                        name: "fk_projects_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projects_code",
                table: "projects",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_owner_id",
                table: "projects",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Migrations\20260603180007_Add_Projects.Designer.cs

~~~text
// <auto-generated />
using System;
using Kronxy.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260603180007_Add_Projects")]
    partial class Add_Projects
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int[]>("Amenities")
                        .IsRequired()
                        .HasColumnType("integer[]")
                        .HasColumnName("amenities");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateTime?>("LastBookedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_booked_on_utc");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id")
                        .HasName("pk_apartments");

                    b.ToTable("apartments", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<DateTime?>("CancelledOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("cancelled_on_utc");

                    b.Property<DateTime?>("CompletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("completed_on_utc");

                    b.Property<DateTime?>("ConfirmedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("confirmed_on_utc");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("RejectedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("rejected_on_utc");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_bookings");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_bookings_apartment_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_bookings_user_id");

                    b.ToTable("bookings", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Projects.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("code");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("DeletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_on_utc");

                    b.Property<string>("Description")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateOnly?>("EndDate")
                        .HasColumnType("date")
                        .HasColumnName("end_date");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<Guid>("OwnerId")
                        .HasColumnType("uuid")
                        .HasColumnName("owner_id");

                    b.Property<int>("Priority")
                        .HasColumnType("integer")
                        .HasColumnName("priority");

                    b.Property<DateOnly?>("StartDate")
                        .HasColumnType("date")
                        .HasColumnName("start_date");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<DateTime?>("UpdatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_on_utc");

                    b.HasKey("Id")
                        .HasName("pk_projects");

                    b.HasIndex("Code")
                        .IsUnique()
                        .HasDatabaseName("ix_projects_code");

                    b.HasIndex("OwnerId")
                        .HasDatabaseName("ix_projects_owner_id");

                    b.ToTable("projects", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<Guid>("BookingId")
                        .HasColumnType("uuid")
                        .HasColumnName("booking_id");

                    b.Property<string>("Comment")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("comment");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<int>("Rating")
                        .HasColumnType("integer")
                        .HasColumnName("rating");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_reviews");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_reviews_apartment_id");

                    b.HasIndex("BookingId")
                        .HasDatabaseName("ix_reviews_booking_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_reviews_user_id");

                    b.ToTable("reviews", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Users.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("DeletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_on_utc");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(400)
                        .HasColumnType("character varying(400)")
                        .HasColumnName("email");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("first_name");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("last_name");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasColumnName("phone_number");

                    b.Property<DateTime?>("UpdatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_on_utc");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("username");

                    b.HasKey("Id")
                        .HasName("pk_users");

                    b.HasIndex("Email")
                        .IsUnique()
                        .HasDatabaseName("ix_users_email");

                    b.HasIndex("Username")
                        .IsUnique()
                        .HasDatabaseName("ix_users_username");

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.OwnsOne("Kronxy.Domain.Apartments.Address", "Address", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<string>("City")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_city");

                            b1.Property<string>("Country")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_country");

                            b1.Property<string>("State")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_state");

                            b1.Property<string>("Street")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_street");

                            b1.Property<string>("ZipCode")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_zip_code");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "Price", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.Navigation("Address")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Price")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_user_user_id");

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "AmenitiesUpCharge", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("amenities_up_charge_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("amenities_up_charge_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "PriceForPeriod", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_for_period_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_for_period_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "TotalPrice", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("total_price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("total_price_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Bookings.DateRange", "Duration", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<DateOnly>("End")
                                .HasColumnType("date")
                                .HasColumnName("duration_end");

                            b1.Property<DateOnly>("Start")
                                .HasColumnType("date")
                                .HasColumnName("duration_start");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.Navigation("AmenitiesUpCharge")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Duration")
                        .IsRequired();

                    b.Navigation("PriceForPeriod")
                        .IsRequired();

                    b.Navigation("TotalPrice")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.Projects.Project", b =>
                {
                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("OwnerId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_projects_users_owner_id");
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Bookings.Booking", null)
                        .WithMany()
                        .HasForeignKey("BookingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_bookings_booking_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_user_user_id");
                });
#pragma warning restore 612, 618
        }
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Migrations\20260603182025_Add_ProjectTasks.cs

~~~text
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_ProjectTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    estimated_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    worked_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_tasks_users_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_tasks_assigned_user_id",
                table: "project_tasks",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_tasks_project_id",
                table: "project_tasks",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_tasks");
        }
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Migrations\20260603182025_Add_ProjectTasks.Designer.cs

~~~text
// <auto-generated />
using System;
using Kronxy.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260603182025_Add_ProjectTasks")]
    partial class Add_ProjectTasks
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int[]>("Amenities")
                        .IsRequired()
                        .HasColumnType("integer[]")
                        .HasColumnName("amenities");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateTime?>("LastBookedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_booked_on_utc");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id")
                        .HasName("pk_apartments");

                    b.ToTable("apartments", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<DateTime?>("CancelledOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("cancelled_on_utc");

                    b.Property<DateTime?>("CompletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("completed_on_utc");

                    b.Property<DateTime?>("ConfirmedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("confirmed_on_utc");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("RejectedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("rejected_on_utc");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_bookings");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_bookings_apartment_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_bookings_user_id");

                    b.ToTable("bookings", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.ProjectTasks.ProjectTask", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AssignedUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("assigned_user_id");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("DeletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_on_utc");

                    b.Property<string>("Description")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateOnly?>("DueDate")
                        .HasColumnType("date")
                        .HasColumnName("due_date");

                    b.Property<decimal?>("EstimatedHours")
                        .HasPrecision(10, 2)
                        .HasColumnType("numeric(10,2)")
                        .HasColumnName("estimated_hours");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<int>("Priority")
                        .HasColumnType("integer")
                        .HasColumnName("priority");

                    b.Property<Guid>("ProjectId")
                        .HasColumnType("uuid")
                        .HasColumnName("project_id");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("title");

                    b.Property<DateTime?>("UpdatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_on_utc");

                    b.Property<decimal?>("WorkedHours")
                        .HasPrecision(10, 2)
                        .HasColumnType("numeric(10,2)")
                        .HasColumnName("worked_hours");

                    b.HasKey("Id")
                        .HasName("pk_project_tasks");

                    b.HasIndex("AssignedUserId")
                        .HasDatabaseName("ix_project_tasks_assigned_user_id");

                    b.HasIndex("ProjectId")
                        .HasDatabaseName("ix_project_tasks_project_id");

                    b.ToTable("project_tasks", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Projects.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("code");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("DeletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_on_utc");

                    b.Property<string>("Description")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateOnly?>("EndDate")
                        .HasColumnType("date")
                        .HasColumnName("end_date");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<Guid>("OwnerId")
                        .HasColumnType("uuid")
                        .HasColumnName("owner_id");

                    b.Property<int>("Priority")
                        .HasColumnType("integer")
                        .HasColumnName("priority");

                    b.Property<DateOnly?>("StartDate")
                        .HasColumnType("date")
                        .HasColumnName("start_date");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<DateTime?>("UpdatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_on_utc");

                    b.HasKey("Id")
                        .HasName("pk_projects");

                    b.HasIndex("Code")
                        .IsUnique()
                        .HasDatabaseName("ix_projects_code");

                    b.HasIndex("OwnerId")
                        .HasDatabaseName("ix_projects_owner_id");

                    b.ToTable("projects", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<Guid>("BookingId")
                        .HasColumnType("uuid")
                        .HasColumnName("booking_id");

                    b.Property<string>("Comment")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("comment");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<int>("Rating")
                        .HasColumnType("integer")
                        .HasColumnName("rating");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_reviews");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_reviews_apartment_id");

                    b.HasIndex("BookingId")
                        .HasDatabaseName("ix_reviews_booking_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_reviews_user_id");

                    b.ToTable("reviews", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Users.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("DeletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_on_utc");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(400)
                        .HasColumnType("character varying(400)")
                        .HasColumnName("email");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("first_name");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("last_name");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasColumnName("phone_number");

                    b.Property<DateTime?>("UpdatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_on_utc");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("username");

                    b.HasKey("Id")
                        .HasName("pk_users");

                    b.HasIndex("Email")
                        .IsUnique()
                        .HasDatabaseName("ix_users_email");

                    b.HasIndex("Username")
                        .IsUnique()
                        .HasDatabaseName("ix_users_username");

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.OwnsOne("Kronxy.Domain.Apartments.Address", "Address", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<string>("City")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_city");

                            b1.Property<string>("Country")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_country");

                            b1.Property<string>("State")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_state");

                            b1.Property<string>("Street")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_street");

                            b1.Property<string>("ZipCode")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_zip_code");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "Price", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.Navigation("Address")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Price")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_user_user_id");

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "AmenitiesUpCharge", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("amenities_up_charge_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("amenities_up_charge_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "PriceForPeriod", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_for_period_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_for_period_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "TotalPrice", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("total_price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("total_price_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Bookings.DateRange", "Duration", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<DateOnly>("End")
                                .HasColumnType("date")
                                .HasColumnName("duration_end");

                            b1.Property<DateOnly>("Start")
                                .HasColumnType("date")
                                .HasColumnName("duration_start");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.Navigation("AmenitiesUpCharge")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Duration")
                        .IsRequired();

                    b.Navigation("PriceForPeriod")
                        .IsRequired();

                    b.Navigation("TotalPrice")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.ProjectTasks.ProjectTask", b =>
                {
                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("AssignedUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_project_tasks_users_assigned_user_id");

                    b.HasOne("Kronxy.Domain.Projects.Project", null)
                        .WithMany()
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_project_tasks_projects_project_id");
                });

            modelBuilder.Entity("Kronxy.Domain.Projects.Project", b =>
                {
                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("OwnerId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_projects_users_owner_id");
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Bookings.Booking", null)
                        .WithMany()
                        .HasForeignKey("BookingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_bookings_booking_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_user_user_id");
                });
#pragma warning restore 612, 618
        }
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Migrations\ApplicationDbContextModelSnapshot.cs

~~~text
// <auto-generated />
using System;
using Kronxy.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int[]>("Amenities")
                        .IsRequired()
                        .HasColumnType("integer[]")
                        .HasColumnName("amenities");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateTime?>("LastBookedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_booked_on_utc");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id")
                        .HasName("pk_apartments");

                    b.ToTable("apartments", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<DateTime?>("CancelledOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("cancelled_on_utc");

                    b.Property<DateTime?>("CompletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("completed_on_utc");

                    b.Property<DateTime?>("ConfirmedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("confirmed_on_utc");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("RejectedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("rejected_on_utc");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_bookings");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_bookings_apartment_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_bookings_user_id");

                    b.ToTable("bookings", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.ProjectTasks.ProjectTask", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AssignedUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("assigned_user_id");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("DeletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_on_utc");

                    b.Property<string>("Description")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateOnly?>("DueDate")
                        .HasColumnType("date")
                        .HasColumnName("due_date");

                    b.Property<decimal?>("EstimatedHours")
                        .HasPrecision(10, 2)
                        .HasColumnType("numeric(10,2)")
                        .HasColumnName("estimated_hours");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<int>("Priority")
                        .HasColumnType("integer")
                        .HasColumnName("priority");

                    b.Property<Guid>("ProjectId")
                        .HasColumnType("uuid")
                        .HasColumnName("project_id");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("title");

                    b.Property<DateTime?>("UpdatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_on_utc");

                    b.Property<decimal?>("WorkedHours")
                        .HasPrecision(10, 2)
                        .HasColumnType("numeric(10,2)")
                        .HasColumnName("worked_hours");

                    b.HasKey("Id")
                        .HasName("pk_project_tasks");

                    b.HasIndex("AssignedUserId")
                        .HasDatabaseName("ix_project_tasks_assigned_user_id");

                    b.HasIndex("ProjectId")
                        .HasDatabaseName("ix_project_tasks_project_id");

                    b.ToTable("project_tasks", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Projects.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("code");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("DeletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_on_utc");

                    b.Property<string>("Description")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("description");

                    b.Property<DateOnly?>("EndDate")
                        .HasColumnType("date")
                        .HasColumnName("end_date");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<Guid>("OwnerId")
                        .HasColumnType("uuid")
                        .HasColumnName("owner_id");

                    b.Property<int>("Priority")
                        .HasColumnType("integer")
                        .HasColumnName("priority");

                    b.Property<DateOnly?>("StartDate")
                        .HasColumnType("date")
                        .HasColumnName("start_date");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<DateTime?>("UpdatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_on_utc");

                    b.HasKey("Id")
                        .HasName("pk_projects");

                    b.HasIndex("Code")
                        .IsUnique()
                        .HasDatabaseName("ix_projects_code");

                    b.HasIndex("OwnerId")
                        .HasDatabaseName("ix_projects_owner_id");

                    b.ToTable("projects", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ApartmentId")
                        .HasColumnType("uuid")
                        .HasColumnName("apartment_id");

                    b.Property<Guid>("BookingId")
                        .HasColumnType("uuid")
                        .HasColumnName("booking_id");

                    b.Property<string>("Comment")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("comment");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<int>("Rating")
                        .HasColumnType("integer")
                        .HasColumnName("rating");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_reviews");

                    b.HasIndex("ApartmentId")
                        .HasDatabaseName("ix_reviews_apartment_id");

                    b.HasIndex("BookingId")
                        .HasDatabaseName("ix_reviews_booking_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_reviews_user_id");

                    b.ToTable("reviews", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Users.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on_utc");

                    b.Property<DateTime?>("DeletedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_on_utc");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(400)
                        .HasColumnType("character varying(400)")
                        .HasColumnName("email");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("first_name");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("last_name");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasColumnName("phone_number");

                    b.Property<DateTime?>("UpdatedOnUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_on_utc");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("username");

                    b.HasKey("Id")
                        .HasName("pk_users");

                    b.HasIndex("Email")
                        .IsUnique()
                        .HasDatabaseName("ix_users_email");

                    b.HasIndex("Username")
                        .IsUnique()
                        .HasDatabaseName("ix_users_username");

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("Kronxy.Domain.Apartments.Apartment", b =>
                {
                    b.OwnsOne("Kronxy.Domain.Apartments.Address", "Address", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<string>("City")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_city");

                            b1.Property<string>("Country")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_country");

                            b1.Property<string>("State")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_state");

                            b1.Property<string>("Street")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_street");

                            b1.Property<string>("ZipCode")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("address_zip_code");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "Price", b1 =>
                        {
                            b1.Property<Guid>("ApartmentId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_currency");

                            b1.HasKey("ApartmentId");

                            b1.ToTable("apartments");

                            b1.WithOwner()
                                .HasForeignKey("ApartmentId")
                                .HasConstraintName("fk_apartments_apartments_id");
                        });

                    b.Navigation("Address")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Price")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.Bookings.Booking", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_bookings_user_user_id");

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "AmenitiesUpCharge", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("amenities_up_charge_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("amenities_up_charge_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "CleaningFee", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("cleaning_fee_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("cleaning_fee_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "PriceForPeriod", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("price_for_period_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("price_for_period_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Shared.Money", "TotalPrice", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<decimal>("Amount")
                                .HasColumnType("numeric")
                                .HasColumnName("total_price_amount");

                            b1.Property<string>("Currency")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasColumnName("total_price_currency");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.OwnsOne("Kronxy.Domain.Bookings.DateRange", "Duration", b1 =>
                        {
                            b1.Property<Guid>("BookingId")
                                .HasColumnType("uuid")
                                .HasColumnName("id");

                            b1.Property<DateOnly>("End")
                                .HasColumnType("date")
                                .HasColumnName("duration_end");

                            b1.Property<DateOnly>("Start")
                                .HasColumnType("date")
                                .HasColumnName("duration_start");

                            b1.HasKey("BookingId");

                            b1.ToTable("bookings");

                            b1.WithOwner()
                                .HasForeignKey("BookingId")
                                .HasConstraintName("fk_bookings_bookings_id");
                        });

                    b.Navigation("AmenitiesUpCharge")
                        .IsRequired();

                    b.Navigation("CleaningFee")
                        .IsRequired();

                    b.Navigation("Duration")
                        .IsRequired();

                    b.Navigation("PriceForPeriod")
                        .IsRequired();

                    b.Navigation("TotalPrice")
                        .IsRequired();
                });

            modelBuilder.Entity("Kronxy.Domain.ProjectTasks.ProjectTask", b =>
                {
                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("AssignedUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_project_tasks_users_assigned_user_id");

                    b.HasOne("Kronxy.Domain.Projects.Project", null)
                        .WithMany()
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_project_tasks_projects_project_id");
                });

            modelBuilder.Entity("Kronxy.Domain.Projects.Project", b =>
                {
                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("OwnerId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_projects_users_owner_id");
                });

            modelBuilder.Entity("Kronxy.Domain.Reviews.Review", b =>
                {
                    b.HasOne("Kronxy.Domain.Apartments.Apartment", null)
                        .WithMany()
                        .HasForeignKey("ApartmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_apartments_apartment_id");

                    b.HasOne("Kronxy.Domain.Bookings.Booking", null)
                        .WithMany()
                        .HasForeignKey("BookingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_bookings_booking_id");

                    b.HasOne("Kronxy.Domain.Users.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_reviews_user_user_id");
                });
#pragma warning restore 612, 618
        }
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Repositories\ApartmentRepository.cs

~~~text
using Kronxy.Domain.Apartments;

namespace Kronxy.Infrastructure.Repositories;

internal sealed class ApartmentRepository : Repository<Apartment>, IApartmentRepository
{
    public ApartmentRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Repositories\BookingRepository.cs

~~~text
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure.Repositories;

internal sealed class BookingRepository : Repository<Booking>, IBookingRepository
{
    private static readonly BookingStatus[] ActiveBookingStatuses =
    {
        BookingStatus.Reserved,
        BookingStatus.Confirmed,
        BookingStatus.Completed
    };

    public BookingRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<bool> IsOverlappingAsync(
        Apartment apartment,
        DateRange duration,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Booking>()
            .AnyAsync(
                booking =>
                    booking.ApartmentId == apartment.Id &&
                    booking.Duration.Start <= duration.End &&
                    booking.Duration.End >= duration.Start &&
                    ActiveBookingStatuses.Contains(booking.Status),
                cancellationToken);
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Repositories\ProjectRepository.cs

~~~text
using Kronxy.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure.Repositories;

internal sealed class ProjectRepository : Repository<Project>, IProjectRepository
{
    public ProjectRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Project?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Project>()
            .FirstOrDefaultAsync(
                project => project.Code == code,
                cancellationToken);
    }

    public void Remove(Project project)
    {
        DbContext.Remove(project);
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Repositories\ProjectTaskRepository.cs

~~~text
using Kronxy.Domain.ProjectTasks;
namespace Kronxy.Infrastructure.Repositories;
internal sealed class ProjectTaskRepository : Repository<ProjectTask>, IProjectTaskRepository
{
    public ProjectTaskRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
    public void Remove(ProjectTask projectTask)
    {
        DbContext.Remove(projectTask);
    }
}

~~~

---

### FILE: src\Kronxy.Infrastructure\Repositories\Repository.cs

~~~text
using Kronxy.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure.Repositories;

internal abstract class Repository<T>
    where T : Entity
{
    protected readonly ApplicationDbContext DbContext;

    protected Repository(ApplicationDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public async Task<T?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<T>()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public void Add(T entity)
    {
        DbContext.Add(entity);
    }
}
~~~

---

### FILE: src\Kronxy.Infrastructure\Repositories\UserRepository.cs

~~~text
using Microsoft.EntityFrameworkCore;
using UserEntity = Kronxy.Domain.Users.User;
using UserEmail = Kronxy.Domain.Users.Email;
using UserUsername = Kronxy.Domain.Users.Username;
using Kronxy.Domain.Users;

namespace Kronxy.Infrastructure.Repositories;

internal sealed class UserRepository : Repository<UserEntity>, IUserRepository
{
    public UserRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<UserEntity?> GetByEmailAsync(
        UserEmail email,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<UserEntity>()
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public async Task<UserEntity?> GetByUsernameAsync(
        UserUsername username,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<UserEntity>()
            .FirstOrDefaultAsync(user => user.Username == username, cancellationToken);
    }
}
~~~
