import struct, zlib, sys
def read(path):
    d=open(path,'rb').read()
    ver=struct.unpack('<I',d[23:27])[0]
    big = ver>=7500
    def node(o):
        if big:
            end,np,pl=struct.unpack('<QQQ',d[o:o+24]); o+=24
        else:
            end,np,pl=struct.unpack('<III',d[o:o+12]); o+=12
        nl=d[o]; o+=1
        if end==0: return None,o
        name=d[o:o+nl].decode('latin1'); o+=nl
        props=[]
        for _ in range(np):
            t=chr(d[o]); o+=1
            if t in 'YCIFDL':
                fmt={'Y':'<h','C':'<?','I':'<i','F':'<f','D':'<d','L':'<q'}[t]; s=struct.calcsize(fmt)
                props.append(struct.unpack(fmt,d[o:o+s])[0]); o+=s
            elif t in 'fdlib':
                n,enc,cl=struct.unpack('<III',d[o:o+12]); o+=12
                raw=d[o:o+cl]; o+=cl
                if enc: raw=zlib.decompress(raw)
                fmt={'f':'f','d':'d','l':'q','i':'i','b':'?'}[t]
                props.append(list(struct.unpack('<%d%s'%(n,fmt),raw)))
            elif t in 'SR':
                n=struct.unpack('<I',d[o:o+4])[0]; o+=4
                v=d[o:o+n]; o+=n
                props.append(v.decode('latin1') if t=='S' else ('RAW',len(v)))
        kids=[]
        while o<end:
            k,o2=node(o)
            o=o2
            if k is None: break
            kids.append(k)
        return (name,props,kids),end
    o=27; top=[]
    while o<len(d)-200:
        n,o=node(o)
        if n is None: break
        top.append(n)
    return top
def find(n,name):
    return [k for k in n[2] if k[0]==name]
