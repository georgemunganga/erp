"""Pure service owner command and evidence guards; no database or network."""
import hashlib
import json
import unittest
from verify_p30_po_cookie_owner import Failed, canonical_json
from verify_p30_service_cookie_owner import (fingerprints, json_object,
    service_canonical_json, service_line_acceptance, unique_json)

ORDER='11111111-1111-4111-8111-111111111111'
ISSUED='22222222-2222-4222-8222-222222222222'
LINE='33333333-3333-4333-8333-333333333333'
SERVICE='44444444-4444-4444-8444-444444444444'
CREATE={'purchaseOrderId':ORDER,'issuedVersionId':ISSUED,'purchaseOrderLineId':LINE,
    'deliverableReference':'PRIVATE_BROWSER_SERVICE_HALF','periodFrom':'2026-10-24',
    'periodTo':'2026-10-24','quantity':'0.5','notes':None}
CERTIFY={'version':1,'sourceVersion':0,'quantity':'0.5',
    'evidenceReference':'PRIVATE-REC:browser-service-half'}

class ServiceOwnerEvidence(unittest.TestCase):
    def test_dotnet_string_escaping_and_jsonb_object_shape(self):
        value={'at':'2026-10-24T10:00:00+00:00','limit':__import__('decimal').Decimal('0.0001')}
        self.assertIn(r'\u002B00:00',service_canonical_json(value))
        self.assertNotEqual(service_canonical_json(value),canonical_json(value))
        self.assertEqual(json_object({'revision':2},'bad'),{'revision':2})
        self.assertEqual(json_object('{"revision":2}','bad'),{'revision':2})
        with self.assertRaises(Failed): json_object(['not-an-object'],'bad')

    def test_half_acceptance_exact_actor_metadata_and_commercial_fields(self):
        actor='55555555-5555-4555-8555-555555555555'
        old={'accepted_quantity':'0','version':1,'updated_by':None,'updated_at':None,
            'created_at':'2026-10-24T10:00:00+00:00','unit_price':'0.0001'}
        after={**old,'accepted_quantity':'0.5','version':2,'updated_by':actor,
            'updated_at':'2026-10-24T10:01:00+00:00'}
        service_line_acceptance(old,after,actor)
        for changed in ({**after,'updated_by':'other'},{**after,'version':3},
            {**after,'updated_at':'2026-10-24T09:59:00+00:00'},
            {**after,'unit_price':'0.0002'},{**after,'accepted_quantity':'1'}):
            with self.subTest(changed=changed), self.assertRaises(Failed):
                service_line_acceptance(old,changed,actor)

    def browser(self,create=None,certify=None):
        return {'createRequestBody':json.dumps(create if create is not None else CREATE),
            'certifyRequestBody':json.dumps(certify if certify is not None else CERTIFY)}

    def test_exact_create_and_certify_original_command_hashes(self):
        create_sha,certify_sha,created,certified=fingerprints(self.browser(),{'issuedOrderId':ORDER,
            'issuedVersionId':ISSUED,'issuedOrderLineId':LINE,'servicePeriodDate':'2026-10-24'},SERVICE)
        self.assertEqual(created,CREATE)
        self.assertEqual(certified,CERTIFY)
        self.assertEqual(create_sha,hashlib.sha256(canonical_json({'operation':'Create',
            'kind':'Service','input':CREATE}).encode()).hexdigest())
        self.assertEqual(certify_sha,hashlib.sha256(canonical_json({'operation':'Certify',
            'id':SERVICE,'input':CERTIFY}).encode()).hexdigest())
        self.assertNotEqual(create_sha,certify_sha)

    def test_changed_source_quantity_or_evidence_denied(self):
        fixture={'issuedOrderId':ORDER,'issuedVersionId':ISSUED,'issuedOrderLineId':LINE,
            'servicePeriodDate':'2026-10-24'}
        for changed in ({**CREATE,'purchaseOrderLineId':SERVICE},
                        {**CREATE,'quantity':'1'}, {**CREATE,'notes':'hidden'}):
            with self.assertRaises(Failed): fingerprints(self.browser(create=changed),fixture,SERVICE)
        with self.assertRaises(Failed): fingerprints(self.browser(certify={**CERTIFY,
            'evidenceReference':'different'}),fixture,SERVICE)

    def test_duplicate_and_extra_fields_denied(self):
        with self.assertRaises(Failed): unique_json('{"version":1,"version":2}',{'version'})
        with self.assertRaises(Failed): unique_json('{"version":1,"extra":2}',{'version'})

if __name__=='__main__': unittest.main()
