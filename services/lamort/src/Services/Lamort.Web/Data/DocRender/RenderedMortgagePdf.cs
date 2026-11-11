namespace Lamort.Web.Data.DocRender;

public sealed class RenderedMortgagePdf
{
    public Guid MortgageApplicationId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public byte[] PdfBytes { get; set; } = [];

    public DateTimeOffset SourceSubmittedAt { get; set; }

    public DateTimeOffset RenderedAt { get; set; }
}
