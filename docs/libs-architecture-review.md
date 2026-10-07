# DrakeModsLibs architecture review

*2026-10-07. Prompted by two LockSmith menu bugs that could only be fixed by re-releasing Libs. Revised after your answers: Libs is the shared customization-suite layer; the goal is **no bloat for someone who installs a single mod**.*

## Your direction (now the rule)

1. **What Libs is for.** Anything that is shared, or *could* be shared, and has to move fluidly across mods:
   - UI controls (even the −/+ stepper only RenameIt uses today);
   - the item-display, tooltip and tag hooks that were built so several customization mods can work on the same item.
2. **No bloat for single-mod installs.** A RenameIt-only player must not pay for things RenameIt never uses. Paying for what *this* mod needs, or for multi-mod integration when several mods are loaded, is fine.
3. **The UI kit is right in principle but too stiff in practice.**

## Bottom line

- **Where the bloat actually is.** Unused code sitting in the Libs DLL costs almost nothing at runtime: .NET only compiles a method the first time it's called, so the ~8,500 lines are mostly download/disk weight. The real bloat is **always-on behavior**. At startup Libs patches about **30 game methods** for every install, whichever mods are present.
- **The fix for that** is to keep features dormant until a mod turns them on.
- **The fix for the stiffness** is a layout core under the UI kit.

---

## 1. Inventory, judged by your rule

| Area | Who uses it today | Verdict |
|---|---|---|
| `DrakeConfigSync` | ItemForge, ItemShop, RenameIt, VRP, LockSmith | Core. Always on is fine (passive until a mod binds). |
| `CustomizeLibsAPI`, `Data` keys, tags (`DrakeTagManager`, `CustomizationGatekeeper`) | 5 mods / ItemForge+RenameIt | Suite contract. Stays. |
| Item display / name / tooltip / stack patches | RenameIt (by design for the suite) | Stays, but **dormant until a customization mod registers** (§3). |
| `ItemLookService` + 13 `VisEquipment` hooks | ReskinIt | Stays as a suite hook, but **dormant until ReskinIt registers**. Highest risk today: spawn/equip patches running on every install. |
| `Progression` (boss-key gating, craft tracking) | ReskinIt | Suite-shareable (any customization mod could gate on progress). Stays; craft tracking dormant until used. |
| UI kit: action menu, confirm, text prompt, stepper, toggle, tab host, sfx, input block | 1–3 mods each | Stays. **Rebuild on a layout core** (§2). |
| `CompatHost`, `ArtItemLoader`, permission profiles | 2–3 mods each | Stays. Passive until called. |
| `DrakePlayerIdentity`, `ItemIconService`, `ItemDisplayService`, `InventoryContextHints` | Libs only | Mark `internal` unless meant as API. Smaller promise, same code. |

Nothing needs to move out of Libs. The work is to make it **lazy** and make the UI **flexible**.

---

## 2. UI kit: from stiff to dynamic

**Why it's stiff.** Every panel places its parts by hand: about 140 hardcoded pixel values across six files, with no shared layout code and no caller options. Any change in content (more buttons, longer text) needs a Libs rebuild and release.

**This week's cost:**
- the action menu overflowed into Close when it had more than four buttons;
- it showed no buttons after a logout or character swap;
- the confirm popup is fixed at 300×178, so long text is cut off. That's the root of the tester's "the warning is too long to read" complaint.

**Proposal: a layout core** (`UI/Layout/`):
- **Panel builder.** A wood panel with Unity's `VerticalLayoutGroup` and `ContentSizeFitter`, so its height follows its content.
- **Rows.** Title, wrapped text, a button list, button rows and input fields, each sized by a `LayoutElement`.
- **Overflow.** Above `MaxHeight` the content scrolls (`ScrollRect`) instead of overflowing.
- **Caller options.** `DrakePanelOptions { Width, MinHeight, MaxHeight, ButtonWidth, … }`, with today's sizes as the defaults.
- **Existing panels** (`DrakeWoodActionMenu`, `DrakeConfirmPanel`, …) become thin wrappers on the core, with **the same public signatures**, so no mod changes.

**Result:** content and size changes stay in the mod. Libs only releases for real behavior changes.

---

## 3. Pay for what you use: dormant until registered

**Today:** `HarmonyPatchHub.ApplyAll` patches everything at startup. A RenameIt-only player gets ReskinIt's model-swap hooks; a LockSmith-only player gets RenameIt's display, tooltip and stack hooks and ReskinIt's.

**Proposal:**
- **Group the patches into features**, each with its own Harmony ID, for example:
  - `Display` (names, tooltips),
  - `Stacks`,
  - `Looks` (VisEquipment model swap),
  - `Progression` (craft tracking),
  - `InventoryContext` (Shift+RMB tabs).
- **A feature patches itself on first request.** That happens either through an explicit call such as `DrakeFeatures.Enable(DrakeFeature.Looks, ownerGuid)` from the mod's Awake, or automatically on the first use of that feature's API.
- **Integration costs only appear when they're used.** Stacking or display arbitration between mods only switches on when the mods involved register. RenameIt alone gets the display hooks; LockSmith alone gets just the tab host and the UI kit.
- **The log shows what's on**, e.g. `Libs features on: Display(RenameIt), InventoryContext(LockSmith)`. Bug reports then say what was actually running.

---

## 4. Release coupling

- **Each Libs fix forces a Libs release plus a dependency bump in each mod.** Today's LockSmith beta waited on Libs 0.9.12 for two UI bugs. §2 removes that class of release.
- **No version policy is written down.** Suggestion:
  - **patch** = fixes,
  - **minor** = added API or new features,
  - **removals and breaking changes wait for 1.0**, with old signatures kept as `[Obsolete]` wrappers for one minor version.

---

## 5. Suggested order

1. **UI layout core** plus wrappers. This fixes the stiffness and the clipped popups for every mod, with no mod changes.
2. **Feature registration and dormant patches**, Looks first (spawn/equip risk), then Display, Stacks and Progression, then InventoryContext. Each mod adds one-line `Enable` calls.
3. **`internal` sweep**, and write the rule from "Your direction" into the Libs README.
4. A **UI gallery command** (`drake_ui_gallery`) to check every panel with few/many buttons and short/long text before each Libs release.

---

## Decisions (2026-10-07)

| # | Decision |
|---|---|
| G1/G3/G4/G7 | Shared or shareable stays in Libs; suite hooks stay; one package. |
| G2 | **Dynamic layout** (Unity layout groups + caller options). |
| G5 | **Wrap, don't break.** Keep current public signatures; breaking cleanup only at 1.0 with `[Obsolete]` wrappers. |
| G6 | **Features switch on from what mods already call** (registering a tab, display layer or look, or using the feature API). No new `Enable` call to forget. |
| G7b | **Arbitration only with 2+ registrants.** One mod = direct path, no arbitration. |
| G8 | No UI gallery command. |
| G9 | Version policy is a Claude rule: see `Vahleim/Mod/CLAUDE.md`. |
| G10 | Whoever needs to may change Libs (new public API gets a README line). |
| G11 | Download size **and** runtime count as bloat. |
