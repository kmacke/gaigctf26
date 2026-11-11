using System.Text;
using Lamort.Shared.Auth;
using Lamort.Shared.Messaging;
using Lamort.Web.Data;
using Lamort.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value);
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

builder.Services.AddDbContext<WebDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("LamortDb")));

builder.Services
    .AddIdentity<LamortUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 0;
        options.Password.RequiredUniqueChars = 0;
    })
    .AddEntityFrameworkStores<WebDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
});

builder.Services.AddAuthorization();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/App");
    options.Conventions.AllowAnonymousToFolder("/Account");
});

builder.Services.AddScoped<JwtTokenService>();

var app = builder.Build();

app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/auth/token", async (
    HttpContext httpContext,
    UserManager<LamortUser> userManager,
    JwtTokenService tokenService) =>
{
    if (httpContext.User.Identity?.IsAuthenticated != true)
    {
        return Results.Unauthorized();
    }

    var user = await userManager.GetUserAsync(httpContext.User);
    return user is null ? Results.Unauthorized() : Results.Ok(tokenService.CreateToken(user));
}).RequireAuthorization();

app.MapRazorPages();

app.Run();
