import subprocess, sys
from collections import deque

SRC = '/tmp/v5.png'
BOUNDS = [(15,232),(263,476),(502,720),(752,963),
          (995,1207),(1230,1445),(1477,1699),(1730,1943)]
H = 544

W,H = map(int, subprocess.run(['identify','-format','%w %h',SRC],
                              capture_output=True,text=True).stdout.split())
buf = subprocess.run(['convert',SRC,'-depth','8','rgba:-'],
                     capture_output=True).stdout
assert len(buf) == W*H*4, (len(buf), W*H*4)

def px(x,y):
    i=(y*W+x)*4
    return buf[i], buf[i+1], buf[i+2], buf[i+3]

def is_white_opaque(x,y):
    r,g,b,a = px(x,y)
    return a>200 and r>=253 and g>=253 and b>=253

def is_open(x,y):
    """Прозрачный ИЛИ чисто белый непрозрачный — сюда фон попадает"""
    r,g,b,a = px(x,y)
    return a<=200 or (r>=253 and g>=253 and b>=253)

for idx,(x0,x1) in enumerate(BOUNDS,1):
    # --- 1. находим замкнутые белые дыры в этом кадре ---
    seen=[[False]*H for _ in range(x1-x0+1)]
    q=deque()
    w=x1-x0+1
    for x in range(w):
        for y in (0,H-1):
            if not seen[x][y] and is_open(x0+x,y):
                seen[x][y]=True; q.append((x,y))
    for y in range(H):
        for x in (0,w-1):
            if not seen[x][y] and is_open(x0+x,y):
                seen[x][y]=True; q.append((x,y))
    while q:
        x,y=q.popleft()
        for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
            nx,ny=x+dx,y+dy
            if 0<=nx<w and 0<=ny<H and not seen[nx][ny] and is_open(x0+nx,ny):
                seen[nx][ny]=True; q.append((nx,ny))

    holes=0
    out=bytearray(buf)
    for x in range(w):
        for y in range(H):
            if not seen[x][y] and is_white_opaque(x0+x,y):
                i=((y*W)+(x0+x))*4
                out[i+3]=0
                holes+=1
    open(f'/tmp/v5raw{idx}.rgba','wb').write(bytes(out))
    print(f'кадр {idx}: замкнутых белых областей очищено — {holes} px')
