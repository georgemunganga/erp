ALTER TABLE procurement.receipt_inspections
  ADD CONSTRAINT fk_receiving_inspection_exact_physical
  FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, receiving_source_state_id, physical_observation_id)
  REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, source_state_binding_id, physical_binding_id)
  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE procurement.receiving_acceptance_effects
  ADD CONSTRAINT fk_receiving_effect_exact_physical
  FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, receiving_source_state_id, physical_observation_id)
  REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, source_state_binding_id, physical_binding_id)
  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE procurement.receiving_acceptance_effects
  ADD CONSTRAINT fk_receiving_effect_exact_service
  FOREIGN KEY (tenant_id, legal_entity_id, service_acceptance_id, receiving_source_state_id)
  REFERENCES procurement.service_acceptances (tenant_id, legal_entity_id, id, source_state_binding_id)
  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE procurement.invoice_match_allocations
  ADD CONSTRAINT fk_invoice_allocation_exact_goods
  FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, purchase_order_line_id, receiving_source_state_id)
  REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, purchase_order_line_id, source_state_binding_id)
  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE procurement.invoice_match_allocations
  ADD CONSTRAINT fk_invoice_allocation_exact_service
  FOREIGN KEY (tenant_id, legal_entity_id, service_acceptance_id, purchase_order_line_id, receiving_source_state_id)
  REFERENCES procurement.service_acceptances (tenant_id, legal_entity_id, id, purchase_order_line_id, source_state_binding_id)
  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE procurement.sourcing_events ADD CONSTRAINT fk_rfq_event_original
  FOREIGN KEY (tenant_id,legal_entity_id,source_evidence_id,id)
  REFERENCES procurement.sourcing_draft_evidence (tenant_id,legal_entity_id,id,sourcing_event_id)
  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_original
  FOREIGN KEY (tenant_id,legal_entity_id,source_evidence_id,sourcing_event_id)
  REFERENCES procurement.sourcing_draft_evidence (tenant_id,legal_entity_id,id,sourcing_event_id)
  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_exact_line
  FOREIGN KEY (tenant_id,legal_entity_id,sourcing_line_id,sourcing_event_id,purchase_request_line_id)
  REFERENCES procurement.sourcing_lines (tenant_id,legal_entity_id,id,sourcing_event_id,purchase_request_line_id)
  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_exact_hold
  FOREIGN KEY (tenant_id,legal_entity_id,allocation_id,purchase_request_id,purchase_request_line_id,target_type,sourcing_event_id)
  REFERENCES procurement.purchase_request_allocations (tenant_id,legal_entity_id,id,purchase_request_id,purchase_request_line_id,target_type,target_id)
  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
