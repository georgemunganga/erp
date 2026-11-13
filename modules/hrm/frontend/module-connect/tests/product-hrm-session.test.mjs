import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';

let source = await readFile(new URL('../src/modules/core/product-hrm-session.ts', import.meta.url), 'utf8');
for (const name of ['core-workspace-context', 'product-operational-api'])
  source = source.replace('"./'+name+'"', JSON.stringify(new URL('../src/modules/core/'+name+'.ts',import.meta.url).href));
source = source.replaceAll('import.meta.env.VITE_ERP_PRODUCT_MODE','"true"').replaceAll('import.meta.env.VITE_ERP_FULL_HRM','"true"');
const code = ts.transpileModule(source,{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext}}).outputText;
const {loadProductHrmSession,clearProductHrmSession,productHrmHeaders,ProductHrmSessionError,PRODUCT_HRM_RECHECK_EVENT} = await import('data:text/javascript;base64,'+Buffer.from(code).toString('base64'));
const company='11111111-1111-4111-8111-111111111111', marker='a'.repeat(64);
const context={schemaVersion:1,tenantId:'company-a',contextKind:'product',selectedModule:'hrm',hrmWorkspace:true,
  selectedCompanyId:company,principalId:'person-a',moduleRoles:['hr_admin'],sessionContext:marker,
  companies:[{id:company,code:'A',registeredName:'Company A',currency:'ZMW',isDefault:true}],branches:[],coreActions:[]};
const operational={verified:true,purpose:'erp-session-v1',product_code:'kuubu',csrf_token:'c'.repeat(43),
  operational_access_granted:false,next_step:'company_selection',proof_until:new Date(Date.now()+60000).toISOString(),company_discovery_available:true};
async function withFetch(contextStatus,action,operationalStatus=200){
  const original=globalThis.fetch,calls=[];
  globalThis.fetch=async(path,options)=>{calls.push({path,options});return path.endsWith('/operational/session')
    ? Response.json(operational,{status:operationalStatus}) : Response.json(context,{status:contextStatus});};
  try {await action(calls);} finally {globalThis.fetch=original;clearProductHrmSession();}
}
test('continuity is observed before context, with cookie-only transport and exact marker',async()=>{
  await withFetch(200,async calls=>{
    const result=await loadProductHrmSession(new AbortController().signal,marker);
    assert.equal(result.marker,marker);assert.equal(calls.length,2);
    assert.ok(calls[0].path.endsWith('/operational/session'));
    assert.equal(calls[1].options.headers['X-Kuubu-Session-Context'],marker);
    assert.equal(productHrmHeaders()['X-Shell-Entity'],company);
    assert.equal(productHrmHeaders().Authorization,undefined);
  });
});
test('temporary failure removes the request capability and can recover without a new login',async()=>{
  await withFetch(200,async()=>{await loadProductHrmSession(new AbortController().signal);});
  // Establish and fail in one lifecycle; do not retain headers during outage.
  const original=globalThis.fetch;let status=200;
  globalThis.fetch=async path=>Response.json(path.endsWith('/operational/session')?operational:context,{status:path.endsWith('/operational/session')?200:status});
  try {
    await loadProductHrmSession(new AbortController().signal); status=503;
    await assert.rejects(loadProductHrmSession(new AbortController().signal,marker),e=>e instanceof ProductHrmSessionError && e.temporary);
    assert.throws(()=>productHrmHeaders()); status=200;
    await loadProductHrmSession(new AbortController().signal,marker);
    assert.equal(productHrmHeaders()['X-Kuubu-Session-Context'],marker);
  } finally {globalThis.fetch=original;clearProductHrmSession();}
});
for (const status of [401,403,409]) test('status '+status+' requires a real reconnect/review rather than transient recovery',async()=>{
  await withFetch(status,async()=>{
    await assert.rejects(loadProductHrmSession(new AbortController().signal,marker),e=>e instanceof ProductHrmSessionError && !e.temporary && e.status===status);
    assert.throws(()=>productHrmHeaders());
  });
});
test('an unavailable operational check never proceeds to context or grants request headers',async()=>{
  await withFetch(200,async calls=>{
    await assert.rejects(loadProductHrmSession(new AbortController().signal),e=>e.temporary && e.status===503);
    assert.equal(calls.length,1);assert.throws(()=>productHrmHeaders());
  },503);
});
test('a missing memory context stops a save and requests a recheck without submitting it',async()=>{
  const original=globalThis.window, events=new EventTarget();let rechecks=0;
  globalThis.window=events;events.addEventListener(PRODUCT_HRM_RECHECK_EVENT,()=>rechecks++);
  try {
    clearProductHrmSession();assert.throws(()=>productHrmHeaders(),/No changes were submitted/);
    await new Promise(resolve=>queueMicrotask(resolve));assert.equal(rechecks,1);
  } finally {if(original===undefined)delete globalThis.window;else globalThis.window=original;}
});
