using System.Security.Claims;
using System.Text;
using Lamort.DocRender.Data;
using Lamort.DocRender.Services;
using Lamort.Shared.Auth;
using Lamort.Shared.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value);
builder.Services.AddSingleton<MortgagePdfRenderer>();
builder.Services.AddHostedService<MortgageReadyForReviewConsumer>();

builder.Services.AddDbContext<DocRenderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("LamortDb")));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
jwtSettings.EnsureValid();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // SECURITY: tokens are accepted from the Authorization header only, never the URL.
    });

builder.Services.AddAuthorization();
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/mortgages/{id:guid}/pdf", async (
    Guid id,
    HttpContext httpContext,
    ClaimsPrincipal user,
    DocRenderDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Unauthorized();
    }

    var pdf = await dbContext.RenderedMortgagePdfs
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.MortgageApplicationId == id && x.UserId == userId, cancellationToken);
    if (pdf is null)
    {
        return Results.NotFound();
    }

    httpContext.Response.Headers["Cache-Control"] = "no-store";
    httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
    httpContext.Response.Headers["Content-Disposition"] = $"inline; filename=\"mortgage-{id:N}.pdf\"";
    return Results.File(
        pdf.PdfBytes,
        "application/pdf",
        enableRangeProcessing: false,
        lastModified: pdf.RenderedAt,
        entityTag: null);
}).RequireAuthorization();

app.Run();
