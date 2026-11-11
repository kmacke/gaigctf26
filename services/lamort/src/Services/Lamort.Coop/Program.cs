using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "lamort-web";
var jwtKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 bytes long.");
}

var tokenValidation = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = jwtIssuer,
    ValidateAudience = true,
    ValidAudience = "lamort-coop",
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
    ClockSkew = TimeSpan.FromSeconds(30)
};

// Returns the authenticated user name from a signed Web-issued token, or null if invalid/expired.
string? ValidateToken(string? token)
{
    if (string.IsNullOrWhiteSpace(token))
    {
        return null;
    }

    try
    {
        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }
            .ValidateToken(token.Trim(), tokenValidation, out _);
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? principal.FindFirstValue("unique_name") ?? principal.FindFirstValue("name");
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
    catch (Exception)
    {
        return null;
    }
}

var connectionString = builder.Configuration.GetConnectionString("LamortDb")
    ?? throw new InvalidOperationException("Connection string 'LamortDb' is required.");

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Cache-Control"] = "no-store";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'";
    await next();
});

app.MapGet("/application", async (Guid? id, string? token, CancellationToken cancellationToken) =>
{
    static string MaskSsn(string ssn)
    {
        var digits = new string(ssn.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? "***-**-" + digits[^4..] : "***";
    }

    static string FormatPriceRange(decimal? min, decimal? max)
    {
        if (min is null && max is null)
        {
            return "Not provided";
        }

        if (min is not null && max is not null)
        {
            return $"{min.Value:0.##} to {max.Value:0.##}";
        }

        if (min is not null)
        {
            return $"{min.Value:0.##}+";
        }

        return $"Up to {max!.Value:0.##}";
    }

    var authenticatedUser = ValidateToken(token);
    if (authenticatedUser is null)
    {
        return Results.Unauthorized();
    }

    if (id is null)
    {
        return Results.BadRequest("Missing id");
    }

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        SELECT
            a.id,
            a.application_name,
            a.co_applicant_user_name,
            a.has_co_applicants,
            a.social_security_number,
            a.documents_requested,
            a.home_price_min,
            a.home_price_max,
            a.status,
            a.created_at,
            a.submitted_at
        FROM mortgage.applications a
        LEFT JOIN identity.users owner ON owner."Id" = a.user_id
        WHERE a.id = @id
            AND (a.co_applicant_user_name = @username OR owner."UserName" = @username)
        """;
    command.Parameters.AddWithValue("@id", id.Value);
    command.Parameters.AddWithValue("@username", authenticatedUser);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
    {
        return Results.NotFound("Application not found");
    }

    var applicationId = reader.GetGuid(0);
    var title = reader.IsDBNull(1) ? "Mortgage application" : reader.GetString(1);
    var coApplicantUserName = reader.IsDBNull(2) ? "None" : reader.GetString(2);
    var hasCoApplicants = reader.GetBoolean(3) ? "Yes" : "No";
    var socialSecurityNumber = reader.IsDBNull(4) ? "Not provided" : MaskSsn(reader.GetString(4));
    var documentsRequested = reader.GetBoolean(5) ? "Yes" : "No";
    var homePriceRange = FormatPriceRange(
        reader.IsDBNull(6) ? null : reader.GetDecimal(6),
        reader.IsDBNull(7) ? null : reader.GetDecimal(7));
    var status = reader.GetString(8);
    var createdAt = reader.GetFieldValue<DateTimeOffset>(9).ToString("yyyy-MM-dd HH:mm 'UTC'");
    var submittedAt = reader.IsDBNull(10)
        ? "Not submitted"
        : reader.GetFieldValue<DateTimeOffset>(10).ToString("yyyy-MM-dd HH:mm 'UTC'");

    var html =
        $"""
        <!doctype html>
        <html>
        <body>
            <h1>{WebUtility.HtmlEncode(title)}</h1>
            <table border="1" cellpadding="8" cellspacing="0">
                <tbody>
                    <tr>
                        <th align="left">Application ID</th>
                        <td>{WebUtility.HtmlEncode(applicationId.ToString())}</td>
                    </tr>
                    <tr>
                        <th align="left">Application Name</th>
                        <td>{WebUtility.HtmlEncode(title)}</td>
                    </tr>
                    <tr>
                        <th align="left">Status</th>
                        <td>{WebUtility.HtmlEncode(status)}</td>
                    </tr>
                    <tr>
                        <th align="left">Co-Applicant</th>
                        <td>{WebUtility.HtmlEncode(coApplicantUserName)}</td>
                    </tr>
                    <tr>
                        <th align="left">Has Co-Applicants</th>
                        <td>{WebUtility.HtmlEncode(hasCoApplicants)}</td>
                    </tr>
                    <tr>
                        <th align="left">Social Security Number</th>
                        <td>{WebUtility.HtmlEncode(socialSecurityNumber)}</td>
                    </tr>
                    <tr>
                        <th align="left">Documents Requested</th>
                        <td>{WebUtility.HtmlEncode(documentsRequested)}</td>
                    </tr>
                    <tr>
                        <th align="left">Home Price Range</th>
                        <td>{WebUtility.HtmlEncode(homePriceRange)}</td>
                    </tr>
                    <tr>
                        <th align="left">Created</th>
                        <td>{WebUtility.HtmlEncode(createdAt)}</td>
                    </tr>
                    <tr>
                        <th align="left">Submitted</th>
                        <td>{WebUtility.HtmlEncode(submittedAt)}</td>
                    </tr>
                </tbody>
            </table>
        </body>
        </html>
        """;

    return Results.Content(html, "text/html");
});

app.MapGet("/", async (string? token, CancellationToken cancellationToken) =>
{
    var safeToken = ValidateToken(token);
    if (string.IsNullOrWhiteSpace(safeToken))
    {
        return Results.Content(
            """
            <!doctype html>
            <html>
            <body>
                <h1>Lamort.Coop</h1>
                <p>?</p>
            </body>
            </html>
            """,
            "text/html");
    }

    var applications = new List<SharedApplicationRow>();

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        SELECT a.id, a.application_name, owner."UserName", a.status, a.created_at, a.submitted_at
        FROM mortgage.applications a
        LEFT JOIN identity.users owner ON owner."Id" = a.user_id
        WHERE a.co_applicant_user_name = @username
            OR owner."UserName" = @username
        ORDER BY a.created_at DESC
        """;
    command.Parameters.AddWithValue("@username", safeToken);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        applications.Add(new SharedApplicationRow(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? "Mortgage application" : reader.GetString(1),
            reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5)));
    }

    var rows = applications.Count == 0
        ? "<p>No shared applications were found.</p>"
        : string.Join(
            "",
            applications.Select(application =>
                $"""
                <tr>
                    <td>{WebUtility.HtmlEncode(application.ApplicationName)}</td>
                    <td>{WebUtility.HtmlEncode(application.OwnerUserName)}</td>
                    <td>{WebUtility.HtmlEncode(application.Status)}</td>
                    <td>{WebUtility.HtmlEncode(application.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"))}</td>
                    <td>{WebUtility.HtmlEncode(application.SubmittedAt?.ToString("yyyy-MM-dd HH:mm 'UTC'") ?? "Not submitted")}</td>
                    <td><a href="/coop/application?id={application.Id}&token={Uri.EscapeDataString(token!)}">show</a></td>
                </tr>
                """));

    var html =
        $"""
        <!doctype html>
        <html>
        <body>
            <h1>Lamort.Coop</h1>
            <p>Shared applications for {WebUtility.HtmlEncode(safeToken)}</p>
            <table border="1" cellpadding="8" cellspacing="0">
                <thead>
                    <tr>
                        <th>Application</th>
                        <th>Owner</th>
                        <th>Status</th>
                        <th>Created</th>
                        <th>Submitted</th>
                        <th>Id</th>
                    </tr>
                </thead>
                <tbody>
                    {rows}
                </tbody>
            </table>
        </body>
        </html>
        """;

    return Results.Content(html, "text/html");
});

app.Run();

internal sealed record SharedApplicationRow(
    Guid Id,
    string ApplicationName,
    string OwnerUserName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt);
