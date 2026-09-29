"""Safely add the platforming/underground objects to the CURRENT TestLevel.

Unlike level_build.py, this does not regenerate the level: it preserves all
existing objects, their fileIDs, player configuration and later project art.
Run from the project root with Unity closed: py Concepts/apply_platforming.py
A one-time .before-platforming.bak copy of the scene is made first.
"""

import argparse
import os
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Assets/Scenes/TestLevel.unity'
TEMPLATE = Path(__file__).with_name('platforming_additions.txt')
DOC = re.compile(r'^--- !u!(\d+) &(\d+)\n(?:(?!--- !u!).)*', re.M | re.S)
SCENE_ROOTS = re.compile(r'^--- !u!1660057539 &\d+\nSceneRoots:\n(?:(?!--- !u!).)*', re.M | re.S)


def documents(text):
    docs = {}
    for match in DOC.finditer(text):
        fid = int(match.group(2))
        if fid in docs:
            raise ValueError(f'Duplicate Unity fileID {fid}')
        docs[fid] = (int(match.group(1)), match.group(0))
    return docs


def replace_once(text, old, new, label):
    if text.count(old) != 1:
        raise ValueError(f'{label}: expected exactly one occurrence of {old!r}')
    return text.replace(old, new, 1)


def set_document(scene, fid, old_block, new_block):
    return replace_once(scene, old_block, new_block, f'fileID {fid}')


def check_assets():
    expected = {
        'Assets/Art/Environment/tree_roots.png.meta': 'cc85d957593e436998557368842e4502',
        'Assets/Art/Environment/secret_crystal.png.meta': 'a19548bbd0474e568263a15f636d6c8d',
        'Assets/Scripts/MovingPlatform.cs.meta': '7e36cb6efa424810af7c88a1c29ab6b9',
        'Assets/Scripts/CrumblingPlatform.cs.meta': 'caac8375845f4a9baaf6451b0eb64ea5',
        'Assets/Scripts/SecretPassage.cs.meta': '1e5cb70f0925466b9c26ac0e35361f4b',
        'Assets/Scripts/CollectibleCrystal.cs.meta': '562d83646464472a92bc4a15fa70c885',
    }
    for path, guid in expected.items():
        file = ROOT / path
        if not file.exists() or f'guid: {guid}' not in file.read_text(encoding='utf-8'):
            raise ValueError(f'Missing asset or GUID mismatch: {file}')
        if not file.with_suffix('').exists():
            raise ValueError(f'Missing asset for {file}')


def scene_roots(text, old_roots):
    """Keep every original root, including added decorations after SceneRoots."""
    docs = documents(text)
    root_ids = []
    for fid, (kind, block) in docs.items():
        if kind != 4 or 'm_Father: {fileID: 0}' not in block:
            continue
        gid = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', block).group(1))
        if gid not in docs:
            raise ValueError(f'Transform {fid} has no GameObject {gid}')
        root_ids.append(gid)
    listed = [int(x) for x in re.findall(r'^  - \{fileID: (\d+)\}$', old_roots, re.M)]
    if not set(listed) <= set(root_ids):
        raise ValueError('Existing SceneRoots contain unknown/non-root objects')
    ids = list(dict.fromkeys(listed + root_ids))
    return (text.rstrip('\n') +
            '\n--- !u!1660057539 &9223372036854775807\nSceneRoots:\n'
            '  m_ObjectHideFlags: 0\n  m_Roots:\n' +
            ''.join(f'  - {{fileID: {gid}}}\n' for gid in ids))


def apply(scene):
    check_assets()
    original = scene.read_text(encoding='utf-8')
    current = documents(original)
    additions = documents(TEMPLATE.read_text(encoding='utf-8'))
    names = ('Ground_Raised', 'Ground_Start_East', 'Ground_Landing',
             'Platform_Moving', 'Platform_Crumbling', 'Underground_TreeRoots',
             'Secret_FalseWall', 'Secret_Shelf', 'Secret_Crystal')
    existing_names = re.findall(r'^  m_Name: (.*)$', original, re.M)
    if all(name in existing_names for name in names):
        print('Already added; scene unchanged.')
        return
    if any(name in existing_names for name in names):
        raise ValueError('Some platforming objects already exist; refusing partial overwrite')
    collisions = set(current) & set(additions)
    if collisions:
        raise ValueError(f'Unity fileID collision: {sorted(collisions)[:10]}')
    required = {210000000: 'Ground_Start', 210000001: 'Transform:',
                210000002: 'SpriteRenderer:', 210000003: 'BoxCollider2D:'}
    for fid, marker in required.items():
        if fid not in current or marker not in current[fid][1]:
            raise ValueError(f'Unexpected TestLevel: missing {marker} at {fid}')
    if 'Ground_Low' not in existing_names or 'Ground_Crest' not in existing_names:
        raise ValueError('Not the expected TestLevel scene')
    roots = list(SCENE_ROOTS.finditer(original))
    if len(roots) != 1:
        raise ValueError(f'Expected exactly one SceneRoots block, got {len(roots)}')

    # Only shorten the existing starting ground. Do not rebuild the rest of
    # the user's scene (later characters, decorations and other edits).
    changes = {
        210000001: [('m_LocalPosition: {x: -25.5, y: 0.2266, z: 0}',
                     'm_LocalPosition: {x: -28.7, y: 0.2266, z: 0}')],
        210000002: [('m_Size: {x: 13.0, y: 2.933594}',
                     'm_Size: {x: 6.6, y: 2.933594}')],
        210000003: [('m_Size: {x: 13.0, y: 2.5}',
                     'm_Size: {x: 6.6, y: 2.5}')],
        240000191: [('m_LocalPosition: {x: -25.0, y: 0.0, z: -0.5}',
                     'm_LocalPosition: {x: -25.0, y: 1.8, z: -0.5}')],
        240000501: [('m_LocalPosition: {x: -25.0, y: 0.0, z: -0.5}',
                     'm_LocalPosition: {x: -25.0, y: 1.8, z: -0.5}')],
    }
    for fid, edits in changes.items():
        if fid not in current:
            raise ValueError(f'Missing original object {fid}; nothing has been written')
        block = current[fid][1]
        for old, new in edits:
            block = replace_once(block, old, new, f'fileID {fid}')
        original = set_document(original, fid, current[fid][1], block)

    # Keep existing scene roots and every other object, including those that
    # were appended AFTER SceneRoots by another tool. Give new roots free order.
    root_doc = SCENE_ROOTS.search(original).group(0)
    original = SCENE_ROOTS.sub('', original, count=1).rstrip('\n') + '\n'
    max_order = max(map(int, re.findall(r'  m_RootOrder: (-?\d+)', original)))
    chunks = []
    for _, block in additions.values():
        if block.startswith('Transform:') and 'm_Father: {fileID: 0}' in block:
            max_order += 1
            block = re.sub(r'm_RootOrder: -?\d+', f'm_RootOrder: {max_order}', block, count=1)
        chunks.append(block.rstrip('\n') + '\n')
    updated = scene_roots(original + ''.join(chunks), root_doc)
    final_docs = documents(updated)
    if not set(current).issubset(final_docs) or not set(additions).issubset(final_docs):
        raise ValueError('A preexisting or new Unity object was lost; refusing write')
    if len(final_docs) != len(current) + len(additions):
        raise ValueError('Unexpected object count; refusing write')
    # Verify all existing documents except explicitly changed ones survive byte-for-byte.
    root_fid = int(re.search(r'&(\d+)', root_doc).group(1))
    for fid in set(current) - set(changes) - {root_fid}:
        if (current[fid][0] != final_docs[fid][0] or
                current[fid][1].rstrip('\n') != final_docs[fid][1].rstrip('\n')):
            raise ValueError(f'Unrelated object {fid} changed; refusing write')

    # Outside Assets so Unity does not import the backup and create a .meta.
    backup = (ROOT / 'TestLevel.unity.before-platforming.bak'
              if scene.resolve() == SCENE.resolve() else
              Path(str(scene) + '.before-platforming.bak'))
    if backup.exists():
        raise FileExistsError(f'Backup already exists; refusing overwrite: {backup}')
    shutil.copy2(scene, backup)
    temp = Path(str(scene) + '.platforming.tmp')
    try:
        temp.write_text(updated, encoding='utf-8')
        os.replace(temp, scene)
    finally:
        if temp.exists():
            temp.unlink()
    print(f'Added {len(additions)} Unity components without removing existing objects.')
    print(f'Original scene saved as: {backup}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scene', type=Path, default=SCENE,
                        help='TestLevel.unity to patch (defaults to this project)')
    args = parser.parse_args()
    apply(args.scene)
