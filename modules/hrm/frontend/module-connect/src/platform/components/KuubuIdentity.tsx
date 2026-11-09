/** Reuses Kuubu's maintained website hippo mark and wordmark. */
export function KuubuIdentity() {
  return (
    <a
      href="https://www.kuubuerp.com/"
      aria-label="Kuubu home"
      className="inline-flex items-center gap-2 text-3xl font-bold tracking-tight"
    >
      <svg
        className="size-9 text-[var(--product-kuubu-mark)]"
        viewBox="0 0 48 46"
        aria-hidden="true"
      >
        <path
          fill="currentColor"
          d="M8 13C0 6 8-3 15 3c2 2 2 4 2 6 5-2 9-2 14 0 0-2 0-4 2-6 7-6 15 3 7 10l2 10c5 4 7 11 4 16-4 7-13 7-22 7S6 46 2 39c-3-5-1-12 4-16z"
        />
        <ellipse cx="15" cy="33" rx="2" ry="3" fill="var(--card)" />
        <ellipse cx="33" cy="33" rx="2" ry="3" fill="var(--card)" />
      </svg>
      <span>
        kuubu<span className="text-[var(--product-kuubu-mark)]">.</span>
      </span>
    </a>
  );
}
