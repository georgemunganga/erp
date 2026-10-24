using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ContractReviewTermsTests
{
    [Fact]
    public void Exact_rate_and_header_changes_do_not_reuse_the_retained_submission()
    {
        var tenant = "synthetic-contract-test"; var company = Guid.CreateVersion7();
        var contract = new SupplierContract { TenantId = tenant, LegalEntityId = company,
            Number = "CON-SYNTHETIC", SupplierId = Guid.CreateVersion7(), Title = "Synthetic terms",
            ContractType = "Framework", CurrencyCode = "ZMW", CeilingAmount = 99999999999999.0001m,
            OwnerSubjectId = Guid.CreateVersion7().ToString("D"), Status = "Draft" };
        var version = new SupplierContractVersion { TenantId = tenant, LegalEntityId = company,
            SupplierContractId = contract.Id, RevisionNumber = 1, CeilingAmount = contract.CeilingAmount,
            Status = "Draft" };
        var active = new ContractRate { TenantId = tenant, LegalEntityId = company,
            SupplierContractVersionId = version.Id, LineNumber = 1, Status = "Active",
            Description = "Current exact rate", UnitCode = "EA", UnitPrice = 0.0001m };
        var removed = new ContractRate { TenantId = tenant, LegalEntityId = company,
            SupplierContractVersionId = version.Id, LineNumber = 2, Status = "Removed",
            StatusReason = "Replaced", Description = "Retained history", UnitCode = "EA", UnitPrice = 1m };
        var frozen = ContractReviewTerms.Capture(contract, version, [removed, active]);
        var submission = new ContractReviewSubmission { TenantId = tenant, LegalEntityId = company,
            ContractId = contract.Id, ContractVersionId = version.Id, ContractBusinessVersion = contract.Version,
            ContractVersionBusinessVersion = version.Version, SupplierId = contract.SupplierId,
            CoreCaseId = Guid.CreateVersion7(),
            TermsJson = frozen.Json, TermsSha256 = frozen.Sha256 };
        ContractReviewTerms.VerifyCurrentDraft(submission, contract, version, [active, removed]);
        Assert.Contains("99999999999999.0001", frozen.Json);
        Assert.Contains("0.0001", frozen.Json);

        contract.Status = "PendingReview"; version.Status = "PendingReview";
        contract.Version++; version.Version++;
        version.ApprovalInstanceRef = submission.CoreCaseId.ToString("D");
        ContractReviewTerms.VerifyCurrentPending(submission, contract, version, [active, removed]);

        active.UnitPrice = 0.0002m;
        Assert.Throws<InvalidOperationException>(() => ContractReviewTerms.VerifyCurrentPending(submission, contract, version, [active, removed]));
        active.UnitPrice = 0.0001m;
        removed.StatusReason = "Changed retained line";
        Assert.Throws<InvalidOperationException>(() => ContractReviewTerms.VerifyCurrentPending(submission, contract, version, [active, removed]));
    }

    [Fact]
    public void Missing_or_foreign_or_duplicate_rate_does_not_form_a_submission()
    {
        var contract = new SupplierContract { TenantId = "synthetic", LegalEntityId = Guid.CreateVersion7(),
            SupplierId = Guid.CreateVersion7(), OwnerSubjectId = Guid.CreateVersion7().ToString("D"),
            Status = "Draft" };
        var version = new SupplierContractVersion { TenantId = contract.TenantId,
            LegalEntityId = contract.LegalEntityId, SupplierContractId = contract.Id,
            RevisionNumber = 1, Status = "Draft" };
        Assert.Throws<InvalidOperationException>(() => ContractReviewTerms.Capture(contract, version, []));
        var foreign = new ContractRate { TenantId = contract.TenantId, LegalEntityId = contract.LegalEntityId,
            SupplierContractVersionId = Guid.CreateVersion7(), LineNumber = 1, Status = "Active",
            Description = "Other version", UnitCode = "EA", UnitPrice = 1m };
        Assert.Throws<InvalidOperationException>(() => ContractReviewTerms.Capture(contract, version, [foreign]));
    }
}
