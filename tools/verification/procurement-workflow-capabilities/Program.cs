using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
var company=Guid.NewGuid();const string tenant="verification",employee="11111111-1111-4111-8111-111111111111",supervisor="22222222-2222-4222-8222-222222222222",executive="33333333-3333-4333-8333-333333333333",permission="procurement-approval-approve";
var checks=new List<string>();
void Need(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
void Denies<T>(Action action,string name) where T:Exception {try{action();}catch(T){checks.Add(name);return;}throw new Exception(name);}
ApprovalActor Actor(string who)=>new(who,tenant,company,new HashSet<string>{permission});
ApprovalCase Case(string maker,params string[] people){var c=new ApprovalCase{TenantId=tenant,LegalEntityId=company,SourceModule="procurement",SourceType="purchase_request",SourceId=Guid.NewGuid(),SourceVersion=1,PolicyVersionId=Guid.NewGuid(),PolicyVersionNumber=1,MakerSubjectId=maker};for(var i=0;i<people.Length;i++){var s=new ApprovalStage{TenantId=tenant,LegalEntityId=company,CaseId=c.Id,Code="STEP_"+i,Sequence=i+1,Mode="All",RequiredApprovals=1,State=i==0?"Active":"Waiting"};s.Assignments.Add(new(){TenantId=tenant,LegalEntityId=company,CaseId=c.Id,StageId=s.Id,AssigneeSubjectId=people[i],PermissionKey=permission});c.Stages.Add(s);}return c;}
var c=Case(employee,supervisor,executive);ApprovalEngine.ValidateNewCase(c);checks.Add("Engine accepts employee-to-supervisor-to-executive definition");var stages=c.Stages.OrderBy(s=>s.Sequence).ToArray();var decisions=new List<ApprovalDecision>();var now=DateTimeOffset.UtcNow;
Denies<InvalidOperationException>(()=>ApprovalEngine.Decide(c,stages[1].Assignments.Single(),Actor(executive),"Approve","early",null,decisions,now),"Executive cannot bypass pending supervisor stage");
var first=ApprovalEngine.Decide(c,stages[0].Assignments.Single(),Actor(supervisor),"Approve","first",null,decisions,now);Need(!first.CaseCompleted&&c.State=="Pending"&&stages[1].State=="Active","Supervisor approval activates executive stage without final approval");
var last=ApprovalEngine.Decide(c,stages[1].Assignments.Single(),Actor(executive),"Approve","last",null,decisions,now);Need(last.CaseCompleted&&c.State=="Approved"&&decisions.Count==2,"Executive completes independently approved chain");
var own=Case(supervisor,supervisor,executive);Denies<ArgumentException>(()=>ApprovalEngine.ValidateNewCase(own),"Supervisor cannot be assigned own request approval");
var supervisorRequest=Case(supervisor,executive);ApprovalEngine.ValidateNewCase(supervisorRequest);var supDecisions=new List<ApprovalDecision>();Need(ApprovalEngine.Decide(supervisorRequest,supervisorRequest.Stages.Single().Assignments.Single(),Actor(executive),"Approve","supervisor-submission",null,supDecisions,now).CaseCompleted,"Engine supports supervisor submission sent directly to executive");
string Rules(string assignee,bool addNamed=false) {
 var stage=new Dictionary<string,object?>{{"code","EXECUTIVE"},{"assignee",assignee},{"mode","One"}};
 if(addNamed)stage["principalId"]=executive;
 return JsonSerializer.Serialize(new {schemaVersion=1,transactionType="PurchaseRequest",currencyCode="ZMW",amountBasis="EstimatedLineBase",rules=new[]{new {code="DEFAULT",priority=0,
 when=new {minimumAmountInclusive=(decimal?)null,maximumAmountExclusive=(decimal?)null,categoryCodes=Array.Empty<string>(),purchaseTypes=Array.Empty<string>(),branchIds=Array.Empty<string>()},
 result=new {budgetReservation="Required",sourcingMethodCode=(string?)null,minimumQuoteCount=(int?)null,approvalStages=new[]{stage}}}}});
}
Need(PolicyRuleValidator.Validate(Rules("ProcurementManager")).Count==0,"Current rule validator accepts supported predefined assignee");
Need(PolicyRuleValidator.Validate(Rules("CEO")).Any(x=>x.Path.EndsWith(".assignee")),"Current rule validator rejects arbitrary CEO approver type");
Need(PolicyRuleValidator.Validate(Rules("ProcurementManager",true)).Count>0,"Current rule validator rejects named executive binding field");
var branch=Guid.NewGuid();var invoice=Guid.NewGuid();var supplier=Guid.NewGuid();var actor=new ManualPaymentActor(employee,tenant,company,branch,new HashSet<Guid>{branch},new HashSet<string>{"procurement-payment-create"},true);
ManualPaymentAssignee Person(string id,string action)=>new(id,tenant,company,new HashSet<Guid>{branch},new HashSet<string>{"procurement-payment-"+action},true);
var draft=new ManualPaymentDraft(branch,Guid.Empty,supplier,"ZMW",10m,supervisor,executive,"44444444-4444-4444-8444-444444444444","advance");var source=new ManualPaymentInvoiceSource(invoice,tenant,company,branch,supplier,"ZMW",new DateOnly(2026,11,4),100m,100m,true);
Denies<InvalidOperationException>(()=>ManualPaymentWorkflow.Create(draft,actor,Person(supervisor,"approve"),Person(executive,"record"),Person(draft.ConfirmerPrincipalId,"confirm"),source,now),"Payment draft without its actual invoice is rejected");
Console.WriteLine(JsonSerializer.Serialize(new{status="PASS_EXISTING_ENGINE_AND_CONFIGURATION_BOUNDARY_PROBE",checks,source="9fecef1732b7629f5ac8dbd005c863d20898a250",productionWorkflowNotExecuted=true}));
