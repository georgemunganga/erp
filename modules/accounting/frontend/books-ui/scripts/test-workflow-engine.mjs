import { createServer } from 'vite';
import { writeFileSync } from 'node:fs';
import assert from 'node:assert/strict';
const server = await createServer({ server: { middlewareMode: true }, appType: 'custom' });
try {
  const { allFlows } = await server.ssrLoadModule('/src/workflows/registry.ts');
  const { executeWorkflow, listResources, calculatedTotal } = await server.ssrLoadModule('/src/workflows/engine.ts');
  const { initialData, moduleSpecs } = await server.ssrLoadModule('/src/data.ts');
  const { seedFlowStore } = await server.ssrLoadModule('/src/workflows/store.tsx');
  const { evaluateCriteria } = await server.ssrLoadModule('/src/workflows/criteria.ts');
  assert.equal(new Set(allFlows.map(flow => flow.id)).size, allFlows.length, 'Unique workflow IDs');
  const fixtures = [], failures = [], passed = [];
  function setup(flow) {
    const workspace = structuredClone(initialData); const store = seedFlowStore();
    const credit = (id, party) => ({ id, code: id, name: 'Credit allowance', party, date: '2026-10-21', status: 'Open', amount: 500 });
    workspace.creditNotes = [credit('credit-100', workspace.invoices[0].party)]; workspace.vendorCredits = [credit('vendor-credit-100', workspace.bills[0].party)];
    workspace.recurringBills = [{ ...workspace.bills[0], id: 'recurring-bill-100', status: 'Active', category: 'Monthly' }];
    for (const key of ['invoices','bills','quotes','purchaseOrders','recurringInvoices','recurringBills']) for (const row of workspace[key]) row.details = { name: row.name, party: workspace.customers.find(x=>x.name===row.party)?.id || workspace.vendors.find(x=>x.name===row.party)?.id || row.party, lines: [{ description: row.name, quantity: 1, rate: row.amount }], amount: row.amount };
    const journalLines = [{ account: 'account-cash', debit: 100, credit: 0 }, { account: 'account-sales', debit: 0, credit: 100 }];
    workspace.journals = [{ id:'journal-100', code:'JRN-0100', name:'Balanced journal', party:'Reference', date:'2026-10-21', status:flow.id==='reverse-journal'?'Published':'Draft', amount:100, details:{lines:journalLines} }];
    if (/resume-recurring/.test(flow.id)) for (const row of workspace[flow.effect.entity]) row.status='Paused';
    if (flow.id==='accept-quote') workspace.quotes[0].status='Sent';
    if (flow.id==='send-quote') workspace.quotes[0].status='Draft';
    if (flow.id==='convert-quote-to-invoice') workspace.quotes[0].status='Accepted';
    if (flow.id==='send-invoice') workspace.invoices[0].status='Draft';
    if (flow.id==='approve-purchase-order') workspace.purchaseOrders[0].status='Sent';
    if (flow.id==='submit-purchase-order') workspace.purchaseOrders[0].status='Draft';
    if (flow.id==='convert-purchase-order-to-bill') workspace.purchaseOrders[0].status='Approved';
    if(flow.id==='convert-expense-to-billable')workspace.expenses[0].details={...workspace.expenses[0].details,billable:true};
    const artifact = (id, collection, values, status='Active') => ({id,collection,name:String(values.name||id),values,status,history:[]});
    store.artifacts.push(artifact('rule-100','bankRules',{name:'Supplies rule',account:'bank-operations',category:'Office supplies'}),artifact('automation-rule-100','automationRules',{name:'Overdue check',criteria:'status=Overdue AND amount>=100',active:true}),artifact('automation-action-100','automationActions',{name:'Follow-up',rule:'automation-rule-100',actionType:'Email alert',message:'Review record'}),artifact('task-100','tasks',{name:'Planning task',project:'project-101',timerState:'Running'},'Running'),artifact('saved-report-100','reports',{name:'October sales',reportType:'Sales by Customer',fromDate:'2026-10-01',toDate:'2026-10-31'}));
    if (/reconcile-bank-account/.test(flow.id)) store.artifacts.find(x=>x.id==='bank-txn-income').status='Categorized';
    if (flow.id==='undo-bank-match') { const txn=store.artifacts.find(x=>x.id==='bank-txn-income');txn.status='Matched';Object.assign(txn.values,{targetRecord:'invoice-1048',matchedCollection:'invoices',matchedAmount:5400,matchedAllocations:[{recordId:'invoice-1048',amount:5400}]}); workspace.invoices[0].status='Paid';workspace.invoices[0].paidAmount=5400; }
    if (flow.id==='undo-bank-reconciliation') { store.artifacts.push(artifact('reconciliation-100','reconciliations',{name:'October reconciliation',transactionIds:['bank-txn-income'],account:'bank-operations'},'Reconciled'));store.artifacts.find(x=>x.id==='bank-txn-income').values.reconciled=true; }
    return {workspace,store};
  }
  function makeValues(flow, workspace, store) {
    const values={};
    for (const field of flow.steps.flatMap(step=>step.fields)) {
      if (field.defaultValue !== undefined) values[field.id]=structuredClone(field.defaultValue);
      else if (field.type==='record') values[field.id]=listResources(workspace,store,field.collection||'')[0]?.id || '';
      else if (field.type==='multi-record') values[field.id]=listResources(workspace,store,field.collection||'').slice(0,1).map(row=>row.id);
      else if (field.type==='checkbox') values[field.id]=true;
      else if (field.type==='select') values[field.id]=field.options?.[0] || '';
      else if (field.type==='date') values[field.id]='2026-10-21';
      else if (field.type==='number') values[field.id]=Math.max(field.min ?? 0, 100);
      else if (field.type==='email') values[field.id]='review@example.test';
      else if (field.type==='lines') values[field.id]=[{description:'Design workshop',quantity:1,rate:100}];
      else if (field.type==='journal-lines') values[field.id]=[{account:'account-cash',debit:100,credit:0},{account:'account-sales',debit:0,credit:100}];
      else if (field.type==='allocations') { const option=listResources(workspace,store,field.collection||'')[0];values[field.id]=option ? [{recordId:option.id,amount:Math.min(100,Math.abs(option.amount)||100)}] : []; }
      else if (field.type==='attachment') values[field.id]=JSON.stringify({filename:'fictional-receipt.txt',base64:'RmljdGlvbmFsIHJlY2VpcHQ='});
      else if (field.type==='csv') values[field.id]='name,party,date,amount,description\nImported sample,Aster & Oak Studio,2026-10-21,100,Sample description';
      else values[field.id]=field.id==='criteria'?'status=Overdue AND amount>=100':'Workflow example';
    }
    for (const field of flow.steps.flatMap(step=>step.fields)) if (field.type==='checkbox' && field.required) values[field.id]=true;
    if (values.amount!==undefined) values.amount=100;
    if (values.quantity!==undefined) values.quantity=flow.id==='adjust-item-stock'?2:1;
    if (flow.id==='match-bank-deposit-to-invoice') {values.record='bank-txn-income';values.targetRecord='invoice-1048';values.allocations=[{recordId:'invoice-1048',amount:5400}];}
    if (flow.id==='match-bank-withdrawal-to-bill') {values.record='bank-txn-expense';values.targetRecord='bill-201';values.allocations=[{recordId:'bill-201',amount:1240}];}
    if (flow.id==='reconcile-bank-account') {values.allocations=[{recordId:'bank-txn-income',amount:5400}];values.statementBalance=5400;}
    if (flow.id==='test-automation-rule') {values.sampleStatus='Overdue';values.sampleAmount=500;}
    if (flow.id==='create-automation-rule') values.criteria='status=Overdue AND amount>=100';
    if (values.discount!==undefined) values.discount=0;if(values.taxRate!==undefined)values.taxRate=0;if(values.shipping!==undefined)values.shipping=0;
    return values;
  }
  for (const flow of allFlows) {
    const {workspace,store}=setup(flow);const values=makeValues(flow,workspace,store);
    fixtures.push({flow,workspace,store,values});
    try { const result=executeWorkflow(flow,values,workspace,store);assert.equal(result.store.receipts.length,store.receipts.length+1);assert.ok(result.receipt.links.length>0,'Committed workflow must have record links');assert.ok(JSON.stringify(result.workspace)!==JSON.stringify(workspace)||JSON.stringify(result.store.artifacts)!==JSON.stringify(store.artifacts), 'Workflow must change an operational local record or artifact in addition to creating its receipt');passed.push(flow.id); }
    catch(error) { failures.push({id:flow.id,error:error.message}); }
  }
  const negative=[];
  const rejects=(name,fn)=>{assert.throws(fn);negative.push(name)};
  const invoice=allFlows.find(x=>x.id==='record-invoice-payment');const {workspace,store}=setup(invoice);
  rejects('Payment above outstanding',()=>executeWorkflow(invoice,{date:'2026-10-21',amount:10000,allocations:[{recordId:'invoice-1048',amount:10000}]},workspace,store));
  rejects('Payment total mismatch',()=>executeWorkflow(invoice,{date:'2026-10-21',amount:90,allocations:[{recordId:'invoice-1048',amount:100}]},workspace,store));
  rejects('Mixed customer allocations',()=>executeWorkflow(invoice,{date:'2026-10-21',amount:200,allocations:[{recordId:'invoice-1048',amount:100},{recordId:'invoice-1047',amount:100}]},workspace,store));
  rejects('Duplicate allocations',()=>executeWorkflow(invoice,{date:'2026-10-21',amount:200,allocations:[{recordId:'invoice-1048',amount:100},{recordId:'invoice-1048',amount:100}]},workspace,store));
  rejects('Payment allocation does not match selected record',()=>executeWorkflow(allFlows.find(x=>x.id==='pay-bill'),{record:'bill-201',amount:100,allocations:[{recordId:'bill-202',amount:100}]},workspace,store));
  rejects('Quote conversion before acceptance',()=>executeWorkflow(allFlows.find(x=>x.id==='convert-quote-to-invoice'),{record:'quote-101',date:'2026-10-21'},workspace,store));
  rejects('Unbalanced journal',()=>executeWorkflow(allFlows.find(x=>x.id==='create-journal'),{date:'2026-10-21',name:'Bad entry',category:'Accrual',lines:[{account:'account-cash',debit:100,credit:0},{account:'account-sales',debit:0,credit:99}]},workspace,store));
  rejects('Code-like criteria rejected',()=>evaluateCriteria('fetch("external")',{name:'Sample',status:'Open',amount:1}));
  assert.equal(evaluateCriteria('status=Overdue AND amount>=100',{name:'Sample',status:'Paid',amount:500}).matched,false);negative.push('Automation non-match');
  assert.equal(calculatedTotal({lines:[{description:'Two items',quantity:2,rate:100}],discount:10,taxRate:15,shipping:5}),223.5,'Line totals must use one shared discount/tax/shipping formula');negative.push('Derived line total formula');
  const receiptFlow=allFlows.find(flow=>flow.id==='process-receipt');const receiptSetup=setup(receiptFlow);receiptSetup.store.artifacts.push({id:'fictional-document-100',collection:'documents',name:'Receipt · fictional',status:'Inbox',values:{filename:'receipt.txt',amount:250},history:[]});const receiptValues=makeValues(receiptFlow,receiptSetup.workspace,receiptSetup.store);Object.assign(receiptValues,{document:'fictional-document-100',destination:'Bill',merchant:'vendor-101',amount:250});const receiptResult=executeWorkflow(receiptFlow,receiptValues,receiptSetup.workspace,receiptSetup.store);assert.ok(receiptResult.workspace.bills.some(row=>row.sourceId==='fictional-document-100'&&row.party==='Cedarline Printworks'),'Bill destination must create a bill linked to the selected local vendor');negative.push('Receipt Bill destination');
  const itemImport=allFlows.find(flow=>flow.id==='import-items');const itemSetup=setup(itemImport);const itemValues=makeValues(itemImport,itemSetup.workspace,itemSetup.store);Object.assign(itemValues,{csv:'name,quantity,rate\nImported fictional item,7,123',quantity:7,rate:123,category:'Sample inventory',description:'Imported locally'});const itemResult=executeWorkflow(itemImport,itemValues,itemSetup.workspace,itemSetup.store);assert.equal(itemResult.workspace.items[0].stock,7,'Item import must apply mapped quantity');assert.equal(itemResult.workspace.items[0].category,'Sample inventory','Item import must apply selected category default');negative.push('Item import defaults and stock mapping');
  for (const flow of allFlows) for (const next of flow.nextFlowIds || []) if(!allFlows.some(candidate=>candidate.id===next)) failures.push({id:flow.id,error:'Broken next workflow: '+next});
  const manifest={routes:[{path:'/',title:'Dashboard'},...moduleSpecs.map(spec=>({path:spec.path,title:spec.title})),...moduleSpecs.filter(spec=>spec.createPath).map(spec=>({path:spec.createPath,title:'Create '+spec.entityLabel})),...['/items/:itemId','/records/:moduleKey/:recordId','/workflows','/workflows/:flowId','/workflow-results/:receiptId','/workflow-resources/:artifactId','/banking','/chart-of-accounts','/transaction-locking','/reports','/reports/:reportSlug','/documents','/settings','/settings/general','/settings/templates','/settings/automation'].map(path=>({path}))]};
  writeFileSync('public/manus-routes.json',JSON.stringify(manifest,null,2)+'\n');
  writeFileSync('tests/fixtures/workflow-cases.json',JSON.stringify(fixtures,null,2)+'\n');
  const summary={registered:allFlows.length,passed:passed.length,negativeChecks:negative,failures,domainCounts:Object.fromEntries([...new Set(allFlows.map(x=>x.domain))].map(domain=>[domain,allFlows.filter(x=>x.domain===domain).length]))};
  writeFileSync('docs/workflow-depth/engine-test-results.json',JSON.stringify(summary,null,2)+'\n');
  writeFileSync('docs/workflow-depth/workflow-inventory.json',JSON.stringify(allFlows,null,2)+'\n');
  const lines=['# Registered workflow inventory','',`Generated from the live application registry: ${allFlows.length} locally executable workflows. This is a simulation catalog, not evidence that every path was inspected or completed in the selected Zoho tenant. See \`../reference-manifest.json\` for tenant evidence labels.`,''];
  for(const domain of [...new Set(allFlows.map(flow=>flow.domain))]){lines.push(`## ${domain}`,'');for(const flow of allFlows.filter(item=>item.domain===domain)){const steps=flow.steps.map(step=>`${step.title} (${step.fields.map(field=>`${field.label}${field.required?'*':''}`).join(', ')||'confirmation'})`).join(' → ');lines.push(`### \`${flow.id}\` · ${flow.title}`,'',`**Route:** \`/workflows/${flow.id}\` · **Effect:** \`${flow.effect.kind}${flow.effect.entity?` → ${flow.effect.entity}`:''}${flow.effect.target?` → ${flow.effect.target}`:''}${flow.effect.collection?` → ${flow.effect.collection}`:''}\` · **Registry evidence label:** ${flow.evidence}`,'',steps,``,flow.description,flow.completionMessage,...(flow.nextFlowIds?.length?[`**Next:** ${flow.nextFlowIds.map(id=>`\`${id}\``).join(', ')}`]:[]),'');}}
  writeFileSync('docs/workflow-depth/workflow-inventory.md',lines.join('\n')+'\n');console.log(JSON.stringify(summary,null,2));
  if(failures.length) process.exitCode=1;
} finally { await server.close(); }
