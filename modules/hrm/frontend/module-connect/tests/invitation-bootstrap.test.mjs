import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";
const source = await readFile(
  new URL("../public/invitation-bootstrap.js", import.meta.url),
  "utf8",
);
function run(hash, search = "", pathname = "/accept-invitation") {
  let now = 0;
  const location = { hash, search, pathname };
  const changes = [];
  const timers = [];
  const window = {};
  const context = {
    location,
    window,
    history: {
      replaceState: (state, title, path) => {
        changes.push(path);
        location.hash = "";
        location.search = "";
      },
    },
    Date: { now: () => now },
    setTimeout: (callback, delay) => timers.push({ callback, delay }),
    localStorage: new Proxy(
      {},
      {
        get() {
          throw new Error("storage forbidden");
        },
      },
    ),
  };
  vm.runInNewContext(source, context);
  return {
    window,
    location,
    changes,
    timers,
    advance: () => {
      now = 60000;
    },
  };
}
test("secret is removed before hydration and retrieved only once from memory", () => {
  const token = "t".repeat(43);
  const f = run("#token=" + token);
  assert.equal(f.location.hash, "");
  assert.equal(f.location.search, "");
  assert.deepEqual(f.changes, ["/accept-invitation"]);
  assert.equal(f.window.__kuubuTakeInvitationFragment(), token);
  assert.equal(f.window.__kuubuTakeInvitationFragment, undefined);
});
test("invalid, duplicated, query-bearing and expired fragments are never accepted", () => {
  for (const hash of [
    "",
    "#token=short",
    "#token=" + "t".repeat(43) + "&other=1",
    "#token=%74" + "t".repeat(42),
    "#token=" + "t".repeat(43) + "&token=other",
  ]) {
    const f = run(hash);
    assert.equal(f.window.__kuubuTakeInvitationFragment(), null);
    assert.equal(f.location.hash, "");
  }
  const query = run("#token=" + "t".repeat(43), "?next=foreign");
  assert.equal(query.window.__kuubuTakeInvitationFragment(), null);
  const expired = run("#token=" + "t".repeat(43));
  expired.advance();
  assert.equal(expired.window.__kuubuTakeInvitationFragment(), null);
  const cleanup = run("#token=" + "t".repeat(43));
  cleanup.timers[0].callback();
  assert.equal(cleanup.window.__kuubuTakeInvitationFragment, undefined);
});
test("other ERP routes are untouched", () => {
  const f = run("#module", "?filter=1", "/team");
  assert.deepEqual(f.changes, []);
  assert.equal(f.location.hash, "#module");
  assert.equal(f.window.__kuubuTakeInvitationFragment, undefined);
});
