"""Собирает финальную альфу кадров ходьбы.

Источник цвета  — исходный белый фон (мягкие края сохраняются).
Источник маски  — флудфилл (он корректно отличает фон от платья,
в отличие от порога по яркости: платье почти белое).
Дополнительно снимаются замкнутые белые дыры, до которых флудфилл
не дошёл (между к��сой и подолом).
"""
import subprocess
from collections import deque

SRC = '/home/user/Platform/Concepts/walk_cycle_v5/walk_sheet_v5.png'
MASK = '/tmp/v5.png'          # результат флудфилла, нужен только alpha
BOUNDS = [(15,232),(263,476),(502,720),(752,963),
          (995,1207),(1230,1445),(1477,1699),(1730,1943)]
H = 544

W, H = map(int, subprocess.run(['identify','-format','%w %h',SRC],
                               capture_output=True,text=True).stdout.split())
orig = subprocess.run(['convert',SRC,'-depth','8','rgb:-'],capture_output=True).stdout
mask = subprocess.run(['convert',MASK,'-depth','8','rgba:-'],capture_output=True).stdout
assert len(orig) == W*H*3 and len(mask) == W*H*4

def m_alpha(x,y): return mask[(y*W+x)*4+3]
def m_white(x,y):
    i=(y*W+x)*4
    return mask[i]>=253 and mask[i+1]>=253 and mask[i+2]>=253
def open_px(x,y):
    return m_alpha(x,y) <= 200 or m_white(x,y)

out = bytearray(mask)
for x0,x1 in BOUNDS:
    w = x1-x0+1
    seen=[[False]*H for _ in range(w)]
    q=deque()
    for x in range(w):
        for y in (0,H-1):
            if not seen[x][y] and open_px(x0+x,y): seen[x][y]=True; q.append((x,y))
    for y in range(H):
        for x in (0,w-1):
            if not seen[x][y] and open_px(x0+x,y): seen[x][y]=True; q.append((x,y))
    while q:
        x,y=q.popleft()
        for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
            nx,ny=x+dx,y+dy
            if 0<=nx<w and 0<=ny<H and not seen[nx][ny] and open_px(x0+nx,ny):
                seen[nx][ny]=True; q.append((nx,ny))
    for x in range(w):
        for y in range(H):
            gx=x0+x
            if not seen[x][y] and m_alpha(gx,y)>200 and m_white(gx,y):
                out[(y*W+gx)*4+3]=0

# цвет берём из оригинала везде, где пиксель остаётся
for i in range(W*H):
    j=i*4
    if out[j+3] > 0:
        out[j],out[j+1],out[j+2] = orig[i*3],orig[i*3+1],orig[i*3+2]
    else:
        out[j]=out[j+1]=out[j+2]=0

open('/tmp/v5_final.rgba','wb').write(bytes(out))
subprocess.run(['convert','-size',f'{W}x{H}','-depth','8','rgba:/tmp/v5_final.rgba',
                'PNG32:/tmp/v5_final.png'],check=True)
print('готово', W, H)
