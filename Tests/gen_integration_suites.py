"""Writes DrakeModsLibs' cross-mod integration suites into Tests/suites/ (run: python gen_integration_suites.py).

One suite per pair of mods that are designed to work together, each requiring only those two mods, so a profile with only some
of them still runs the rest. They use the mods' own test hooks (renameit.*, reskin.*, itemshop.*, mail.*, lock.*).
The build copies them into the test profiles; Tests/ is never packaged."""
import json, os, shutil

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites')
RI, RS, SHOP, LOCK = 'com.drakemods.renameit', 'com.drakemods.reskinit', 'com.drakemods.itemshop', 'com.drakemods.locksmith'
TEAMS, REPAIR, LIBS = 'com.drakemods.serverteams', 'com.drakemods.betterrepair', 'com.drakemods.libs'
SOURCES = {RI: 'DrakesRenameit', RS: 'DrakesReskinIt', SHOP: 'DrakesItemShop', LOCK: 'LockSmith',
           TEAMS: 'DrakesServerTeams', REPAIR: 'DrakesBetterRepair'}


def H(name, expect=None, label=None, contains=None, notContains=None, stop=False, **args):
    s = {'do': 'hook', 'name': name}
    if args:
        s['args'] = args
    for k, v in (('expect', expect), ('contains', contains), ('notContains', notContains), ('label', label)):
        if v is not None:
            s[k] = v
    if stop:
        s['stop'] = True
    return s


def check(kind, label=None, **f):
    s = {'do': 'check', 'check': kind, **f}
    if label:
        s['label'] = label
    return s


def play(who):
    return {'do': 'playAs', 'who': who}


def wait(s=0.3):
    return {'do': 'wait', 'seconds': s}


def craft(prefab, by=None):
    s = {'do': 'craft', 'prefab': prefab}
    if by:
        s['by'] = by
    return s


def give(prefab, count=1):
    return {'do': 'give', 'prefab': prefab, 'count': count}


def shop_case(owner='Alice', ward=False, tag='shop', dz=3, dx=0):
    s = [play(owner), {'do': 'place', 'prefab': 'Drake_DisplayCase', 'dz': dz, 'dx': dx, 'as': tag}]
    if ward:
        s.append({'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': owner, 'enabled': True, 'radius': 15})
    return s + [wait(0.5)]


suites = {}


def suite(name, mods, description, config=None):
    suites[name] = {'meta': {
        'name': name, 'description': description,
        'requireMods': [{'guid': g, 'minVersion': '0.1.0'} for g in mods],
        'configOverrides': [{'guid': g, 'section': k.split('/')[0], 'key': k.split('/')[1], 'value': v} for g, kv in (config or {}).items() for k, v in kv.items()],
        'setup': {'timeOfDay': 0.5, 'freezeTime': 1, 'god': 1, 'noBuild': 'off', 'hideUnlockPopups': 1},
        'resetBetweenTests': True}, 'tests': {}, 'mods': mods}


def T(suite_name, tid, title, steps, desc=''):
    s = suites[suite_name]
    s['tests'][tid] = {'name': f'{tid} {title}', 'description': desc, 'steps': steps,
                       'asserts': [{'check': 'noErrors', 'source': SOURCES[g]} for g in s['mods']]}


RI_BASE = {'01 Admin/AllowAdminOverride': 'false', '07 UnlockCost/UnlockCostEnabled': 'false', '05 General/LockToOwner': 'true',
           '06 Stacks/SeparateStacks': 'true', '06 Stacks/SeparateStacksHardLock': 'true', '03 Exclusions/ExcludedNames': '',
           '03 Exclusions/ExcludedCategory': '', '11 UI-NotSynced/MenuOpenModifier': ''}
RS_BASE = {'02 - Admin/AdminSource': 'Own', '02 - Admin/AllowAdminOverride': 'false', '03 - Features/RequireDiscovery': 'Off',
           '03 - Features/ProgressionGate': 'false', '05 - Cost/CostEnabled': 'false', '04 - Exclusions/ExclusionSource': 'Own'}

# ---------------------------------------------------------------- RenameIt x ItemShop
A = 'DrakeModsLibs-RenameIt-ItemShop'
suite(A, [RI, SHOP], 'Renamed items in shops and mail: names, descriptions, crafter and paper text travel with the item; separate stacks on shelves.',
      {RI: RI_BASE})

T(A, 'I-01', 'a renamed item sold in a shop arrives renamed', [
    *shop_case(),
    craft('AxeStone'),
    H('renameit.edit', True, prefab='AxeStone', op='name', text='Alice Axe'),
    H('renameit.edit', True, prefab='AxeStone', op='desc', text='Forged with care'),
    H('itemshop.putIn', True, 'Alice puts her renamed axe on the shelf', stop=True, target='shop', prefab='AxeStone', price='c:5'),
    H('itemshop.open', True, target='shop', on=True),
    play('Bob'),
    give('Coins', 5),
    H('itemshop.buy', '', 'Bob buys it', stop=True, target='shop', prefab='AxeStone'),
    H('renameit.name', label='the name came with it', contains='Alice Axe', prefab='AxeStone'),
    H('renameit.desc', label='and the description', contains='Forged with care', prefab='AxeStone'),
    H('renameit.owner', 'Alice', 'the crafter is still Alice', prefab='AxeStone'),
    H('renameit.can', False, 'so Bob cannot rename it (owner lock)', prefab='AxeStone', op='name'),
    play('me'),
])

T(A, 'I-02', 'a renamed item on display shows its name', [
    *shop_case(),
    craft('SwordBronze'),
    H('renameit.edit', True, prefab='SwordBronze', op='name', text='Shop Sword'),
    H('itemshop.putIn', True, target='shop', prefab='SwordBronze', price='c:5'),
    H('itemshop.open', True, target='shop', on=True),
    wait(0.5),
    play('Bob'),
    {'do': 'lookAt', 'target': 'shop', 'distance': 2},
    check('crosshair', 'observed: what the crosshair shows on the case (informational)', notContains='\u0001'),
    {'do': 'screenshot', 'name': 'renamed-on-display'},
    play('me'),
], desc='How the case presents the renamed goods: screenshot plus the crosshair text.')

T(A, 'I-03', 'a written page sent by mail keeps its text', [
    play('Bob'),
    {'do': 'place', 'prefab': 'Drake_Mailbox', 'dz': 2.5, 'as': 'box'},
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Bob', 'enabled': True, 'radius': 15},
    wait(0.5),
    play('Alice'),
    give('Drakes_PieceOfPaper', 1),
    H('renameit.edit', True, stop=True, prefab='Drakes_PieceOfPaper', op='name', text='Love Letter'),
    H('renameit.edit', True, prefab='Drakes_Paper_Written', op='desc', text='Meet me at the shop'),
    H('mail.send', '', 'Alice posts the page', stop=True, target='box', prefab='Drakes_Paper_Written', count=1),
    play('Bob'),
    H('mail.takeAll', 1, 'Bob takes his mail', target='box'),
    H('renameit.name', label='the page keeps its name', contains='Love Letter', prefab='Drakes_Paper_Written'),
    H('renameit.desc', label='and its text', contains='Meet me at the shop', prefab='Drakes_Paper_Written'),
    play('me'),
])

T(A, 'I-04', 'renamed and plain stacks stay apart on the shelf and after buying', [
    *shop_case(),
    give('Wood', 20),
    H('renameit.split', True, prefab='Wood', amount=10),
    H('renameit.edit', True, prefab='Wood', index=0, op='name', text='Oak Planks'),
    H('itemshop.putIn', True, target='shop', prefab='Wood', index=0, price='c:1'),
    H('itemshop.putIn', True, target='shop', prefab='Wood', index=0, price='c:1'),
    H('itemshop.open', True, target='shop', on=True),
    play('Bob'),
    give('Coins', 2),
    H('itemshop.stockCount', 20, 'all 20 wood on the shelf', target='shop', prefab='Wood'),
    H('itemshop.buy', '', 'Bob buys one from the first stack', target='shop', prefab='Wood', index=0),
    H('itemshop.buy', '', 'and one from the second', target='shop', prefab='Wood', index=1),
    H('renameit.stackCount', 2, 'renamed and plain wood stay separate stacks (SeparateStacks)', prefab='Wood'),
    H('renameit.name', label='one of them is the Oak Planks', contains='Oak Planks', prefab='Wood', match='Oak'),
    play('me'),
])

# ---------------------------------------------------------------- ReskinIt x ItemShop
B = 'DrakeModsLibs-ReskinIt-ItemShop'
suite(B, [RS, SHOP], 'Reskinned items in shops: the look (icon, model, colours) travels with the item and shows on display.', {RS: RS_BASE})

T(B, 'I-10', 'a reskinned item sold in a shop keeps its look', [
    *shop_case(),
    craft('AxeStone'),
    H('reskin.apply', '', prefab='AxeStone', icon='AxeFlint', model='AxeFlint', iconTint='#ff0000', modelTint='#00ff00'),
    H('itemshop.putIn', True, stop=True, target='shop', prefab='AxeStone', price='c:5'),
    H('itemshop.open', True, target='shop', on=True),
    wait(0.5),
    {'do': 'screenshot', 'name': 'reskinned-on-display'},
    play('Bob'),
    give('Coins', 5),
    H('itemshop.buy', '', stop=True, target='shop', prefab='AxeStone'),
    H('reskin.icon', label='icon override arrived', contains='AxeFlint', prefab='AxeStone'),
    H('reskin.iconTint', '#FF0000', prefab='AxeStone'),
    H('reskin.equip', True, prefab='AxeStone'),
    wait(0.5),
    H('reskin.wornModel', 'AxeFlint', 'Bob holds the flint axe model', slot='right'),
    H('reskin.wornTint', '#00FF00', 'drawn green', slot='right'),
    play('me'),
])

# ---------------------------------------------------------------- LockSmith x ItemShop
C = 'DrakeModsLibs-LockSmith-ItemShop'
suite(C, [LOCK, SHOP], "ItemShop's tabs inside LockSmith's Lock menu (smart Shop group, Mail), and LockSmith access on shops and mailboxes.",
      {LOCK: {'02 Features/EnableChests': 'true', '02 Features/EnableManagedAccess': 'true', '02 Features/RequireActiveWard': 'true',
              '02 Features/EnableOptInAccess': 'true', '02 Features/EnablePieceGuests': 'true', '02 Features/EnableDesignate': 'false'}})

T(C, 'I-20', 'the Lock menu on a shop shows the Shop group; Buy and Layout open under it', [
    *shop_case(ward=True),
    H('lock.openMenu', True, 'AltPlace+E on the case opens the Lock menu', stop=True, target='shop'), wait(0.4),
    check('uiVisible', 'the Shop tab is there', name='Shop', exact=True),
    check('uiVisible', 'next to Layout', name='Layout', exact=True),
    check('uiVisible', 'and the Lock tab (LockSmith)', name='Lock', exact=True),
    check('uiVisible', 'no Buy tab while buying is off', name='Buy', exact=True, expect=False),
    H('lock.closeMenu'),
    H('buy.addOffer', True, 'turn on buying (an offer for wood)', target='shop', want='Wood', amount=10, pay=5),
    H('lock.openMenu', True, target='shop'), wait(0.4),
    {'do': 'click', 'button': 'Shop'}, wait(0.3),
    check('uiVisible', 'with buying on, the Buy tab joins the Shop group', name='Buy', exact=True),
    {'do': 'screenshot', 'name': 'lock-menu-shop-tabs'},
    H('lock.closeMenu'),
    play('me'),
])

T(C, 'I-21', 'the Lock menu on a mailbox shows the Mail tab', [
    play('Alice'),
    {'do': 'place', 'prefab': 'Drake_Mailbox', 'dz': 2.5, 'as': 'box'},
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15},
    wait(0.5),
    H('lock.openMenu', True, stop=True, target='box'), wait(0.4),
    check('uiVisible', 'the Mail tab is there', name='Mail'),
    check('uiVisible', 'no Shop tab on a mailbox', name='Shop', exact=True, expect=False),
    H('lock.closeMenu'),
    play('me'),
])

T(C, 'I-22', 'a LockSmith guest on a shop may open its chest but not run the shop', [
    *shop_case(ward=True),
    H('itemshop.stock', True, target='shop', prefab='SwordBronze', count=1, price='c:5'),
    H('itemshop.open', True, target='shop', on=True),
    H('lock.openJoin', True, target='shop', on=True),
    play('Bob'),
    H('itemshop.canOpenChest', False, 'a stranger cannot open the shop chest', target='shop'),
    H('lock.openMenu', True, target='shop'), wait(0.4),
    check('uiEnabled', 'a stranger sees the Shop tab but cannot switch it', name='Open for business', expect=False),
    {'do': 'click', 'button': 'Lock'}, wait(0.3),
    {'do': 'click', 'button': 'Join access'}, wait(0.5),
    H('lock.closeMenu'),
    H('lock.isGuest', True, 'Bob joined as a LockSmith guest', target='shop'),
    H('itemshop.canOpenChest', True, 'a guest may open the chest (LockSmith)', target='shop'),
    H('itemshop.canManage', False, 'but may not run the shop (ward permit needed)', target='shop'),
    give('Coins', 5),
    H('itemshop.buy', '', 'and can still buy', target='shop', prefab='SwordBronze'),
    play('me'),
])

T(C, 'I-23', 'LockSmith public on a shop and on a mailbox', [
    *shop_case(ward=True),
    H('lock.setPublic', True, 'Alice makes the shop chest public', target='shop', on=True),
    {'do': 'place', 'prefab': 'Drake_Mailbox', 'dz': 3, 'dx': 2.5, 'as': 'box'},
    wait(0.5),
    play('Bob'),
    H('itemshop.canOpenChest', True, 'anyone may open a public shop chest', target='shop'),
    H('itemshop.canManage', False, 'but not run the shop', target='shop'),
    play('Alice'),
    H('lock.setPublic', label='observed: may a mailbox be made public with LockSmith? (informational)', target='box', on=True),
    play('Bob'),
    H('mail.canOpen', label='observed: can a stranger then read the mail? (informational: privacy question)', target='box'),
    play('me'),
], desc='Whether LockSmith public should apply to a mailbox is a design question; this test records what happens.')

# ---------------------------------------------------------------- RenameIt x ReskinIt (libs-level: tags, both on one item, stands)
D = 'DrakeModsLibs-RenameIt-ReskinIt'
suite(D, [RI, RS], 'Shared item data: tag rules that block renaming, a renamed + reskinned item on a stand and on the ground.', {RI: RI_BASE, RS: RS_BASE})

T(D, 'I-30', 'tag rules: NoRename blocks renaming, not reskinning', [
    craft('AxeStone'),
    H('renameit.setData', True, prefab='AxeStone', key='Drake_NoRename', value='1'),
    H('renameit.can', False, 'the NoRename tag blocks renaming', prefab='AxeStone', op='name'),
    H('reskin.can', True, 'reskinning is a different operation', prefab='AxeStone', kind='icon'),
    H('renameit.setData', True, prefab='AxeStone', key='Drake_NoRename', value=''),
    H('renameit.can', True, 'tag removed: renaming allowed again', prefab='AxeStone', op='name'),
])

T(D, 'I-31', 'a renamed + reskinned item on a stand and on the ground', [
    craft('AxeStone'),
    H('renameit.edit', True, prefab='AxeStone', op='name', text='Twin Axe'),
    H('reskin.apply', '', prefab='AxeStone', model='AxeFlint', modelTint='#ff00ff'),
    {'do': 'place', 'prefab': 'itemstandh', 'dz': 2.5, 'as': 'stand'},
    H('renameit.standPut', True, stop=True, prefab='AxeStone', target='stand'),
    wait(0.5),
    H('renameit.standHover', label='the stand shows the custom name', contains='Twin Axe', target='stand'),
    {'do': 'screenshot', 'name': 'stand-renamed-reskinned'},
    H('renameit.standTake', True, target='stand'),
    wait(0.5),
    check('hasItem', 'the stone axe came back off the stand', prefab='AxeStone'),
    check('noItem', 'not a flint axe (the look is only a look)', prefab='AxeFlint'),
    check('objectNear', 'nothing left lying around the stand', target='AxeFlint', radius=5, expect=False),
    H('reskin.model', label='the look survived the stand', contains='AxeFlint', prefab='AxeStone'),
    H('renameit.name', label='and the name', contains='Twin Axe', prefab='AxeStone'),
    {'do': 'drop', 'prefab': 'AxeStone', 'as': 'ground', 'dz': 1.5},
    wait(0.8),
    {'do': 'lookAt', 'target': 'ground', 'distance': 1.5},
    check('uiVisible', 'the item on the ground shows its custom name', name='Twin Axe'),
    {'do': 'screenshot', 'name': 'ground-renamed-reskinned'},
])

# ---------------------------------------------------------------- RenameIt x LockSmith x ItemShop (the real Shift+E path, not lock.openMenu)
E = 'DrakeModsLibs-RenameIt-LockSmith'
suite(E, [RI, LOCK, SHOP],
      'Shift+E on a placed piece goes through the player, the way the game does it. LockSmith and ItemShop tabs stay, and rename is the Name tab. A piece with no Lock menu still opens the RenameIt workshop.',
      {RI: {**RI_BASE, '12 Pieces/PieceRenameEnabled': 'true', '11 UI-NotSynced/UseToolkitUi': 'true'},
       LOCK: {'02 Features/EnableDesignate': 'false', '02 Features/EnableChests': 'true', '02 Features/EnableDoors': 'true',
              '02 Features/EnableManagedAccess': 'true', '02 Features/RequireActiveWard': 'true', '04 Display/UseWheel': 'false'}})

def shift_use(target):
    return {'do': 'interact', 'target': target, 'alt': True, 'hold': False, 'asPlayer': True}

T(E, 'I-40', 'Shift+E on a chest opens the Lock menu, with Name and Ward, not the rename-only window', [
    play('Alice'),
    {'do': 'place', 'prefab': 'piece_chest_wood', 'dz': 2.5, 'as': 'chest'},
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15, 'as': 'ward'},
    wait(0.5),
    shift_use('chest'),
    wait(0.4),
    H('lock.menuOpen', True, 'the Lock menu opened', stop=True),
    H('renameit.pieceWindow', False, 'the rename-only workshop did not take the key'),
    check('uiVisible', 'the Lock tab is there', name='Lock', exact=True),
    check('uiVisible', 'the Name tab is there', name='Name', exact=True),
    check('uiVisible', 'the Ward tab is there', name='Ward', exact=True),
    {'do': 'click', 'button': 'Name'},
    wait(0.3),
    {'do': 'type', 'field': 'Name', 'text': 'Pantry'},
    {'do': 'click', 'button': 'Save'},
    wait(0.3),
    H('renameit.pieceName', 'Pantry', 'the Name tab saved the piece name', target='chest'),
    {'do': 'screenshot', 'name': 'chest-name-ward-tabs'},
    H('lock.closeMenu'),
])

T(E, 'I-41', 'Shift+E on a door opens the Lock menu with Name and Ward', [
    play('Alice'),
    {'do': 'place', 'prefab': 'wood_door', 'dz': 2.5, 'as': 'door'},
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15},
    wait(0.5),
    shift_use('door'),
    wait(0.4),
    H('lock.menuOpen', True, 'the Lock menu opened on the door', stop=True),
    H('renameit.pieceWindow', False, 'the workshop did not take the door'),
    check('uiVisible', 'Lock', name='Lock', exact=True),
    check('uiVisible', 'Name', name='Name', exact=True),
    check('uiVisible', 'Ward', name='Ward', exact=True),
    H('lock.closeMenu'),
])

T(E, 'I-42', 'Shift+E on a shop opens on Shop, with Layout and Name beside it; Lock still has Ward', [
    play('Alice'),
    {'do': 'place', 'prefab': 'Drake_DisplayCase', 'dz': 3, 'as': 'shop'},
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15},
    wait(0.5),
    shift_use('shop'),
    wait(0.4),
    H('lock.menuOpen', True, 'the Lock menu opened on the case', stop=True),
    H('renameit.pieceWindow', False, 'the workshop did not take the shop'),
    check('uiVisible', 'Shop is the opening tab', name='Shop', exact=True),
    check('uiVisible', 'Layout is visible with Shop', name='Layout', exact=True),
    check('uiVisible', 'Name is on the row, not hidden under Lock', name='Name', exact=True),
    check('uiVisible', 'and Lock', name='Lock', exact=True),
    {'do': 'click', 'button': 'Lock'},
    wait(0.3),
    check('uiVisible', 'Ward shows with the Lock group', name='Ward', exact=True),
    check('uiVisible', 'Name stays visible', name='Name', exact=True),
    {'do': 'screenshot', 'name': 'shop-lock-name-ward'},
    H('lock.closeMenu'),
])

T(E, 'I-44', 'Shift+E on a Karve opens the workshop on the piece name, not that name plus E', [
    play('Alice'),
    {'do': 'spawn', 'prefab': 'Karve', 'dz': 3, 'as': 'ship'},
    wait(0.5),
    {'do': 'hook', 'name': 'renameit.armPieceOpeningKey'},
    {'do': 'holdKey', 'key': 'E'},
    shift_use('ship'),
    wait(0.2),
    {'do': 'pressKey', 'button': 'Use', 'frames': 8},
    {'do': 'releaseKeys'},
    wait(0.5),
    H('renameit.pieceWindow', True, 'the workshop opened', stop=True),
    H('lock.menuOpen', False, 'a ship has no Lock menu'),
    H('renameit.pieceWindowName', label='the name is the default, with no stray E'),
    {'do': 'hook', 'name': 'renameit.closeWindows'},
    {'do': 'closeUi'},
])

T(E, 'I-43', 'a workbench has no Lock menu, so Shift+E still opens the piece workshop', [
    play('Alice'),
    {'do': 'place', 'prefab': 'piece_workbench', 'dz': 2.5, 'as': 'bench'},
    wait(0.4),
    shift_use('bench'),
    wait(0.4),
    H('renameit.pieceWindow', True, 'the workshop opened', stop=True),
    H('lock.menuOpen', False, 'LockSmith has nothing to do on a workbench'),
    {'do': 'hook', 'name': 'renameit.closeWindows'},
    {'do': 'closeUi'},
])

# ---------------------------------------------------------------- every mod on the one piece Shift+E (stays in Tests/; not packaged)
ALL = 'DrakeModsLibs-AllMods'
suite(ALL, [RI, RS, TEAMS, LOCK, REPAIR, SHOP],
      'One Shift+E for a placed piece. Tab order comes from DrakeModsLibs Integration/PieceTabOrder. '
      'A Team Ward uses that same Lock menu and LockSmith keeps its own switches locked to server admins. '
      'Rename, Reskin and Repair share an item without taking the piece key from each other.',
      {RI: {**RI_BASE, '12 Pieces/PieceRenameEnabled': 'true', '11 UI-NotSynced/UseToolkitUi': 'true'},
       RS: RS_BASE,
       LOCK: {'02 Features/EnableDesignate': 'false', '02 Features/EnableChests': 'true', '02 Features/EnableDoors': 'true',
              '02 Features/EnableManagedAccess': 'true', '02 Features/RequireActiveWard': 'true', '04 Display/UseWheel': 'false'}})

T(ALL, 'I-50', 'a chest Shift+E is the Lock menu, in the Libs tab order, and the workshop stays closed', [
    play('Alice'),
    {'do': 'place', 'prefab': 'piece_chest_wood', 'dz': 2.5, 'as': 'chest'},
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15},
    wait(0.5),
    shift_use('chest'),
    wait(0.4),
    H('lock.menuOpen', True, 'one Lock menu', stop=True),
    H('renameit.pieceWindow', False, 'rename did not take Shift+E'),
    H('lock.tabOrder', label='Lock before Ward before Name', contains='Lock,Ward'),
    H('lock.tabOrder', label='Ward is not in front of Lock', notContains='Ward,Lock'),
    check('uiVisible', 'Name is on that menu', name='Name', exact=True),
    H('lock.closeMenu'),
])

T(ALL, 'I-51', 'PieceTabOrder in the Libs config is the order the menu uses', [
    {'do': 'setConfig', 'guid': LIBS, 'values': {
        'Integration/PieceTabOrder': 'locksmith.ward;locksmith;renameit.piece;locksmith.groups'}},
    play('Alice'),
    {'do': 'place', 'prefab': 'piece_chest_wood', 'dz': 2.5, 'as': 'chest'},
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15},
    wait(0.5),
    shift_use('chest'),
    wait(0.4),
    H('lock.menuOpen', True, stop=True),
    H('lock.tabOrder', label='Ward, then Lock, then Name', contains='Ward,Lock,Name'),
    H('lock.closeMenu'),
])

T(ALL, 'I-52', 'a shop Shift+E opens on Shop, with Layout and Name, and Lock still has Ward', [
    play('Alice'),
    {'do': 'place', 'prefab': 'Drake_DisplayCase', 'dz': 3, 'as': 'shop'},
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15},
    wait(0.5),
    shift_use('shop'),
    wait(0.4),
    H('lock.menuOpen', True, stop=True),
    H('renameit.pieceWindow', False),
    check('uiVisible', 'Shop', name='Shop', exact=True),
    check('uiVisible', 'Layout', name='Layout', exact=True),
    check('uiVisible', 'Name', name='Name', exact=True),
    check('uiVisible', 'Lock', name='Lock', exact=True),
    {'do': 'click', 'button': 'Lock'},
    wait(0.3),
    check('uiVisible', 'Ward', name='Ward', exact=True),
    check('uiVisible', 'Name', name='Name', exact=True),
    H('lock.closeMenu'),
])

T(ALL, 'I-53', 'Shift+E on a Team Ward is the Lock menu, and only an admin can change it', [
    play('Alice'),
    H('teams.defineTeam', team='red', title='Red'),
    H('teams.setAdmin', on=True),
    {'do': 'place', 'prefab': 'TeamWard', 'dz': 3, 'as': 'ward'},
    wait(1.0),
    shift_use('ward'),
    wait(0.4),
    H('lock.teamMenu', True, 'the shared menu opened', stop=True),
    H('lock.menuOpen', True),
    H('teams.wardPanelOpen', False, 'the separate admin window stayed closed'),
    H('renameit.pieceWindow', False, 'rename did not take the ward'),
    H('lock.teamLocked', False, 'an admin can change what it protects'),
    H('lock.tabOrder', label='Team before Lock', contains='Team,Lock'),
    H('teams.adminCommand', line='ward team red'),
    wait(0.5),
    check('uiVisible', 'Team', name='Team', exact=True),
    check('uiVisible', 'Lock', name='Lock', exact=True),
    check('uiVisible', 'Team managed', name='Team managed'),
    check('uiVisible', 'Protect build', name='Protect build'),
    check('uiVisible', 'the ward is Red', name='Red', exact=True),
    {'do': 'click', 'button': 'Lock'},
    wait(0.3),
    check('uiVisible', 'Ward on', name='Ward on'),
    check('uiVisible', 'who may change it', name='Who can change this ward: server admins.'),
    H('teams.setAdmin', on=False),
    {'do': 'click', 'button': 'Team'},
    wait(0.4),
    H('lock.teamLocked', True, 'everyone else only gets the locked menu'),
    check('uiVisible', 'told why', name='Only admins can change a Team Ward.'),
    H('teams.setAdmin', on=True),
    wait(0.5),
    H('lock.teamLocked', False),
    H('lock.closeMenu'),
])

T(ALL, 'I-54', 'rename, reskin and repair all keep the same item', [
    {'do': 'setConfig', 'guid': REPAIR, 'values': {'4 - Repair cost (coins)/UseCoinRepair': True}},
    play('Alice'),
    craft('AxeStone'),
    H('renameit.edit', True, prefab='AxeStone', op='name', text='Twin Axe'),
    H('reskin.apply', '', prefab='AxeStone', model='AxeFlint'),
    H('reskin.tabAvailable', True, 'Reskin still offers its tab', prefab='AxeStone'),
    H('renameit.can', True, 'Rename still offers its tab', prefab='AxeStone', op='name'),
    {'do': 'place', 'prefab': 'piece_workbench', 'dz': 2.5, 'as': 'bench'},
    wait(0.4),
    H('repair.damage', prefab='AxeStone', percent=40),
    give('Coins', 200),
    H('repair.useStation', prefab='piece_workbench'),
    wait(0.4),
    H('repair.perform', True, 'it repairs', prefab='AxeStone'),
    H('repair.durability', 100, prefab='AxeStone'),
    H('renameit.name', label='the name survived', contains='Twin Axe', prefab='AxeStone'),
    H('reskin.model', label='the look survived', contains='AxeFlint', prefab='AxeStone'),
    H('reskin.tabAvailable', True, prefab='AxeStone'),
    H('renameit.can', True, prefab='AxeStone', op='name'),
    {'do': 'closeUi'},
])

T(ALL, 'I-55', 'a workbench still opens the workshop, not the Lock menu', [
    play('Alice'),
    {'do': 'place', 'prefab': 'piece_workbench', 'dz': 2.5, 'as': 'bench'},
    wait(0.4),
    shift_use('bench'),
    wait(0.4),
    H('renameit.pieceWindow', True, 'the workshop opened', stop=True),
    H('lock.menuOpen', False, 'LockSmith has nothing to do on a workbench'),
    H('lock.teamMenu', False),
    {'do': 'hook', 'name': 'renameit.closeWindows'},
    {'do': 'closeUi'},
])

# ---------------------------------------------------------------- write
for name, s in suites.items():
    folder = os.path.join(OUT, name)
    if os.path.isdir(folder):
        shutil.rmtree(folder)
    os.makedirs(folder)
    with open(os.path.join(folder, 'suite.json'), 'w', encoding='utf-8') as f:
        json.dump(s['meta'], f, indent=2, ensure_ascii=False)
    for tid, t in s['tests'].items():
        with open(os.path.join(folder, f'{tid}.json'), 'w', encoding='utf-8') as f:
            json.dump(t, f, indent=2, ensure_ascii=False)
    print(f"{name}: {len(s['tests'])} tests")
