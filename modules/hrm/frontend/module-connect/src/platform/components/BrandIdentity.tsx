import { useBranding } from "@/platform/branding";

export function BrandIdentity({
  onDark = false,
  showName = true,
  logoClassName = "h-8 w-auto max-w-[132px] object-contain",
  nameClassName = "font-semibold",
}: {
  onDark?: boolean;
  showName?: boolean;
  logoClassName?: string;
  nameClassName?: string;
}) {
  const { branding } = useBranding();
  const logo = onDark
    ? branding?.logoLightDataUri || branding?.logoDarkDataUri
    : branding?.logoDarkDataUri || branding?.logoLightDataUri;

  return (
    <>
      {logo ? (
        <img src={logo} alt="" className={logoClassName} decoding="async" />
      ) : (
        <span
          aria-hidden="true"
          className={`inline-flex size-8 shrink-0 items-center justify-center rounded-md text-xs font-bold ${
            onDark ? "bg-white/15 text-white" : "bg-primary text-primary-foreground"
          }`}
        >
          HR
        </span>
      )}
      {showName ? <span className={nameClassName}>{branding?.displayName || "HR workspace"}</span> : null}
    </>
  );
}
