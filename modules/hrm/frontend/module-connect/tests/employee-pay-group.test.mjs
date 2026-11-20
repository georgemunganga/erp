import assert from 'node:assert/strict';
import test from 'node:test';
import { employeePayGroupLabel } from '../src/platform/employee-pay-group.ts';
const p = {workerId:'worker-a',payGroupId:'g1',effectiveFrom:'2026-11-01'};
const groups = [{id:'g1',name:'Monthly ZMW'}];
test('profile displays its payroll-owned group without requiring a copied worker field',()=>{
  assert.equal(employeePayGroupLabel([p],groups,'worker-a','2026-11-20'),'Monthly ZMW');
});
test('other workers and future or ended profiles cannot supply the displayed group',()=>{
  for(const row of [{...p,workerId:'worker-b'},{...p,effectiveFrom:'2026-12-01'},{...p,effectiveTo:'2026-11-19'},{...p,isActive:false}])
    assert.equal(employeePayGroupLabel([row],groups,'worker-a','2026-11-20'),'No current salary profile');
});
test('missing groups and ambiguous active assignments are explicit, never invented defaults',()=>{
  assert.equal(employeePayGroupLabel([p],[],'worker-a','2026-11-20'),'Pay group unavailable — review Compensation');
  assert.equal(employeePayGroupLabel([p,p],groups,'worker-a','2026-11-20'),'Multiple current profiles — review Compensation');
});
test('non-ISO source dates cannot be mistaken for effective payroll assignments',()=>{
  assert.equal(employeePayGroupLabel([{...p,effectiveFrom:'11/01/2026'}],groups,'worker-a','2026-11-20'),'No current salary profile');
});
