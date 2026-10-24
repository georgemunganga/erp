namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Authored P32 migration companion, not installed or a readiness claim.
/// PostgreSQL can reference the managed nonpartial unique index despite legacy
/// nullable bid.invitation_id. New offer.invitation_id is required and can only
/// match one exact retained bid invitation; no legacy column is altered.</summary>
public static class PrincipalRfqCommercialOfferInvitationOwnershipSql
{
    public const string Install = """
        ALTER TABLE procurement.principal_rfq_commercial_offer_submissions
          ADD CONSTRAINT fk_p32_offer_bid_invitation
          FOREIGN KEY (tenant_id,legal_entity_id,supplier_bid_id,sourcing_event_id,supplier_id,invitation_id)
          REFERENCES procurement.supplier_bids
            (tenant_id,legal_entity_id,id,sourcing_event_id,supplier_id,invitation_id)
          ON DELETE NO ACTION;
        """;
}
