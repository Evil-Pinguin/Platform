"""Заспавнивает в TestLevel.unity два диагностических объекта.

Их смысл — разделить две разные поломки, которые снаружи выглядят
одинаково («пустой экран»):

  TEST_Block  — куб 2x2x2 со встроенным материалом и коллайдером.
                Если его видно, значит сцена грузится, камера смотрит
                туда же и рендер работает. Тогда не видно именно
                спрайтов — и причина в их импорте.
  TEST_Sprite — спрайт ground.png 1x1, покрашенный в малиновый.
                Если видно его, но не видно остального — сцена и
                спрайты в порядке, а сломан порядок отрисовки.

Ищи их в иерархии по именам. Скрипт можно запускать повторно —
старые объекты вырезаются.
"""
import re
import sys

SCENE = 'Assets/Scenes/TestLevel.unity'
GROUND_GUID = '715ee93385374f0b96aa2d97ab5033cb'
CUBE_MESH = 10202          # встроенный куб
DEFAULT_MAT = 10303        # встроенный Default-Material

HEAD = """  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
"""


def go(gid, name, comps):
    c = '\n'.join(f'  - component: {{fileID: {x}}}' for x in comps)
    return (f'--- !u!1 &{gid}\nGameObject:\n' + HEAD +
            '  serializedVersion: 6\n  m_Component:\n' + c + '\n'
            f'  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n'
            '  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n'
            '  m_StaticEditorFlags: 0\n  m_IsActive: 1\n')


def tr(tid, gid, pos, scale, order):
    return (f'--- !u!4 &{tid}\nTransform:\n' + HEAD +
            f'  m_GameObject: {{fileID: {gid}}}\n'
            '  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n'
            f'  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}\n'
            f'  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}\n'
            '  m_Children: []\n  m_Father: {fileID: 0}\n'
            f'  m_RootOrder: {order}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n')


def meshfilter(fid, gid):
    return (f'--- !u!33 &{fid}\nMeshFilter:\n' + HEAD +
            f'  m_GameObject: {{fileID: {gid}}}\n'
            f'  m_Mesh: {{fileID: {CUBE_MESH}, guid: 0000000000000000e000000000000000, type: 0}}\n')


def meshrenderer(fid, gid):
    return (f'--- !u!23 &{fid}\nMeshRenderer:\n' + HEAD +
            f'  m_GameObject: {{fileID: {gid}}}\n  m_Enabled: 1\n'
            '  m_CastShadows: 1\n  m_ReceiveShadows: 1\n  m_DynamicOccludee: 1\n'
            '  m_StaticShadowCaster: 0\n  m_MotionVectors: 1\n'
            '  m_LightProbeUsage: 1\n  m_ReflectionProbeUsage: 1\n'
            '  m_RayTracingMode: 2\n  m_RayTraceProcedural: 0\n'
            '  m_RenderingLayerMask: 1\n  m_RendererPriority: 0\n  m_Materials:\n'
            f'  - {{fileID: {DEFAULT_MAT}, guid: 0000000000000000f000000000000000, type: 0}}\n'
            '  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 1\n'
            '  m_StaticBatchRoot: {fileID: 0}\n  m_ProbeAnchor: {fileID: 0}\n'
            '  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n'
            '  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n'
            '  m_IgnoreNormalsForChartDetection: 0\n  m_ImportantGI: 0\n'
            '  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 0\n'
            '  m_MinimumChartSize: 4\n  m_AutoUVMaxDistance: 0.5\n'
            '  m_AutoUVMaxAngle: 89\n  m_LightmapParameters: {fileID: 0}\n'
            '  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: 0\n'
            '  m_AdditionalVertexStreams: {fileID: 0}\n')


def box2d(fid, gid, size):
    return (f'--- !u!61 &{fid}\nBoxCollider2D:\n' + HEAD +
            f'  m_GameObject: {{fileID: {gid}}}\n  m_Enabled: 1\n  m_Density: 1\n'
            '  m_Material: {fileID: 0}\n  m_IsTrigger: 0\n  m_UsedByEffector: 0\n'
            '  m_UsedByComposite: 0\n  m_Offset: {x: 0, y: 0}\n'
            f'  m_Size: {{x: {size[0]}, y: {size[1]}}}\n  m_EdgeRadius: 0\n')


def sprite(fid, gid):
    return (f'--- !u!212 &{fid}\nSpriteRenderer:\n' + HEAD +
            f'  m_GameObject: {{fileID: {gid}}}\n  m_Enabled: 1\n'
            '  m_CastShadows: 0\n  m_ReceiveShadows: 0\n  m_DynamicOccludee: 1\n'
            '  m_MotionVectors: 1\n  m_LightProbeUsage: 1\n'
            '  m_ReflectionProbeUsage: 1\n  m_RayTracingMode: 0\n'
            '  m_RayTraceProcedural: 0\n  m_RenderingLayerMask: 1\n'
            '  m_RendererPriority: 0\n  m_Materials:\n'
            '  - {fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}\n'
            '  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n'
            '  m_StaticBatchRoot: {fileID: 0}\n  m_ProbeAnchor: {fileID: 0}\n'
            '  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n'
            '  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n'
            '  m_IgnoreNormalsForChartDetection: 0\n  m_ImportantGI: 0\n'
            '  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 0\n'
            '  m_MinimumChartSize: 4\n  m_AutoUVMaxDistance: 0.5\n'
            '  m_AutoUVMaxAngle: 89\n  m_LightmapParameters: {fileID: 0}\n'
            '  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: 9\n'
            f'  m_Sprite: {{fileID: 21300000, guid: {GROUND_GUID}, type: 3}}\n'
            '  m_Color: {r: 1, g: 0.1, b: 0.6, a: 1}\n  m_FlipX: 0\n  m_FlipY: 0\n'
            '  m_DrawMode: 0\n  m_Size: {x: 2, y: 2}\n'
            '  m_AdaptiveModeThreshold: 0.5\n  m_SpriteTileMode: 0\n'
            '  m_WasSpriteAssigned: 1\n  m_MaskInteraction: 0\n  m_SpriteSortPoint: 0\n')


s = open(SCENE, encoding='utf-8').read()

# выкидываем прежние тестовые объекты
for pref in ('90', '91'):
    s, n = re.subn(r'--- !u!\d+ &' + pref + r'\d+\n(?:(?!--- !u!).)*', '', s, flags=re.S)

if len(sys.argv) > 1 and sys.argv[1] == 'remove':
    s = re.sub(r'  - \{fileID: (?:900000001|910000001)\}\n', '', s)
    open(SCENE, 'w', encoding='utf-8').write(s)
    print('тестовые объекты удалены')
    raise SystemExit

blocks = [
    go(900000001, 'TEST_Block', [900000002, 900000003, 900000004, 900000006]),
    tr(900000002, 900000001, (-30, 1, 0), (2, 2, 2), 0),
    meshfilter(900000003, 900000001),
    meshrenderer(900000004, 900000001),
    box2d(900000006, 900000001, (2, 2)),
    go(910000001, 'TEST_Sprite', [910000002, 910000003, 910000005]),
    tr(910000002, 910000001, (-27, 1.5, 0), (1, 1, 1), 1),
    sprite(910000003, 910000001),
    box2d(910000005, 910000001, (2, 2)),
]

# вставляем перед блоком SceneRoots, чтобы он оставался последним
i = s.index('--- !u!1660057539 &')
s = s[:i] + '\n'.join(blocks) + '\n' + s[i:]

# и регистрируем их как корневые
s, n = re.subn(r'(SceneRoots:\n(?:.*\n)*?  m_Roots:\n)',
               r'\g<1>  - {fileID: 900000001}\n  - {fileID: 910000001}\n', s)
assert n == 1, f'корень SceneRoots: {n}'

open(SCENE, 'w', encoding='utf-8').write(s)
print('добавлены TEST_Block (куб 2x2x2) и TEST_Sprite (малиновый спрайт 2x2)')
print('оба с коллайдерами — на TEST_Block героиня должна встать')
