import { FULL_PRODUCT_HRM } from "@/modules/core/product-hrm-session";
import { KuubuMark } from "./KuubuIdentity";
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

  if (FULL_PRODUCT_HRM && !logo) return <span className="inline-flex items-center gap-2 font-bold" aria-label="Kuubu"><KuubuMark /></span>;
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
          ERP
        </span>
      )}
      {showName ? <span className={nameClassName}>{branding?.displayName || "ERP workspace"}</span> : null}
    </>
  );
}
