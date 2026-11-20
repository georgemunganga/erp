import assert from 'node:assert/strict';
import test from 'node:test';
import { canRenderProductWorkspace } from '../src/platform/product-workspace-route.ts';
test('full product routing includes shared Core; server authority remains separate',()=>{
 for(const path of ['/hrm','/hrm/pay/compensation','/core','/core/workspace/settings/organization']) assert.equal(canRenderProductWorkspace(path,true),true);
});
test('routing does not admit foreign namespaces, similar prefixes or disabled full-workspace builds',()=>{
 for(const path of ['/hrm-other','/core-other','//foreign.invalid/core','/procurement','/']) assert.equal(canRenderProductWorkspace(path,true),false);
 for(const path of ['/core','/hrm']) assert.equal(canRenderProductWorkspace(path,false),false);
});
