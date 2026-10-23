# DrakeModsLibs integration suites (DrakesTestLab)

Cross-mod tests: how the mods that are built to work together behave when both are installed.
Each suite needs only its two mods (`requireMods`), so a profile with some of them still runs the rest.

| Suite | Mods | What it covers |
|---|---|---|
| DrakeModsLibs-RenameIt-ItemShop | RenameIt + ItemShop | renamed items bought from a shop keep their name, description and crafter (and the owner lock); a renamed item on display; a written page sent by mail keeps its text; renamed and plain stacks stay apart |
| DrakeModsLibs-ReskinIt-ItemShop | ReskinIt + ItemShop | a reskinned item bought from a shop keeps its icon, tint and worn model |
| DrakeModsLibs-LockSmith-ItemShop | LockSmith + ItemShop | the Shop group (Buy, Layout) and Mail tab in the Lock menu; a LockSmith guest may open the shop chest but not run the shop; LockSmith public on shops and mailboxes |
| DrakeModsLibs-RenameIt-ReskinIt | RenameIt + ReskinIt | the NoRename tag blocks renaming but not reskinning; a renamed + reskinned item on a stand and on the ground |

`gen_integration_suites.py` writes `suites/`. The suites use each mod's own test hooks (Debug builds only), so build the mods
in Debug (`scripts/Test-All.ps1` does). `Test-All` copies these suites into the drakeTest profiles; `Tests/` is never packaged.

Real multiplayer (two clients) is still manual: these tests use TestLab's play-as players.
