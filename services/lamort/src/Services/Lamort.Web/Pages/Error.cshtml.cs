using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;

namespace Lamort.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    private readonly ILogger<ErrorModel> _logger;
    private readonly IHostEnvironment _environment;

    public string? RequestId { get; set; }

    public string? ExceptionDetails { get; private set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public bool ShowExceptionDetails => !string.IsNullOrEmpty(ExceptionDetails);

    public ErrorModel(ILogger<ErrorModel> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

        if (_environment.IsDevelopment() && exceptionFeature?.Error is not null)
        {
            ExceptionDetails = exceptionFeature.Error.ToString();
        }
    }
}
