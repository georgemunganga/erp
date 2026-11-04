# Current workflow capability audit probe

This is a **verification baseline**, not a new approval engine or a permanent requirement to reject custom roles. It confirms both existing Core engine behaviour and current Procurement configuration limits. Update the expected limitations when the corresponding features are implemented.

It references prebuilt accepted Core/Procurement assemblies mounted read-only at `/runtime`. Run in the existing .NET10 SDK with network disabled and fresh mutable `/probe` output, never over a retained publication. Copy these three files into that fresh project directory, mount the accepted118-file Procurement test runtime read-only at `/runtime`, and run:

```sh
dotnet restore Probe.csproj --configfile NuGet.Config --source /probe/empty-feed -p:NuGetAudit=false
dotnet build Probe.csproj --no-restore -c Release -p:UseSharedCompilation=false
dotnet bin/Release/net10.0/Probe.dll
```

Create the empty feed directory first. No packages or business records are downloaded/created. Only this synthetic in-memory probe is compiled; the product assemblies are not rebuilt. Preserve source/runtime hashes before and after and retain failed attempts separately. The recorded execution used SDK10.0.400 and accepted runtime source9fecef17, with10 checks passing.

A PASS confirms present capabilities and limitations; it is **not** production UAT, live actor authentication, configured company authority, payment execution, or readiness to hand over Procurement. See `docs/procurement/gap-closure/COMPANY-APPROVAL-WORKFLOW-VERIFICATION-20261104.md`.
