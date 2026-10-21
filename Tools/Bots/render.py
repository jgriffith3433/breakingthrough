# Usage: python3 render.py <Bot.fbx> <Skin.png> <out.png>: front, +X, -X, back and three-quarter views of a textured FBX, no GPU.
import sys, math, zlib, struct; sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
from fbxdump import *
import png
fbx,texp,outp=sys.argv[1],sys.argv[2],sys.argv[3]
mode=sys.argv[4] if len(sys.argv)>4 else 'lit'
top=read(fbx); tw,th,tb,tpx=png.load(texp)
objs=[t for t in top if t[0]=='Objects'][0]
tris=[]
for o in objs[2]:
    if o[0]!='Geometry': continue
    v=find(o,'Vertices')[0][1][0]; pi=find(o,'PolygonVertexIndex')[0][1][0]
    uvl=find(o,'LayerElementUV')[0]; uv=find(uvl,'UV')[0][1][0]; ui=find(uvl,'UVIndex')[0][1][0]
    poly=[]
    for k,i in enumerate(pi):
        vi = i if i>=0 else ~i
        poly.append(((v[3*vi],v[3*vi+1],v[3*vi+2]),(uv[2*ui[k]],uv[2*ui[k]+1])))
        if i<0:
            for j in range(1,len(poly)-1): tris.append((poly[0],poly[j],poly[j+1]))
            poly=[]
S=420
views=[('front',lambda p:(p[0],p[2],p[1])),('plusx',lambda p:(p[1],p[2],-p[0])),('minusx',lambda p:(-p[1],p[2],p[0])),('back',lambda p:(-p[0],p[2],-p[1])),('threeq',lambda p:((p[0]+p[1])*0.7071,p[2],(p[1]-p[0])*0.7071))]
allp=[t[i][0] for t in tris for i in range(3)]
cx=sum(p[0] for p in allp)/len(allp); cy=sum(p[1] for p in allp)/len(allp)
ext=max(max(abs(p[0]-cx),abs(p[1]-cy),p[2]) for p in allp)*1.05
W=S*len(views); img=bytearray([40,44,60]*W*S)
light=(0.4,-0.6,0.7); ln=math.sqrt(sum(c*c for c in light)); light=[c/ln for c in light]
for vi,(name,f) in enumerate(views):
    zb=[1e9]*(S*S)
    for t in tris:
        P=[f((p[0]-cx,p[1]-cy,p[2])) for p,_ in t]
        a=[P[1][k]-P[0][k] for k in range(3)]; b=[P[2][k]-P[0][k] for k in range(3)]
        nrm=(a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]); nl=math.sqrt(sum(c*c for c in nrm)) or 1
        nrm=[c/nl for c in nrm]
        lam=abs(nrm[0]*light[0]+nrm[1]*light[1]+nrm[2]*-light[2])
        sp=[((q[0]/ext*0.5+0.5)*S,(1-(q[1]/ext*0.9+0.05))*S,q[2]) for q in P]
        x0=max(0,int(min(s[0] for s in sp))); x1=min(S-1,int(max(s[0] for s in sp))+1)
        y0=max(0,int(min(s[1] for s in sp))); y1=min(S-1,int(max(s[1] for s in sp))+1)
        (ax,ay,az),(bx,by,bz),(qx,qy,qz)=sp
        den=(by-qy)*(ax-qx)+(qx-bx)*(ay-qy)
        if abs(den)<1e-9: continue
        for y in range(y0,y1+1):
            for x in range(x0,x1+1):
                w0=((by-qy)*(x-qx)+(qx-bx)*(y-qy))/den; w1=((qy-ay)*(x-qx)+(ax-qx)*(y-qy))/den; w2=1-w0-w1
                if w0<0 or w1<0 or w2<0: continue
                z=w0*az+w1*bz+w2*qz
                if z>=zb[y*S+x]: continue
                zb[y*S+x]=z
                u=w0*t[0][1][0]+w1*t[1][1][0]+w2*t[2][1][0]; vv=w0*t[0][1][1]+w1*t[1][1][1]+w2*t[2][1][1]
                tx=min(tw-1,max(0,int(u*tw))); ty=min(th-1,max(0,int((1-vv)*th)))
                c=tpx[(ty*tw+tx)*tb:(ty*tw+tx)*tb+tb]; gl=(c[3]/255.0 if tb==4 else 0); c=c[:3]
                if mode=='lit': col=[min(255,int(ch*(0.35+0.65*lam)+ch*gl*1.6+12)) for ch in c]
                else: col=[min(255,int(ch*(0.35+0.65*lam)*2.2+18)) for ch in c]
                o=((y)*W+x+vi*S)*3; img[o:o+3]=bytes(col)
raw=b''.join(b'\x00'+bytes(img[y*W*3:(y+1)*W*3]) for y in range(S))
def chunk(t,d): return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
open(outp,'wb').write(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',W,S,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw,6))+chunk(b'IEND',b''))
print(len(tris),'tris')
