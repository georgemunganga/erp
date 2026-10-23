using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Infrastructure.Data;
public sealed partial class ProcurementDbContext
{
    // Legacy v1 field-array bytes stay unchanged. New principal evidence emits a
    // durable marker so an administrator cannot make a destructive downgrade safe
    // by selectively removing only its owning business row.
    private static string PrincipalAuditFields(ProcurementRecord row, IEnumerable<string> fields)
    {
        var kind = row switch
        {
            OperationalPolicyIdentityRecord { SchemaVersion: 2 } => "OperationalPolicyIdentity",
            InvoiceMatchMappingVersion { IdentitySchemaVersion: 2 } => "InvoiceMappingIdentity",
            InvoiceMatchResult { ExecutionVersion: 2 } => "InvoiceMatchExecution",
            ManualPaymentRequest or ManualPaymentDecision or ManualPaymentCommandReceipt
                or ManualPaymentWorkflowCaseLink or ManualPaymentCoreDecisionLink or ManualPaymentBusinessDateObservation => "ManualPayment",
            PurchaseRequest { RequesterIdentitySchemaVersion: 1 } => "PurchaseRequestOriginalIdentity",
            BudgetCheckSnapshot { CheckerIdentitySchemaVersion: 2 } => "FinanceCheckerIdentity",
            SpendingAuthorityIdentityRecord { SchemaVersion: 2 } => "SpendingAuthorityIdentity",
            PurchaseRequestApprovalSubmission { SnapshotSchemaVersion: 3 } => "PurchaseRequestApprovalSubmission",
            PurchaseRequestAssignmentAuthorityEvidence { IdentitySchemaVersion: 2 } => "PurchaseRequestAssignmentAuthority",
            _ => null,
        };
        return kind is null ? JsonSerializer.Serialize(fields) : JsonSerializer.Serialize(new
        { fields, principalEvidenceVersion = 2, principalEvidenceKind = kind });
    }
}
