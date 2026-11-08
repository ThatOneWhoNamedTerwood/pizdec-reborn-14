import re

root = r'C:\Users\asvfo\pizdec-reborn-14'

# component: equipped mask
p = root + r'\Content.Server\PizReb\Cyberware\AutoSurgeonComponent.cs'
d = open(p, encoding='utf-8').read()
if 'EquippedMask' not in d:
    d = d.replace('    [ViewVariables]\n    public bool Pending;', '    [ViewVariables]\n    public EntityUid? EquippedMask;\n\n    [ViewVariables]\n    public bool Pending;', 1)
open(p, 'w', encoding='utf-8', newline='').write(d)

# prototype: add DoAfter component
p = root + r'\Resources\Prototypes\PizReb\Cyberware\autosurgeon.yml'
d = open(p, encoding='utf-8').read()
if 'type: DoAfter' not in d:
    d = d.replace('  - type: AutoSurgeon\n', '  - type: AutoSurgeon\n  - type: DoAfter\n', 1)
open(p, 'w', encoding='utf-8', newline='').write(d)

# system
p = root + r'\Content.Server\PizReb\Cyberware\AutoSurgeonSystem.cs'
d = open(p, encoding='utf-8').read()

if 'using Content.Shared.Inventory;' not in d:
    d = d.replace('using Content.Shared.Interaction;', 'using Content.Shared.Inventory;\nusing Content.Shared.Interaction;')
if 'using Content.Shared.Chat;' not in d:
    d = d.replace('using Content.Shared.Clothing.Components;', 'using Content.Shared.Chat;\nusing Content.Shared.Clothing.Components;')

d = d.replace('    [Dependency] private IRobustRandom _random = default!;',
              '    [Dependency] private IRobustRandom _random = default!;\n    [Dependency] private InventorySystem _inventory = default!;', 1)

old = '''        if (!TryPrepareAnesthesia(ent, patient))
            return;

        ent.Comp.Operating = true;'''
new = '''        if (!TryPrepareAnesthesia(ent, patient))
            return;

        EquipMask(ent, patient);
        ent.Comp.Operating = true;'''
assert old in d
d = d.replace(old, new, 1)

old = '''    private bool TryPrepareAnesthesia(Entity<AutoSurgeonComponent> ent, EntityUid patient)'''
new = '''    private void EquipMask(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        foreach (var item in ent.Comp.Contents)
        {
            if (!HasComp<ClothingComponent>(item))
                continue;
            if (_inventory.TryEquip(patient, item, "mask", silent: true, force: true))
                ent.Comp.EquippedMask = item;
            return;
        }
    }

    private void UnequipMask(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        if (ent.Comp.EquippedMask == null)
            return;
        _inventory.TryUnequip(patient, "mask", out _, silent: true, force: true);
        ent.Comp.EquippedMask = null;
    }

    private bool TryPrepareAnesthesia(Entity<AutoSurgeonComponent> ent, EntityUid patient)'''
assert old in d
d = d.replace(old, new, 1)

old = '''                if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, patient, table.PendingDuration, new AutoSurgeonStepEvent(), uid)'''
new = '''                if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, table.PendingDuration, new AutoSurgeonStepEvent(), uid)'''
assert old in d
d = d.replace(old, new, 1)

old = '''        if (!_cyberware.TryInstallImplant(patient, implant.Value, out var replaced))
            return;
        if (replaced != null)
            ent.Comp.Contents.Add(replaced.Value);'''
new = '''        if (!_cyberware.TryInstallImplant(patient, implant.Value, out var replaced))
            return;
        ent.Comp.Contents.Remove(implant.Value);
        if (replaced != null)
            StoreInTable(ent, replaced.Value);'''
assert old in d
d = d.replace(old, new, 1)

old = '''            if (ent.Comp.SelectedImplant != null && TryGetEntity(ent.Comp.SelectedImplant.Value, out var remove) &&
                _cyberware.TryRemoveImplant(patient, remove.Value))
                ent.Comp.Contents.Add(remove.Value);
            return;'''
new = '''            if (ent.Comp.SelectedImplant != null && TryGetEntity(ent.Comp.SelectedImplant.Value, out var remove) &&
                _cyberware.TryRemoveImplant(patient, remove.Value))
                StoreInTable(ent, remove.Value);
            return;'''
assert old in d
d = d.replace(old, new, 1)

old = '''    private void FinishOperation(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {'''
new = '''    private void StoreInTable(Entity<AutoSurgeonComponent> ent, EntityUid item)
    {
        var container = _cyberware.EnsureContainer(ent.Owner);
        _container.Insert(item, container);
        ent.Comp.Contents.Add(item);
    }

    private void FinishOperation(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        UnequipMask(ent, patient);'''
assert old in d
d = d.replace(old, new, 1)

old = '''    private void FinishRevert(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {'''
new = '''    private void FinishRevert(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        UnequipMask(ent, patient);'''
assert old in d
d = d.replace(old, new, 1)

old = '''        _popup.PopupEntity(Loc.GetString(message), patient, patient, PopupType.Large);
        if (_players.TryGetSessionByEntity(patient, out var session))
            _chat.DispatchServerMessage(session, Loc.GetString(message));'''
new = '''        var text = Loc.GetString(message);
        _popup.PopupEntity(text, patient, patient, PopupType.Large);
        _chat.TrySendInGameICMessage(patient, text, InGameICChatType.Speak, ChatTransmitRange.None, ignoreActionBlocker: true);'''
assert old in d
d = d.replace(old, new, 1)

open(p, 'w', encoding='utf-8', newline='').write(d)
print('patched')
