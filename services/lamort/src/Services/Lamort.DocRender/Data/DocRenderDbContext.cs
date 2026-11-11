using Microsoft.EntityFrameworkCore;

namespace Lamort.DocRender.Data;

public sealed class DocRenderDbContext(DbContextOptions<DocRenderDbContext> options) : DbContext(options)
{
    public DbSet<RenderedMortgagePdf> RenderedMortgagePdfs => Set<RenderedMortgagePdf>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RenderedMortgagePdf>(entity =>
        {
            entity.ToTable("rendered_mortgages", "docrender");
            entity.HasKey(x => x.MortgageApplicationId);
            entity.Property(x => x.MortgageApplicationId).HasColumnName("mortgage_application_id");
            entity.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(450);
            entity.Property(x => x.PdfBytes).HasColumnName("pdf_bytes");
            entity.Property(x => x.SourceSubmittedAt).HasColumnName("source_submitted_at");
            entity.Property(x => x.RenderedAt).HasColumnName("rendered_at");
        });
    }
}
