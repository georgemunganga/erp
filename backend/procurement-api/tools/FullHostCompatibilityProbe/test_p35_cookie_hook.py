"""Source admission checks for the uncompiled private P35 cookie hook.

These are guard checks, not SDK, HTTP, browser, database, or owner acceptance.
"""
from pathlib import Path
import re
import unittest

HERE = Path(__file__).resolve().parent
RUN = (HERE / "PrincipalApprovalHttpVerification.cs").read_text()
FIXTURE = (HERE / "PrincipalApprovalFixture.cs").read_text()
SERVE = (HERE / "PrincipalApprovalP35ContractReviewLocalCookieServe.cs").read_text()
NATIVE = (HERE / "PrincipalApprovalP35ContractReviewVerification.cs").read_text()


class P35CookieSourceAdmission(unittest.TestCase):
    def test_exclusive_fresh_database_and_native_dependency(self):
        self.assertIn('P35_HTTP_SERVE_CONTRACT_REVIEW_NORMAL_COOKIE', RUN)
        self.assertIn('^proc_p35_http_contract_review_[a-z0-9_]{1,24}$', RUN)
        self.assertIn('contractReviewFlag=="1" && principalPoFlag=="1"', RUN)
        for forbidden in ('receivingFlag is null', 'invoiceFlag is null', 'returnFlag is null',
                          'principalRfqFlag is null', 'matrixFlag is null', 'schemaFlag is null',
                          'localCookieFlag is null', 'commercialDraftFlag is null'):
            self.assertIn(forbidden, RUN)
        self.assertLess(RUN.index('VerifyPrincipalContractReviewAsync(fixture'),
                        RUN.index('ServeP35ContractReviewNormalLocalCookieAsync(\n'))

    def test_original_workerless_accounts_only(self):
        self.assertIn('principalContractReviewProfile?', FIXTURE)
        self.assertIn('Subjects=>', FIXTURE)
        self.assertIn('subjects.Length==10&&subjects.Distinct().Count()==10', SERVE)
        for fence in ('x.WorkerId is null', 'x.PasswordHash=="private-no-real-login"',
                      'x.PasswordHash=="private-no-real-login"', 'ExecuteUpdateAsync',
                      'PasswordChangedAt==null', 'LastLoginAt==null'):
            self.assertIn(fence, SERVE)

    def test_normal_host_and_completion_do_not_self_certify_browser(self):
        self.assertIn('normalLocalAuthentication:true', SERVE)
        self.assertIn('actual is ProcurementContractReviewStoreReadiness', SERVE)
        self.assertNotIn('ready.IsConfiguredAsync', SERVE)
        self.assertIn('privateSubjectHeaderRejected401=true', SERVE)
        self.assertIn('BrowserFinished', SERVE)
        self.assertIn('completionIsAcceptance=false', SERVE)
        self.assertIn('browserAcceptanceVerified=false', SERVE)
        self.assertIn('ownerCompletionVerified=false', SERVE)
        self.assertIn('UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite', SERVE)

    def test_native_source_and_not_product_simulation(self):
        self.assertIn('PASS_PRIVATE_P35_INTERNAL_CONTRACT_REVIEW_ONLY', NATIVE)
        self.assertIn('P35ContractReviewEvidence(id,supplierId,policyId', NATIVE)
        self.assertIn('ContractReviewRetainedVotes.Verify', NATIVE)
        self.assertIn('normalCookieAuthenticationVerified=false', NATIVE)
        self.assertEqual(re.findall(r'normalLocalAuthentication:true', SERVE),
                         ['normalLocalAuthentication:true'])


if __name__ == "__main__":
    unittest.main()
