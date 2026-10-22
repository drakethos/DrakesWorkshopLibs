# Plan: remove the "Workshop" leftovers from Libs naming

Status: **plan only, nothing changed.** Do this as its own change across every consumer mod, never inside a feature change (Teams, etc.).

Package, assembly and namespace are already `DrakeModsLibs` (Thunderstore id, `DrakeModsLibs.dll`, `DrakeModsLibs.*`, GUID `com.drakemods.libs`). Those do **not** change. Only the leftovers below do.

## What is named "Workshop" today (found by grep in this repo and DrakesRenameIt)

| Where | Name | New name |
|---|---|---|
| GitHub repo | `drakethos/DrakesWorkshopLibs` | `DrakesModsLibs` (GitHub redirects the old URL; update `WebsiteUrl` in every `mod.package.props`) |
| Props file | `build/DrakeMod.Reference.WorkshopLibs.props` | `build/DrakeMod.Reference.Libs.props` |
| MSBuild properties | `_WorkshopLibsProj`, `WorkshopLibsDll`, `WorkshopLibsPluginFolder`, `_WorkshopLibsReferenceMissing`, target `VerifyWorkshopLibsReference` | `_LibsProj`, `LibsDll`, `LibsPluginFolder`, `_LibsReferenceMissing`, `VerifyLibsReference` |
| Import line | `Directory.Build.props` imports the props file above | import the new file |
| CI | each consumer `release.yml`: `/p:WorkshopLibsDll=...`, env `WORKSHOP_LIBS_DLL` | `/p:LibsDll=...`, `LIBS_DLL` |
| Code | `DrakeCustomDataCatalog.ModWorkshopLibs` (value is already `"DrakeModsLibs"`) | `ModLibs`; keep `ModWorkshopLibs` as an `[Obsolete]` alias for one release (public const) |
| Comments / docs | `Verify-NoServerSyncInModDll.ps1` comment; `docs/RenameIt-1.1-Regression-Plan.md` title and "WorkshopLibs 0.3.0+" | reword (history docs can keep the old name in quotes) |
| Not in this repo | `drakethos/drakesworkshop-lib-release`; the suite-root `environment.props` and `scripts\Sync-ModBuild.ps1` (copies `build/` into each mod) | rename the repo; update the two local files |

DrakesWorkshop (the Godot game) is a different project and is **not** touched.

## What breaks if done naively

1. Every consumer repo carries its **own copy** of `Directory.Build.props` and `build/` (copied from RenameIt/Libs). A rename in Libs alone leaves consumers pointing at the old props file, which still exists in their copy, so nothing fails loudly until the copies diverge. They must be re-synced together.
2. The user's suite-root / per-mod `environment.props` may set `WorkshopLibsDll` or `WorkshopLibsPluginFolder`. Renaming silently ignores them and the build falls back to the profile path or errors with "DrakeModsLibs not found".
3. CI passes `/p:WorkshopLibsDll`. A consumer on the new props with an old workflow (or the reverse) fails the reference check.
4. `ModWorkshopLibs` is a public const; a consumer using it stops compiling.

## Order

1. **Libs 0.12.0 (additive).** Add `Reference.Libs.props` with the new names; make it also read the old property names as fallbacks (`LibsDll` defaults to `$(WorkshopLibsDll)`, same for the plugin folder). Keep the old props file as a one-line import of the new one. Add `ModLibs`, mark `ModWorkshopLibs` `[Obsolete]`. Changelog says: no API removed.
2. **Each consumer, one PR each** (RenameIt, LockSmith, ItemShop, Essentials, ReskinIt, Teams, anything else with a `build/` copy): re-copy `build/` and `Directory.Build.props` from Libs 0.12.0, switch `release.yml` to `LibsDll` / `LIBS_DLL`, bump to a patch version, build once locally and once in CI. Order does not matter because the fallbacks keep both spellings working.
3. Update the suite-root `environment.props` and `Sync-ModBuild.ps1` to the new names.
4. **Libs 0.13.0 (removal).** Only when every consumer is on step 2: delete the old props file, the fallbacks and the obsolete const.
5. Rename the GitHub repos last (`DrakesWorkshopLibs` -> `DrakesModsLibs`, `drakesworkshop-lib-release`), then fix `WebsiteUrl` and any README links in a follow-up patch. Do not rename repos while step 2 PRs are open.

## Verify

- Each consumer: clean clone, CI build green, plus a local build with the suite `environment.props` (new and old spellings).
- Grep each repo for `Workshop` afterwards; the only hits left should be history docs and the Godot game's own name.
- Load the Gale `drakeTest` profile once with the new Libs and a consumer on each side of step 2.
