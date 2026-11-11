using Lamort.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LamortDb")
    ?? throw new InvalidOperationException("Connection string 'LamortDb' is required.");

builder.Services.AddDbContext<WebDbContext>(options => options.UseNpgsql(connectionString));

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var db = scope.ServiceProvider.GetRequiredService<WebDbContext>();

await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS identity");
await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS mortgage");
await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS api");
await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS docrender");

await using var connection = db.Database.GetDbConnection();
if (connection.State != System.Data.ConnectionState.Open)
{
    await connection.OpenAsync();
}

await using (var command = connection.CreateCommand())
{
    command.CommandText = "SELECT to_regclass('identity.users') IS NOT NULL";
    var identityUsersExists = (bool?)await command.ExecuteScalarAsync() ?? false;

    if (!identityUsersExists)
    {
        var databaseCreator = db.Database.GetService<IRelationalDatabaseCreator>();
        await databaseCreator.CreateTablesAsync();
    }
}

await db.Database.ExecuteSqlRawAsync(
    """
    CREATE TABLE IF NOT EXISTS mortgage.applications (
        id uuid PRIMARY KEY,
        user_id character varying(450) NOT NULL,
        application_name character varying(200) NULL,
        co_applicant_user_id character varying(450) NULL,
        co_applicant_user_name character varying(256) NULL,
        has_co_applicants boolean NOT NULL,
        social_security_number character varying(128) NULL,
        documents_requested boolean NOT NULL,
        home_price_min numeric(18,2),
        home_price_max numeric(18,2),
        status character varying(32) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        submitted_at timestamp with time zone NULL
    );

    CREATE INDEX IF NOT EXISTS ix_mortgage_applications_user_id_created_at
        ON mortgage.applications (user_id, created_at);

    CREATE TABLE IF NOT EXISTS mortgage.assets (
        id uuid PRIMARY KEY,
        mortgage_application_id uuid NOT NULL REFERENCES mortgage.applications (id) ON DELETE CASCADE,
        name character varying(200) NOT NULL,
        amount character varying(100) NOT NULL,
        sort_order integer NOT NULL
    );

    CREATE TABLE IF NOT EXISTS mortgage.incomes (
        id uuid PRIMARY KEY,
        mortgage_application_id uuid NOT NULL REFERENCES mortgage.applications (id) ON DELETE CASCADE,
        name character varying(200) NOT NULL,
        income_type character varying(100) NOT NULL,
        amount character varying(100) NOT NULL,
        sort_order integer NOT NULL
    );

    CREATE TABLE IF NOT EXISTS mortgage.debts (
        id uuid PRIMARY KEY,
        mortgage_application_id uuid NOT NULL REFERENCES mortgage.applications (id) ON DELETE CASCADE,
        name character varying(200) NOT NULL,
        amount numeric(18,2) NOT NULL,
        sort_order integer NOT NULL
    );

    CREATE TABLE IF NOT EXISTS mortgage.evaluations (
        mortgage_application_id uuid PRIMARY KEY REFERENCES mortgage.applications (id) ON DELETE CASCADE,
        approved boolean NOT NULL,
        reason character varying(500) NOT NULL,
        evaluated_at timestamp with time zone NOT NULL
    );

    CREATE TABLE IF NOT EXISTS api.messages (
        "Id" uuid PRIMARY KEY,
        "UserId" character varying(128) NOT NULL,
        "Text" character varying(500) NOT NULL,
        "CreatedUtc" timestamp with time zone NOT NULL,
        "ProcessedUtc" timestamp with time zone NULL
    );

    CREATE TABLE IF NOT EXISTS docrender.rendered_mortgages (
        mortgage_application_id uuid PRIMARY KEY,
        user_id character varying(450) NOT NULL,
        pdf_bytes bytea NOT NULL,
        source_submitted_at timestamp with time zone NOT NULL,
        rendered_at timestamp with time zone NOT NULL
    );
    """);
