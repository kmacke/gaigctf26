using Lamort.Shared.Auth;
using Lamort.Web.Data.DocRender;
using Lamort.Web.Data.Mortgages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lamort.Web.Data;

public sealed class WebDbContext(DbContextOptions<WebDbContext> options)
    : IdentityDbContext<LamortUser, IdentityRole, string>(options)
{
    public DbSet<MortgageApplication> MortgageApplications => Set<MortgageApplication>();

    public DbSet<MortgageAsset> MortgageAssets => Set<MortgageAsset>();

    public DbSet<MortgageIncome> MortgageIncomes => Set<MortgageIncome>();

    public DbSet<MortgageDebt> MortgageDebts => Set<MortgageDebt>();

    public DbSet<MortgageEvaluation> MortgageEvaluations => Set<MortgageEvaluation>();

    public DbSet<RenderedMortgagePdf> RenderedMortgagePdfs => Set<RenderedMortgagePdf>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<LamortUser>().ToTable("users", "identity");
        builder.Entity<IdentityRole>().ToTable("roles", "identity");
        builder.Entity<IdentityUserRole<string>>().ToTable("user_roles", "identity");
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims", "identity");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins", "identity");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens", "identity");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims", "identity");

        builder.Entity<MortgageApplication>(application =>
        {
            application.ToTable("applications", "mortgage");
            application.HasKey(x => x.Id);
            application.Property(x => x.Id).HasColumnName("id");
            application.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(450);
            application.Property(x => x.ApplicationName).HasColumnName("application_name").HasMaxLength(200);
            application.Property(x => x.CoApplicantUserId).HasColumnName("co_applicant_user_id").HasMaxLength(450);
            application.Property(x => x.CoApplicantUserName).HasColumnName("co_applicant_user_name").HasMaxLength(256);
            application.Property(x => x.HasCoApplicants).HasColumnName("has_co_applicants");
            application.Property(x => x.SocialSecurityNumber).HasColumnName("social_security_number").HasMaxLength(128);
            application.Property(x => x.DocumentsRequested).HasColumnName("documents_requested");
            application.Property(x => x.HomePriceMin).HasColumnName("home_price_min").HasPrecision(18, 2);
            application.Property(x => x.HomePriceMax).HasColumnName("home_price_max").HasPrecision(18, 2);
            application.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
            application.Property(x => x.CreatedAt).HasColumnName("created_at");
            application.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            application.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            application.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        builder.Entity<MortgageAsset>(asset =>
        {
            asset.ToTable("assets", "mortgage");
            asset.HasKey(x => x.Id);
            asset.Property(x => x.Id).HasColumnName("id");
            asset.Property(x => x.MortgageApplicationId).HasColumnName("mortgage_application_id");
            asset.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            asset.Property(x => x.Amount).HasColumnName("amount").HasMaxLength(100);
            asset.Property(x => x.SortOrder).HasColumnName("sort_order");
            asset.HasOne(x => x.MortgageApplication)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.MortgageApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MortgageIncome>(income =>
        {
            income.ToTable("incomes", "mortgage");
            income.HasKey(x => x.Id);
            income.Property(x => x.Id).HasColumnName("id");
            income.Property(x => x.MortgageApplicationId).HasColumnName("mortgage_application_id");
            income.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            income.Property(x => x.IncomeType).HasColumnName("income_type").HasMaxLength(100);
            income.Property(x => x.Amount).HasColumnName("amount").HasMaxLength(100);
            income.Property(x => x.SortOrder).HasColumnName("sort_order");
            income.HasOne(x => x.MortgageApplication)
                .WithMany(x => x.Incomes)
                .HasForeignKey(x => x.MortgageApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MortgageDebt>(debt =>
        {
            debt.ToTable("debts", "mortgage");
            debt.HasKey(x => x.Id);
            debt.Property(x => x.Id).HasColumnName("id");
            debt.Property(x => x.MortgageApplicationId).HasColumnName("mortgage_application_id");
            debt.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            debt.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
            debt.Property(x => x.SortOrder).HasColumnName("sort_order");
            debt.HasOne(x => x.MortgageApplication)
                .WithMany(x => x.Debts)
                .HasForeignKey(x => x.MortgageApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MortgageEvaluation>(evaluation =>
        {
            evaluation.ToTable("evaluations", "mortgage");
            evaluation.HasKey(x => x.MortgageApplicationId);
            evaluation.Property(x => x.MortgageApplicationId).HasColumnName("mortgage_application_id");
            evaluation.Property(x => x.Approved).HasColumnName("approved");
            evaluation.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
            evaluation.Property(x => x.EvaluatedAt).HasColumnName("evaluated_at");
        });

        builder.Entity<RenderedMortgagePdf>(pdf =>
        {
            pdf.ToTable("rendered_mortgages", "docrender");
            pdf.HasKey(x => x.MortgageApplicationId);
            pdf.Property(x => x.MortgageApplicationId).HasColumnName("mortgage_application_id");
            pdf.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(450);
            pdf.Property(x => x.PdfBytes).HasColumnName("pdf_bytes");
            pdf.Property(x => x.SourceSubmittedAt).HasColumnName("source_submitted_at");
            pdf.Property(x => x.RenderedAt).HasColumnName("rendered_at");
        });
    }
}
