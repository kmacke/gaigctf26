using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Lamort.DocRender.Data;
using Lamort.Shared.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Lamort.DocRender.Services;

public sealed class MortgagePdfRenderer(
    IServiceScopeFactory scopeFactory,
    ILogger<MortgagePdfRenderer> logger)
{
    public async Task RenderAsync(MortgageApplicationReadyForReviewEvent message, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocRenderDbContext>();

        var snapshot = await LoadSnapshotAsync(dbContext, message, cancellationToken);
        if (snapshot is null)
        {
            logger.LogWarning("Mortgage {MortgageApplicationId} was not found for rendering.", message.MortgageApplicationId);
            return;
        }

        var pdfBytes = await RenderPdfBytesAsync(snapshot, cancellationToken);
        var existing = await dbContext.RenderedMortgagePdfs.FindAsync([message.MortgageApplicationId], cancellationToken);
        if (existing is null)
        {
            dbContext.RenderedMortgagePdfs.Add(new RenderedMortgagePdf
            {
                MortgageApplicationId = message.MortgageApplicationId,
                UserId = snapshot.UserId,
                PdfBytes = pdfBytes,
                SourceSubmittedAt = message.SubmittedAt,
                RenderedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.UserId = snapshot.UserId;
            existing.PdfBytes = pdfBytes;
            existing.SourceSubmittedAt = message.SubmittedAt;
            existing.RenderedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<MortgageSnapshot?> LoadSnapshotAsync(
        DocRenderDbContext dbContext,
        MortgageApplicationReadyForReviewEvent message,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var applicationCommand = connection.CreateCommand();
        applicationCommand.CommandText =
            """
            SELECT user_id, application_name, has_co_applicants, social_security_number, home_price_min, home_price_max, submitted_at
            FROM mortgage.applications
            WHERE id = @id AND status = 'ReadyForReview'
            """;
        AddParameter(applicationCommand, "@id", message.MortgageApplicationId);

        await using var reader = await applicationCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var snapshot = new MortgageSnapshot
        {
            MortgageApplicationId = message.MortgageApplicationId,
            UserId = reader.GetString(0),
            ApplicationName = reader.IsDBNull(1) ? null : reader.GetString(1),
            HasCoApplicants = reader.GetBoolean(2),
            SocialSecurityNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
            HomePriceMin = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
            HomePriceMax = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            SubmittedAt = reader.IsDBNull(6) ? message.SubmittedAt : reader.GetFieldValue<DateTimeOffset>(6)
        };

        await reader.CloseAsync();
        snapshot.Assets.AddRange(await LoadLinesAsync(connection, message.MortgageApplicationId, "assets", "name", cancellationToken));
        snapshot.Incomes.AddRange(await LoadLinesAsync(connection, message.MortgageApplicationId, "incomes", "name, income_type", cancellationToken));
        snapshot.Debts.AddRange(await LoadLinesAsync(connection, message.MortgageApplicationId, "debts", "name", cancellationToken));
        return snapshot;
    }

    private static async Task<List<MortgageLine>> LoadLinesAsync(
        DbConnection connection,
        Guid mortgageApplicationId,
        string tableName,
        string fields,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {fields}, amount
            FROM mortgage.{tableName}
            WHERE mortgage_application_id = @mortgageApplicationId
            ORDER BY sort_order, name
            """;
        AddParameter(command, "@mortgageApplicationId", mortgageApplicationId);

        var lines = new List<MortgageLine>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (fields == "name, income_type")
            {
                lines.Add(new MortgageLine($"{reader.GetString(0)} ({reader.GetString(1)})", ReadAmount(tableName, reader, 2)));
            }
            else
            {
                lines.Add(new MortgageLine(reader.GetString(0), ReadAmount(tableName, reader, 1)));
            }
        }

        return lines;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<byte[]> RenderPdfBytesAsync(MortgageSnapshot snapshot, CancellationToken cancellationToken)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "lamort-docrender", snapshot.MortgageApplicationId.ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        var postScriptPath = Path.Combine(workingDirectory, "mortgage.ps");
        var pdfPath = Path.Combine(workingDirectory, "mortgage.pdf");

        await File.WriteAllTextAsync(postScriptPath, BuildPostScript(snapshot), Encoding.ASCII, cancellationToken);

        var documentTitle = ResolveApplicationName(snapshot);
        var command = string.Create(
            CultureInfo.InvariantCulture,
            $"gs -dBATCH -dNOPAUSE -sDEVICE=pdfwrite -sTitle={documentTitle} -sOutputFile={pdfPath} {postScriptPath}");
        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(command);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start Ghostscript.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Ghostscript failed with exit code {process.ExitCode}: {error}");
        }

        return await File.ReadAllBytesAsync(pdfPath, cancellationToken);
    }

    private static string BuildPostScript(MortgageSnapshot snapshot)
    {
        var contentLines = new List<string>
        {
            ResolveApplicationName(snapshot),
            $"Submitted {snapshot.SubmittedAt:MMM d, yyyy h:mm tt 'UTC'}",
            $"Co-applicants: {(snapshot.HasCoApplicants ? "Yes" : "No")}",
            $"Social Security number: {FormatText(snapshot.SocialSecurityNumber)}",
            $"Home price range: {FormatRange(snapshot.HomePriceMin, snapshot.HomePriceMax)}"
        };

        AppendSection(contentLines, "Assets", snapshot.Assets);
        AppendSection(contentLines, "Income", snapshot.Incomes);
        AppendSection(contentLines, "Debts", snapshot.Debts);

        var postScript = new List<string>
        {
            "%!PS-Adobe-3.0",
            "/Helvetica findfont 12 scalefont setfont"
        };

        var y = 760;
        foreach (var line in contentLines)
        {
            postScript.Add($"72 {y} moveto ({line}) show");
            y -= 18;
        }

        postScript.Add("showpage");
        return string.Join('\n', postScript);
    }

    private static void AppendSection(List<string> lines, string title, IReadOnlyList<MortgageLine> items)
    {
        lines.Add(string.Empty);
        lines.Add(title);

        if (items.Count == 0)
        {
            lines.Add("None provided");
            return;
        }

        foreach (var item in items)
        {
            lines.Add($"{item.Name}: {item.Amount}");
        }
    }

    private static string FormatRange(decimal? min, decimal? max)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        if (min.HasValue && max.HasValue)
        {
            return $"{min.Value.ToString("C", culture)} to {max.Value.ToString("C", culture)}";
        }

        return "Not specified";
    }

    private static string FormatText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not provided" : value;
    }

    private static string ResolveApplicationName(MortgageSnapshot snapshot)
    {
        return string.IsNullOrWhiteSpace(snapshot.ApplicationName)
            ? "Mortgage application"
            : snapshot.ApplicationName.Trim();
    }

    private static string ReadAmount(string tableName, DbDataReader reader, int ordinal)
    {
        if (tableName == "debts")
        {
            return reader.GetDecimal(ordinal).ToString("C", CultureInfo.GetCultureInfo("en-US"));
        }

        return reader.GetString(ordinal);
    }

    private sealed class MortgageSnapshot
    {
        public Guid MortgageApplicationId { get; init; }

        public string UserId { get; init; } = string.Empty;

        public string? ApplicationName { get; init; }

        public bool HasCoApplicants { get; init; }

        public string? SocialSecurityNumber { get; init; }

        public decimal? HomePriceMin { get; init; }

        public decimal? HomePriceMax { get; init; }

        public DateTimeOffset SubmittedAt { get; init; }

        public List<MortgageLine> Assets { get; } = [];

        public List<MortgageLine> Incomes { get; } = [];

        public List<MortgageLine> Debts { get; } = [];
    }

    private sealed record MortgageLine(string Name, string Amount);
}
