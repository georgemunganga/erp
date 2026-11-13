using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
namespace Mightyfin.Erp.Hrm.Infrastructure.Data;

public sealed partial class HrmDbContext
{
    public DbSet<CoreProductSession> ProductSessions => Set<CoreProductSession>();
    public DbSet<CoreProductSessionLocation> ProductSessionLocations => Set<CoreProductSessionLocation>();
    public DbSet<CoreProductSessionRevocation> ProductSessionRevocations => Set<CoreProductSessionRevocation>();
    private void ConfigureProductSessions(ModelBuilder model)
    {
        model.Entity<ExternalIdentityLink>().HasAlternateKey(x => new { x.TenantId, x.LocalUserId, x.Id });
        ConfigureEntity<CoreProductSession>(model, "core_product_sessions", e => {
            e.Property(x => x.TenantId).ValueGeneratedOnAdd().HasValueGenerator((_, _) => new ScopedTenantKeyGenerator());
            e.Property(x => x.Issuer).HasMaxLength(200); e.Property(x => x.Subject).HasMaxLength(19); e.Property(x => x.ClientId).HasMaxLength(36);
            // UTC ticks preserve 100ns precision across both providers, including
            // the exact deadlines authenticated by the protected token.
            e.Property(x => x.IssuedAt).HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
            e.Property(x => x.ExpiresAt).HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
            e.Property(x => x.ModuleCode).HasMaxLength(16); e.Property(x => x.ScopeKind).HasMaxLength(12);
            e.Property(x => x.ScopeHash).HasMaxLength(64); e.Property(x => x.TokenHash).HasMaxLength(64); e.Property(x => x.ProtectedToken).HasMaxLength(32768);
            e.Property(x => x.CredentialHash).HasMaxLength(64); e.Property(x => x.BrowserHash).HasMaxLength(64); e.Property(x => x.RequestHash).HasMaxLength(64);
            e.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id });
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.MembershipId, x.ModuleCode, x.GrantVersionId, x.GrantRevision })
                .HasDatabaseName("IX_product_sessions_grant_binding");
            e.HasIndex(x => new { x.Issuer, x.Subject, x.ClientId, x.CommandKey }).IsUnique();
            e.HasIndex(x => new { x.Issuer, x.Subject, x.ClientId, x.IssuedAt });
            e.HasOne<ExternalIdentityLink>().WithMany().HasForeignKey(x => new { x.TenantId, x.PrincipalId, x.LinkId })
                .HasPrincipalKey(x => new { x.TenantId, x.LocalUserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CoreModuleAccessVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MembershipId, x.ModuleCode, x.GrantVersionId, x.GrantRevision })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.MembershipId, x.ModuleCode, x.Id, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("core_product_sessions", "hrm", t => {
                t.HasCheckConstraint("CK_product_session_policy", "policy_version = 1 AND grant_revision >= 1 AND issuer = 'https://commercial-api.greenwebb.tech' AND NOT is_archived");
                t.HasCheckConstraint("CK_product_session_scope", "module_code IN ('hrm','procurement') AND scope_kind IN ('company','locations')");
                t.HasCheckConstraint("CK_product_session_secrets", "length(scope_hash) = 64 AND length(token_hash) = 64 AND length(credential_hash) = 64 AND length(browser_hash) = 64 AND length(request_hash) = 64 AND length(protected_token) BETWEEN 44 AND 32768");
                t.HasCheckConstraint("CK_product_session_deadline", "expires_at > issued_at AND expires_at - issued_at <= 288000000000 AND issued_at >= 0 AND expires_at <= 3155378975999999999");
            });
        });
        ConfigureEntity<CoreProductSessionLocation>(model, "core_product_session_locations", e => {
            e.Property(x => x.TenantId).ValueGeneratedOnAdd().HasValueGenerator((_, _) => new ScopedTenantKeyGenerator());
            e.HasIndex(x => new { x.SessionId, x.LocationId }).IsUnique();
            e.HasOne<CoreProductSession>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SessionId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<WorkLocation>().WithMany().HasForeignKey(x => new { x.TenantId, x.LocationId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("core_product_session_locations", "hrm", t => t.HasCheckConstraint("CK_product_session_location", "NOT is_archived"));
        });
        ConfigureEntity<CoreProductSessionRevocation>(model, "core_product_session_revocations", e => {
            e.Property(x => x.TenantId).ValueGeneratedOnAdd().HasValueGenerator((_, _) => new ScopedTenantKeyGenerator());
            e.Property(x => x.Reason).HasMaxLength(16); e.HasIndex(x => x.SessionId).IsUnique();
            e.HasOne<CoreProductSession>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SessionId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("core_product_session_revocations", "hrm", t => t.HasCheckConstraint("CK_product_session_revocation", "reason IN ('logout','replaced') AND NOT is_archived"));
        });
    }
}
