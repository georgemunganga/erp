import { describe, it, afterEach } from "node:test";
import assert from "node:assert/strict";
import { AUTH_CONTEXT_KEY, AUTH_CONTEXT_EVENT, notifyAuthContextChanged } from "./auth-context.ts";
const originals = new Map(["window", "localStorage"].map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
afterEach(() => { for (const [key, descriptor] of originals) { if (descriptor) Object.defineProperty(globalThis,key,descriptor); else delete globalThis[key]; } });
function stub(key,value) { Object.defineProperty(globalThis,key,{value,configurable:true,writable:true}); }
describe("public authentication generation", () => {
  it("publishes an opaque nonce and same-document event without identity or credentials", () => {
    const entries=[],events=[];stub("localStorage",{setItem:(...args)=>entries.push(args)});stub("window",{dispatchEvent:event=>events.push(event)});
    notifyAuthContextChanged();assert.equal(entries.length,1);assert.equal(entries[0][0],AUTH_CONTEXT_KEY);assert.match(entries[0][1],/^[0-9a-f-]{36}$/);assert.equal(events.length,1);assert.equal(events[0].type,AUTH_CONTEXT_EVENT);assert.equal("user" in events[0],false);assert.equal("detail" in events[0],false);
  });
  it("still notifies this document when browser storage is unavailable", () => {
    const events=[];stub("localStorage",{setItem:()=>{throw new Error("Denied");}});stub("window",{dispatchEvent:event=>events.push(event)});
    assert.doesNotThrow(()=>notifyAuthContextChanged());assert.equal(events.length,1);assert.equal(events[0].type,AUTH_CONTEXT_EVENT);
  });
});
