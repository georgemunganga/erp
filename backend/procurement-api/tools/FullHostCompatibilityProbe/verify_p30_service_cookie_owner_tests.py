"""Pure service owner command and evidence guards; no database or network."""
import hashlib
import json
import unittest
from verify_p30_po_cookie_owner import Failed, canonical_json
from verify_p30_service_cookie_owner import fingerprints, unique_json

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
    def browser(self,create=None,certify=None):
        return {'createRequestBody':json.dumps(create if create is not None else CREATE),
            'certifyRequestBody':json.dumps(certify if certify is not None else CERTIFY)}

    def test_exact_create_and_certify_original_command_hashes(self):
        create_sha,certify_sha,created,certified=fingerprints(self.browser(),{'issuedOrderId':ORDER,
            'issuedVersionId':ISSUED,'issuedOrderLineId':LINE},SERVICE)
        self.assertEqual(created,CREATE)
        self.assertEqual(certified,CERTIFY)
        self.assertEqual(create_sha,hashlib.sha256(canonical_json({'operation':'Create',
            'kind':'Service','input':CREATE}).encode()).hexdigest())
        self.assertEqual(certify_sha,hashlib.sha256(canonical_json({'operation':'Certify',
            'id':SERVICE,'input':CERTIFY}).encode()).hexdigest())
        self.assertNotEqual(create_sha,certify_sha)

    def test_changed_source_quantity_or_evidence_denied(self):
        fixture={'issuedOrderId':ORDER,'issuedVersionId':ISSUED,'issuedOrderLineId':LINE}
        for changed in ({**CREATE,'purchaseOrderLineId':SERVICE},
                        {**CREATE,'quantity':'1'}, {**CREATE,'notes':'hidden'}):
            with self.assertRaises(Failed): fingerprints(self.browser(create=changed),fixture,SERVICE)
        with self.assertRaises(Failed): fingerprints(self.browser(certify={**CERTIFY,
            'evidenceReference':'different'}),fixture,SERVICE)

    def test_duplicate_and_extra_fields_denied(self):
        with self.assertRaises(Failed): unique_json('{"version":1,"version":2}',{'version'})
        with self.assertRaises(Failed): unique_json('{"version":1,"extra":2}',{'version'})

if __name__=='__main__': unittest.main()
