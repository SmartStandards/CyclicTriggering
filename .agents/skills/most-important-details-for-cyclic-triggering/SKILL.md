---
name: most-important-details-for-cyclic-triggering
description: Project-specific implementation details, constraints and pitfalls for this repository (shared projects, multi-targeting net48/net8/net10, UJMW-exposable contracts, generated versioning artifacts, documentation layout). Use before changing code, project files or docs in this repository.
---

**WARNING: This skill was placed here via copy-n-paste. The first AI-Agent consuming this will NEED TO read the solution and build-up this document initiallly (and the remove this notice!)**

# Most important details for [PLEASE-FILL-THIS]

## Documentation layout

- [PLASE-FILL-THIS]

## Code structure

- Source lives in **shared projects** (`*.shproj` + `*.projitems`). **Every new `.cs` file must be added to the matching
  `.projitems`** (`<Compile Include="$(MSBuildThisFileDirectory)…" />`), otherwise no target project compiles it.
- Compiler constants: `NET_FX` (net48, enables `[ServiceContract]`/`[OperationContract]` on contracts), `NET_CORE`, `NET_8_`, `NET_10_`.
- Namespaces are [PLASE-FILL-THIS]

## Contract rules

- Contracts must stay **UJMW/WCF-transportable**: synchronous methods, simple DTOs with public properties, no callbacks,

## Known state and pitfalls

- [PLASE-FILL-THIS]

## Related skills

- [PLASE-FILL-THIS]
