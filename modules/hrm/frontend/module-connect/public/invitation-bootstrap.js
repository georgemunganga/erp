// Runs before hydration/telemetry. The invitation secret is never a URL query,
// route loader result, persisted browser value or rendered React property.
(() => {
  if (location.pathname.replace(/\/$/, "") !== "/accept-invitation") return;
  const fragment = location.hash;
  const query = location.search;
  history.replaceState(null, "", location.pathname);
  let token = !query && /^#token=[A-Za-z0-9_-]{43}$/.test(fragment) ? fragment.slice(7) : null;
  const deadline = Date.now() + 60000;
  const clear = () => {
    token = null;
    delete window.__kuubuTakeInvitationFragment;
  };
  window.__kuubuTakeInvitationFragment = () => {
    const result = Date.now() < deadline ? token : null;
    clear();
    return result;
  };
  setTimeout(clear, 60000);
})();
