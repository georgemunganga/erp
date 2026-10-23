using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private sealed record RfqRawFault(string Code,string Table,Guid Id,string Assignment,
        string SqlState,string? Constraint=null,string? Column=null);

    // Owner-only PostgreSQL constraint checks. The normal HTTP/finite-role cohort
    // separately proves authorization and workflow; these updates prove neither.
    private static async Task<object> VerifyPrincipalRfqRawStorageAsync(NpgsqlConnection owner,
        PrincipalApprovalFixture fixture,Guid eventId,Guid createKey)
    {
        await RequirePrivateRawOwnerAsync(owner,PrincipalApprovalFixture.Tenant,fixture.Company);
        await using var db=fixture.Procurement();
        var evidence=await db.SourcingDraftEvidenceRecords.SingleAsync(x=>x.SourcingEventId==eventId);
        var binding=await db.SourcingDemandBindings.SingleAsync(x=>x.SourcingEventId==eventId);
        var receipt=await db.SourcingCommandReceipts.SingleAsync(x=>x.SourcingEventId==eventId
            &&x.Operation=="Create"&&x.CommandKey==createKey);
        RawNeed(evidence.SchemaVersion==2&&binding.IdentitySchemaVersion==2&&receipt.ResponseSchemaVersion==2
            &&evidence.MakerPrincipalId is not null&&binding.MakerPrincipalId is not null
            &&binding.RequesterPrincipalId is not null&&receipt.ActorPrincipalId is not null,
            "Actual current principal RFQ draft and Create receipt are required for owner constraint checks.");
        const string zero="'00000000-0000-0000-0000-000000000000'";
        RfqRawFault[] faults=
        [
            new("event-method-revision-null","sourcing_events",eventId,"method_revision=NULL","23514","ck_sourcing_owned_draft"),
            new("event-fingerprint-null","sourcing_events",eventId,"original_draft_fingerprint=NULL","23514","ck_sourcing_owned_draft"),
            new("evidence-maker-zero","sourcing_draft_evidence",evidence.Id,"maker_principal_id="+zero,"23514","ck_rfq_original_envelope"),
            new("evidence-maker-null","sourcing_draft_evidence",evidence.Id,"maker_principal_id=NULL","23514","ck_rfq_original_envelope"),
            new("binding-maker-zero","sourcing_demand_bindings",binding.Id,"maker_principal_id="+zero,"23514","ck_rfq_binding_value"),
            new("binding-requester-zero","sourcing_demand_bindings",binding.Id,"requester_principal_id="+zero,"23514","ck_rfq_binding_value"),
            new("binding-maker-null","sourcing_demand_bindings",binding.Id,"maker_principal_id=NULL","23514","ck_rfq_binding_value"),
            new("binding-requester-null","sourcing_demand_bindings",binding.Id,"requester_principal_id=NULL","23514","ck_rfq_binding_value"),
            new("binding-zero-quantity","sourcing_demand_bindings",binding.Id,"quantity=0","23514","ck_rfq_binding_value"),
            new("receipt-actor-zero","sourcing_command_receipts",receipt.Id,"actor_principal_id="+zero,"23514","ck_rfq_receipt_identity"),
            new("receipt-actor-null","sourcing_command_receipts",receipt.Id,"actor_principal_id=NULL","23502",Column:"actor_principal_id"),
        ];
        var before=await RfqRowsAsync(owner,fixture.Company,eventId,evidence.Id,binding.Id,receipt.Id);
        var actualDenials=new List<object>(faults.Length);
        foreach(var fault in faults)
        {
            // Every ID is obtained from the actual mapped v2 draft. The scoped row
            // snapshot makes an absent target a failure, not a vacuous UPDATE pass.
            RawNeed(fault.Id!=Guid.Empty,"Actual scoped RFQ row required.");
            await using(var tx=await owner.BeginTransactionAsync(IsolationLevel.RepeatableRead))
            {
                try
                {
                    try
                    {
                        await using var negative=RawCommand(owner,tx,fault.Table,fault.Assignment,
                            fault.Id,PrincipalApprovalFixture.Tenant,fixture.Company);
                        await negative.ExecuteNonQueryAsync();
                        throw new InvalidOperationException("Expected actual P29 constraint denial: "+fault.Code);
                    }
                    catch(PostgresException error)
                    {
                        RawNeed(error.SqlState==fault.SqlState
                            &&(fault.Constraint is null||error.ConstraintName==fault.Constraint)
                            &&(fault.Column is null||error.ColumnName==fault.Column),
                            "Unexpected P29 constraint/column denial: "+fault.Code);
                        actualDenials.Add(new{code=fault.Code,sqlState=error.SqlState});
                    }
                }
                finally { await tx.RollbackAsync(); }
            }
            RawNeed(before==await RfqRowsAsync(owner,fixture.Company,eventId,evidence.Id,binding.Id,receipt.Id),
                "P29 owner rollback changed original draft evidence: "+fault.Code);
        }
        return new{status="PASS_OWNER_P29_CONSTRAINTS_ONLY",caseCount=faults.Length,
            cases=actualDenials.ToArray(),
            fourRowFootprintUnchangedEveryCase=true,finiteRoleOrRuntimeAuthorizationProved=false};
    }

    private static async Task<string> RfqRowsAsync(NpgsqlConnection owner,Guid company,
        Guid eventId,Guid evidenceId,Guid bindingId,Guid receiptId)
    {
        var rows=new JsonElement[]
        {
            await ReadRawRowAsync(owner,"sourcing_events",eventId,PrincipalApprovalFixture.Tenant,company),
            await ReadRawRowAsync(owner,"sourcing_draft_evidence",evidenceId,PrincipalApprovalFixture.Tenant,company),
            await ReadRawRowAsync(owner,"sourcing_demand_bindings",bindingId,PrincipalApprovalFixture.Tenant,company),
            await ReadRawRowAsync(owner,"sourcing_command_receipts",receiptId,PrincipalApprovalFixture.Tenant,company),
        };
        return string.Join("\n",rows.Select(x=>x.GetRawText()));
    }
}
