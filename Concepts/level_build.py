"""Перестраивает геометрию уровня в TestLevel.unity.

Пиво��ы из .meta (это важно, ошибка тут стоила 2.9 юнита):
  ground.png  alignment 2  pivot (0.5, 1) — ВЕРХ по центру
  block.png   alignment 0  pivot (0.5, 0.5) — ЦЕНТР
  chasm.png   alignment 0  pivot (0.5, 0.5) — ЦЕНТР
"""
import re

SCENE = 'Assets/Scenes/TestLevel.unity'
GROUND_GUID = '715ee93385374f0b96aa2d97ab5033cb'
BLOCK_GUID = 'b836230688a54cfe9c6e1d59eee2e42e'
CHASM_GUID = '3d184408974c4b50961cd85749ce031c'

GROUND_H = 2.933594      # спрайт земли при PPU 256
GROUND_COLL = 2.5        # толщина коллайдера земли
CHASM_DEPTH = 6.0

# Поверхность земли везде y = 0
GROUND = [
    ('Ground_Start', -30.0, -6.0),
    ('Ground_Mid',    -4.2,   2.0),
    ('Ground_Far1',    9.5,  16.0),
    ('Ground_Far2',   17.8,  30.0),
]
# Платформы: x0, x1, низ
PLATFORMS = [
    ('Platform_1',  2.0,  5.0, 0.4),
    ('Platform_2',  5.0,  8.0, 1.4),
    ('Platform_3', 21.0, 24.0, 0.0),
    ('Platform_4', 25.0, 28.0, 1.0),
]
# Провалы: x0, x1
CHASMS = [
    ('Chasm_1', -6.0, -4.2),
    ('Chasm_2',  8.0,  9.5),
    ('Chasm_3', 16.0, 17.8),
]

OLD = ['200000001', '200000002', '200000003', '200000004',
       '300000001', '300000002', '300000003', '300000004']

COMMON_HEAD = """  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
"""

def gameobject(gid, name, comps, order):
    c = '\n'.join(f'  - component: {{fileID: {x}}}' for x in comps)
    return (f"--- !u!1 &{gid}\nGameObject:\n" + COMMON_HEAD +
            "  serializedVersion: 6\n  m_Component:\n" + c + "\n" +
            f"  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n"
            f"  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n"
            f"  m_StaticEditorFlags: 0\n  m_IsActive: 1\n")

def transform(tid, gid, pos, order, father=0):
    return (f"--- !u!4 &{tid}\nTransform:\n" + COMMON_HEAD +
            f"  m_GameObject: {{fileID: {gid}}}\n"
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
            f"  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}\n"
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_Children: []\n"
            f"  m_Father: {{fileID: {father}}}\n  m_RootOrder: {order}\n"
            "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n")

def sprite(sid, gid, guid, order, size, draw_mode, pos):
    return (f"--- !u!212 &{sid}\nSpriteRenderer:\n" + COMMON_HEAD +
            f"  m_GameObject: {{fileID: {gid}}}\n  m_Enabled: 1\n"
            "  m_CastShadows: 0\n  m_ReceiveShadows: 0\n  m_DynamicOccludee: 1\n"
            "  m_MotionVectors: 1\n  m_LightProbeUsage: 1\n  m_ReflectionProbeUsage: 1\n"
            "  m_RayTracingMode: 0\n  m_RayTraceProcedural: 0\n  m_RenderingLayerMask: 1\n"
            "  m_RendererPriority: 0\n  m_Materials:\n"
            "  - {fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}\n"
            "  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n"
            "  m_StaticBatchRoot: {fileID: 0}\n  m_ProbeAnchor: {fileID: 0}\n"
            "  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n"
            "  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n  m_IgnoreNormalsForChartDetection: 0\n"
            "  m_ImportantGI: 0\n  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 0\n"
            "  m_MinimumChartSize: 4\n  m_AutoUVMaxDistance: 0.5\n  m_AutoUVMaxAngle: 89\n"
            "  m_LightmapParameters: {fileID: 0}\n  m_SortingLayerID: 0\n"
            f"  m_SortingLayer: 0\n  m_SortingOrder: {order}\n"
            f"  m_Sprite: {{fileID: 21300000, guid: {guid}, type: 3}}\n"
            "  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_FlipX: 0\n  m_FlipY: 0\n"
            f"  m_DrawMode: {draw_mode}\n"
            f"  m_Size: {{x: {size[0]}, y: {size[1]}}}\n"
            "  m_AdaptiveModeThreshold: 0.5\n  m_SpriteTileMode: 0\n"
            "  m_WasSpriteAssigned: 1\n  m_MaskInteraction: 0\n  m_SpriteSortPoint: 0\n")

def box(cid, gid, offset, size):
    return (f"--- !u!61 &{cid}\nBoxCollider2D:\n" + COMMON_HEAD +
            f"  m_GameObject: {{fileID: {gid}}}\n  m_Enabled: 1\n"
            f"  m_Density: 1\n  m_Material: {{fileID: 0}}\n  m_IsTrigger: 0\n"
            "  m_UsedByEffector: 0\n  m_UsedByComposite: 0\n"
            f"  m_Offset: {{x: {offset[0]}, y: {offset[1]}}}\n"
            f"  m_Size: {{x: {size[0]}, y: {size[1]}}}\n  m_EdgeRadius: 0\n")

s = open(SCENE, encoding='utf-8').read()

# выкидываем старую Землю и старый Блок
for oid in OLD:
    s, n = re.subn(r'--- !u!\d+ &' + oid + r'\n(?:(?!--- !u!).)*', '', s, flags=re.S)
    assert n == 1, f'не удалось убрать {oid}: {n}'

blocks = []
order = 10

# --- земля: спрайт верх-центр => transform y = 0, коллайдер уходит вниз
for i, (name, x0, x1) in enumerate(GROUND):
    gid = 210000000 + i*10
    w = round(x1 - x0, 4)
    cx = round((x0 + x1)/2, 4)
    blocks.append(gameobject(gid, name, [gid+1, gid+2, gid+3], order))
    blocks.append(transform(gid+1, gid, (cx, 0, 0), order))
    blocks.append(sprite(gid+2, gid, GROUND_GUID, 0, (w, GROUND_H), 2, None))
    blocks.append(box(gid+3, gid, (0, -GROUND_COLL/2), (w, GROUND_COLL)))
    order += 1

# --- платформы: спрайт по центру => transform y = низ + 0.5
for i, (name, x0, x1, bottom) in enumerate(PLATFORMS):
    gid = 220000000 + i*10
    w = round(x1 - x0, 4)
    cx = round((x0 + x1)/2, 4)
    cy = round(bottom + 0.5, 4)
    blocks.append(gameobject(gid, name, [gid+1, gid+2, gid+3], order))
    blocks.append(transform(gid+1, gid, (cx, cy, 0), order))
    blocks.append(sprite(gid+2, gid, BLOCK_GUID, 1, (w, 1), 2, None))
    blocks.append(box(gid+3, gid, (0, 0), (w, 1)))
    order += 1

# --- провалы: тёмная бездна за землёй, спрайт по центру
for i, (name, x0, x1) in enumerate(CHASMS):
    gid = 230000000 + i*10
    w = round(x1 - x0, 4)
    cx = round((x0 + x1)/2, 4)
    blocks.append(gameobject(gid, name, [gid+1, gid+2], order))
    blocks.append(transform(gid+1, gid, (cx, -CHASM_DEPTH/2, 0), order))
    blocks.append(sprite(gid+2, gid, CHASM_GUID, -5, (w, CHASM_DEPTH), 0, None))
    order += 1

s = s.rstrip('\n') + '\n' + '\n'.join(blocks)

# --- камера теперь следит и по вертикали
s, n = re.subn(r'(--- !u!114 &100000005\nMonoBehaviour:.*?  offset: )\{x: 0, y: 0\}',
               r'\g<1>{x: 0, y: -0.3}', s, flags=re.S)
assert n == 1, f'offset камеры: {n}'
s, n = re.subn(r'(--- !u!114 &100000005\nMonoBehaviour:.*?  followY: )0',
               r'\g<1>1', s, flags=re.S)
assert n == 1, f'followY: {n}'

# --- новая конфигурация PlayerController
frames = '\n'.join(
    f'  - {{fileID: 21300000, guid: {g}, type: 3}}' for g in
    ['7a2d62ffd4fe461990b64ffe293dc930', '334e38dba303439bbcb282a1a842582c',
     '164fb7696ad34f3a9910630351b6bf06', '22d6721341cd46f18ec3bab4728fb4d9',
     '90b168f17df94355a5c63298da7e1f43', '0b1dc8ab10cd479e874689e3d94276ee',
     '1ae4d6901c194c62a2e0e62b49e6a309', '97fcaa228c8648e0a1b563d43e3bcd7f'])
s, n = re.subn(
    r'(--- !u!114 &400000006\nMonoBehaviour:.*?  m_EditorClassIdentifier: \n)'
    r'(?:(?!--- !u!).)*',
    lambda m: (m.group(1) +
        '  moveSpeed: 5\n  acceleration: 70\n'
        '  jumpHeight: 2.2\n  jumpHoldTime: 0.16\n  coyoteTime: 0.1\n'
        '  jumpBuffer: 0.12\n'
        '  walkFrames:\n' + frames + '\n'
        '  walkFps: 12\n'
        '  idleSprite: {fileID: 21300000, guid: 164fb7696ad34f3a9910630351b6bf06, type: 3}\n'
        '  respawnBelowY: -12\n'),
    s, flags=re.S)
assert n == 1, f'PlayerController: {n}'

open(SCENE, 'w', encoding='utf-8').write(s)
print('уровень пересобран: земельных участков', len(GROUND),
      '| платформ', len(PLATFORMS), '| провалов', len(CHASMS))
