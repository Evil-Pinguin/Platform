"""Перестраивает геометрию уровня в TestLevel.unity.

Сцена уже содержит камеру, героиню и фон — их не трогаем. Скрипт
вырезает всё, что сам же и добавлял (диапазон id 21xxxxxx…24xxxxxx),
и собирает заново, поэтому его можно запускать повторно.

Пивоты из .meta (ошибка тут стоила 2.9 юнита — героиня висела в воздухе):
  ground.png  alignment 2  pivot (0.5, 1) — ВЕРХ по центру
  block.png   alignment 0  pivot (0.5, 0.5) — ЦЕНТР
  chasm.png   alignment 0  pivot (0.5, 0.5) — ЦЕНТР
  декор       alignment 8  pivot (0.5, 0)   — НИЗ по центру

Физика прыжка: скорость 5, гравитация 9.81 * 3 = 29.43, высота 2.2.
Траектория y(D) = 2.276*D - 0.5886*D^2, поэтому шаг вверх на h
достижим только начиная с D >= ... (см. проверку reachability ниже).
"""
import re

SCENE = 'Assets/Scenes/TestLevel.unity'
GROUND_GUID = '715ee93385374f0b96aa2d97ab5033cb'
BLOCK_GUID = 'b836230688a54cfe9c6e1d59eee2e42e'
CHASM_GUID = '3d184408974c4b50961cd85749ce031c'
DECOR = {
    'spruce': ('acd1ec0f7dd66712a76a7f4aa03de836', 299, 775, 256),
    'rocks': ('b017c5b7638bea95aacc23fe69524e10', 348, 298, 384),
    'grass_tuft': ('5c2806664d4ba3f3f50b3741002bf34e', 309, 151, 256),
    'serge_pole': ('dfefd0bca0374f584c131084affd95aa', 161, 623, 256),
}

GROUND_H = 2.933594      # спрайт земли при PPU 256
GROUND_COLL = 2.5        # толщина коллайдера земли
CHASM_DEPTH = 6.0
START_X = -30.0           # где встаёт героиня
HERO_Y = 0.031           # её ступни в локальных координатах

# --- земля: имя, x0, x1, высота поверхности -------------------------
GROUND = [
    ('Ground_Start',   -32.0, -19.0,  0.0),   # стартовая поляна
    ('Ground_Ledge1',  -17.5, -14.0,  0.9),   # уступ
    ('Ground_Ledge2',  -12.5,  -9.0,  1.8),   # уступ
    ('Ground_Plateau',  -7.5,  -2.5,  2.4),   # высокое плато, видно весь уровень
    ('Ground_Descent',  -1.0,   2.5,  0.9),   # спуск
    ('Ground_Low',      4.0,   8.5,  0.0),   # низина у обрыва
    ('Ground_Mid',     14.5,  20.0,  0.0),   # за ямой
    ('Ground_Far',     27.5,  29.0,  0.0),   # за ущельем
    ('Ground_Hill1',   30.5,  33.0,  1.0),   # подъём на холм
    ('Ground_Hill2',   34.5,  37.0,  2.0),
    ('Ground_Crest',   38.5,  42.0,  2.4),   # гребень
]

# --- платформы из плит: имя, x0, x1, низ ----------------------------
# ВНИМАНИЕ: у плиты высота 1, поэтому ходить можно по верху = низ + 1.
PLATFORMS = [
    # ступени над ямой — единственный путь через пропасть 8.5…14.5
    ('Platform_Step1',  9.5, 11.0,  0.0),   # верх 1.0
    ('Platform_Step2', 12.0, 13.5, -0.4),   # верх 0.6
    # мост через ущелье 20…27.5, плиты внахлёст, без щели; верх вровень
    # с землёй, чтобы по мосту можно было просто идти
    ('Bridge_1',       20.3, 22.6, -1.0),
    ('Bridge_2',       22.4, 24.7, -1.0),
    ('Bridge_3',       24.5, 26.8, -1.0),
]

ORDER_GROUND, ORDER_PLATFORM, ORDER_CHASM, ORDER_DECOR = 0, 1, -5, 1

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


def sprite(sid, gid, guid, order, size, draw_mode, flip=0):
    return (f"--- !u!212 &{sid}\nSpriteRenderer:\n" + COMMON_HEAD +
            f"  m_GameObject: {{fileID: {gid}}}\n  m_Enabled: 1\n"
            "  m_CastShadows: 0\n  m_ReceiveShadows: 0\n  m_DynamicOccludee: 1\n"
            "  m_MotionVectors: 1\n  m_LightProbeUsage: 1\n"
            "  m_ReflectionProbeUsage: 1\n  m_RayTracingMode: 0\n"
            "  m_RayTraceProcedural: 0\n  m_RenderingLayerMask: 1\n"
            "  m_RendererPriority: 0\n  m_Materials:\n"
            "  - {fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}\n"
            "  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n"
            "  m_StaticBatchRoot: {fileID: 0}\n  m_ProbeAnchor: {fileID: 0}\n"
            "  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n"
            "  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n"
            "  m_IgnoreNormalsForChartDetection: 0\n  m_ImportantGI: 0\n"
            "  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 0\n"
            "  m_MinimumChartSize: 4\n  m_AutoUVMaxDistance: 0.5\n"
            "  m_AutoUVMaxAngle: 89\n  m_LightmapParameters: {fileID: 0}\n"
            "  m_SortingLayerID: 0\n  m_SortingLayer: 0\n"
            f"  m_SortingOrder: {order}\n"
            f"  m_Sprite: {{fileID: 21300000, guid: {guid}, type: 3}}\n"
            "  m_Color: {r: 1, g: 1, b: 1, a: 1}\n"
            f"  m_FlipX: {flip}\n  m_FlipY: 0\n"
            f"  m_DrawMode: {draw_mode}\n"
            f"  m_Size: {{x: {size[0]}, y: {size[1]}}}\n"
            "  m_AdaptiveModeThreshold: 0.5\n  m_SpriteTileMode: 0\n"
            "  m_WasSpriteAssigned: 1\n  m_MaskInteraction: 0\n"
            "  m_SpriteSortPoint: 0\n")


def box(cid, gid, offset, size):
    return (f"--- !u!61 &{cid}\nBoxCollider2D:\n" + COMMON_HEAD +
            f"  m_GameObject: {{fileID: {gid}}}\n  m_Enabled: 1\n"
            f"  m_Density: 1\n  m_Material: {{fileID: 0}}\n  m_IsTrigger: 0\n"
            "  m_UsedByEffector: 0\n  m_UsedByComposite: 0\n"
            f"  m_Offset: {{x: {offset[0]}, y: {offset[1]}}}\n"
            f"  m_Size: {{x: {size[0]}, y: {size[1]}}}\n  m_EdgeRadius: 0\n")


# --- проверка проходимости -------------------------------------------
def reachable(d, h):
    """Можно ли прыгнуть на h выше, перелетев разрыв d по горизонтали.

    Траектория y(x) = (vy/v)x - (g/2v^2)x^2, отсюда корни y(x) = h:
        x = (v/g)(vy +/- sqrt(vy^2 - 2gh))
    Нижний корень — момент, когда она ещё набирает высоту, поэтому
    приземлиться можно только между корнями.
    """
    v, g = 5.0, 29.43
    vy = (2 * g * 2.2) ** 0.5
    disc = vy * vy - 2 * g * h
    if disc < 0:
        return False, (0.0, 0.0)
    root = disc ** 0.5
    d0, d1 = v / g * (vy - root), v / g * (vy + root)
    return d0 <= d <= d1, (d0, d1)


def check():
    """Все поверхности, по которым можно идти: земля + верх плит."""
    surfaces = [(x0, x1, top, n) for n, x0, x1, top in GROUND]
    surfaces += [(x0, x1, bottom + 1.0, n) for n, x0, x1, bottom in PLATFORMS]
    surfaces.sort()
    rows = []
    for i in range(len(surfaces) - 1):
        a, b = surfaces[i], surfaces[i + 1]
        if b[0] <= a[1]:          # участки смыкаются — шагать без прыжка
            continue
        d, h = b[0] - a[1], b[2] - a[2]
        ok, (d0, d1) = reachable(d, h)
        tag = 'прыжок' if h > 0.05 else ('спуск' if h < -0.05 else 'ровно')
        note = f'{tag}, окно {d0:.2f}…{d1:.2f}' if ok else 'НЕДОСТИЖИМО'
        rows.append((ok, f'{a[3]} -> {b[3]}: пропасть {d:.2f}, '
                           f'перепад {h:+.2f}  ({note})'))
    for ok, msg in rows:
        print(('  ok  ' if ok else '  BAD ') + msg)
    return all(ok for ok, _ in rows)


if not check():
    raise SystemExit('уровень непроходим — исправь таблицу выше')


# --- декор: вид, x, отражение ---------------------------------------
DECOR_ITEMS = [
    ('spruce', -29.0, 0), ('spruce', -24.0, 1), ('spruce', -20.5, 0),
    ('spruce', -11.0, 1), ('spruce', -6.0, 0), ('spruce', -3.6, 1),
    ('spruce', 5.0, 0), ('spruce', 7.6, 1), ('spruce', 16.0, 1),
    ('spruce', 31.5, 0), ('spruce', 40.0, 1),
    ('rocks', -26.5, 0), ('rocks', -21.5, 1), ('rocks', -15.5, 0),
    ('rocks', -0.2, 1), ('rocks', 18.0, 0), ('rocks', 35.5, 1),
    ('grass_tuft', -31.0, 0), ('grass_tuft', -27.5, 0),
    ('grass_tuft', -25.0, 1), ('grass_tuft', -22.5, 0),
    ('grass_tuft', -19.8, 1), ('grass_tuft', -16.5, 0),
    ('grass_tuft', -14.6, 1), ('grass_tuft', -10.2, 0),
    ('grass_tuft', -7.0, 0), ('grass_tuft', -4.6, 1),
    ('grass_tuft', -2.9, 0), ('grass_tuft', 0.8, 1),
    ('grass_tuft', 2.1, 0), ('grass_tuft', 4.6, 0),
    ('grass_tuft', 6.4, 1), ('grass_tuft', 8.0, 0),
    ('grass_tuft', 15.2, 0), ('grass_tuft', 19.2, 1),
    ('grass_tuft', 28.2, 0), ('grass_tuft', 30.9, 1),
    ('grass_tuft', 36.4, 0), ('grass_tuft', 39.2, 1),
    ('grass_tuft', 41.3, 0),
    ('serge_pole', -30.2, 0), ('serge_pole', -23.2, 1),
    ('serge_pole', -5.0, 0), ('serge_pole', 17.2, 1),
    ('serge_pole', 28.6, 0), ('serge_pole', 39.0, 1),
]


def ground_top_at(x):
    for _, x0, x1, top in GROUND:
        if x0 - 0.01 <= x <= x1 + 0.01:
            return top
    return None


s = open(SCENE, encoding='utf-8').read()

# --- вырезаем всё, что добавлял прошлый запуск ------------------------
stripped = 0
for prefix in ('21', '22', '23', '24'):
    s, n = re.subn(r'--- !u!\d+ &' + prefix + r'\d+\n(?:(?!--- !u!).)*', '', s, flags=re.S)
    stripped += n

blocks = []
order = 10

# --- земля: спрайт верх-центр => transform y = высота поверхности
for i, (name, x0, x1, top) in enumerate(GROUND):
    gid = 210000000 + i * 10
    w, cx = round(x1 - x0, 4), round((x0 + x1) / 2, 4)
    blocks.append(gameobject(gid, name, [gid + 1, gid + 2, gid + 3], order))
    blocks.append(transform(gid + 1, gid, (cx, top, 0), order))
    blocks.append(sprite(gid + 2, gid, GROUND_GUID, ORDER_GROUND, (w, GROUND_H), 2))
    blocks.append(box(gid + 3, gid, (0, -GROUND_COLL / 2), (w, GROUND_COLL)))
    order += 1

# --- платформы: спрайт по центру => transform y = низ + 0.5
for i, (name, x0, x1, bottom) in enumerate(PLATFORMS):
    gid = 220000000 + i * 10
    w, cx, cy = round(x1 - x0, 4), round((x0 + x1) / 2, 4), round(bottom + 0.5, 4)
    blocks.append(gameobject(gid, name, [gid + 1, gid + 2, gid + 3], order))
    blocks.append(transform(gid + 1, gid, (cx, cy, 0), order))
    blocks.append(sprite(gid + 2, gid, BLOCK_GUID, ORDER_PLATFORM, (w, 1), 2))
    blocks.append(box(gid + 3, gid, (0, 0), (w, 1)))
    order += 1

# --- провалы: тёмная бездна за каждым разрывом в земле
gaps = []
for i in range(len(GROUND) - 1):
    a, b = GROUND[i], GROUND[i + 1]
    if b[1] > a[2]:
        gaps.append((f'Chasm_{i + 1}', a[2], b[1]))
for i, (name, x0, x1) in enumerate(gaps):
    gid = 230000000 + i * 10
    w, cx = round(x1 - x0, 4), round((x0 + x1) / 2, 4)
    blocks.append(gameobject(gid, name, [gid + 1, gid + 2], order))
    blocks.append(transform(gid + 1, gid, (cx, -CHASM_DEPTH / 2, 0), order))
    blocks.append(sprite(gid + 2, gid, CHASM_GUID, ORDER_CHASM, (w, CHASM_DEPTH), 0))
    order += 1

# --- декор: пивот низ-центр => transform y = высота поверхности
placed = 0
for i, (kind, x, flip) in enumerate(DECOR_ITEMS):
    top = ground_top_at(x)
    assert top is not None, f'декор {kind} на x={x} висит в воздухе'
    guid, pw, ph, ppu = DECOR[kind]
    gid = 240000000 + i * 10
    w, h = round(pw / ppu, 4), round(ph / ppu, 4)
    blocks.append(gameobject(gid, f'Decor_{kind}_{i:02d}', [gid + 1, gid + 2], order))
    blocks.append(transform(gid + 1, gid, (x, top, -0.5), order))
    blocks.append(sprite(gid + 2, gid, guid, ORDER_DECOR, (w, h), 0, flip))
    order += 1
    placed += 1

s = s.rstrip('\n') + '\n' + '\n'.join(blocks)

# --- героиня на старте поляны
s, n = re.subn(r'(--- !u!4 &400000002\nTransform:.*?  m_LocalPosition: )'
               r'\{x: [-\d.]+, y: [-\d.]+, z: [-\d.]+\}',
               lambda m: m.group(1) + f'{{x: {START_X}, y: {HERO_Y}, z: 0}}',
               s, flags=re.S)
assert n == 1, f'позиция героини: {n}'

# --- камера следит и по вертикали
s, n = re.subn(r'(--- !u!114 &100000005\nMonoBehaviour:.*?  offset: )\{x: [-\d.]+, y: [-\d.]+\}',
               r'\g<1>{x: 0, y: -0.3}', s, flags=re.S)
assert n == 1, f'offset камеры: {n}'
s, n = re.subn(r'(--- !u!114 &100000005\nMonoBehaviour:.*?  followY: )[01]',
               r'\g<1>1', s, flags=re.S)
assert n == 1, f'followY: {n}'

# --- конфигурация PlayerController
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
print(f'уровень собран: земля {len(GROUND)} | платформы {len(PLATFORMS)} | '
      f'провалы {len(gaps)} | декор {placed} | вырезано блоков {stripped}')
