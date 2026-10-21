import sys; sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
from fbxdump import *
def parts(path):
    top=read(path)
    objs=[t for t in top if t[0]=='Objects'][0]; conns=[t for t in top if t[0]=='Connections'][0]
    names={o[1][0]:o[1][1].split('\x00')[0] for o in objs[2]}
    out=[]
    for o in objs[2]:
        if o[0]!='Geometry': continue
        owner=[names[c[1][2]] for c in conns[2] if c[1][1]==o[1][0]][0]
        v=find(o,'Vertices')[0][1][0]; pi=find(o,'PolygonVertexIndex')[0][1][0]
        uvl=find(o,'LayerElementUV')[0]; uv=find(uvl,'UV')[0][1][0]; ui=find(uvl,'UVIndex')[0][1][0]
        tris=[];poly=[]
        for k,i in enumerate(pi):
            vi=i if i>=0 else ~i
            poly.append(((v[3*vi],v[3*vi+1],v[3*vi+2]),(uv[2*ui[k]],uv[2*ui[k]+1])))
            if i<0:
                for j in range(1,len(poly)-1): tris.append((poly[0],poly[j],poly[j+1]))
                poly=[]
        out.append((owner,tris))
    return out
