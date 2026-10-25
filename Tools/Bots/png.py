import zlib,struct,sys
def load(path):
    d=open(path,'rb').read(); o=8; idat=b''
    while o<len(d):
        n=struct.unpack('>I',d[o:o+4])[0]; t=d[o+4:o+8]; c=d[o+8:o+8+n]; o+=12+n
        if t==b'IHDR': w,h,bd,ct=struct.unpack('>IIBB',c[:10])
        if t==b'IDAT': idat+=c
    raw=zlib.decompress(idat); bpp={2:3,6:4}[ct]; stride=w*bpp
    out=bytearray(); prev=bytearray(stride); i=0
    for y in range(h):
        f=raw[i]; line=bytearray(raw[i+1:i+1+stride]); i+=1+stride
        for x in range(stride):
            a=line[x-bpp] if x>=bpp else 0; b=prev[x]; c=prev[x-bpp] if x>=bpp else 0
            if f==1: line[x]=(line[x]+a)&255
            elif f==2: line[x]=(line[x]+b)&255
            elif f==3: line[x]=(line[x]+((a+b)>>1))&255
            elif f==4:
                p=a+b-c; pa=abs(p-a); pb=abs(p-b); pc=abs(p-c)
                line[x]=(line[x]+(a if pa<=pb and pa<=pc else b if pb<=pc else c))&255
        out+=line; prev=line
    return w,h,bpp,out
if __name__=='__main__':
    for f in sys.argv[1:]:
        w,h,bpp,px=load(f); n=w*h
        mx=sorted(max(px[i*bpp:i*bpp+3]) for i in range(0,n,7))
        m=len(mx)
        print(f,'mean',[round(sum(px[i*bpp+k] for i in range(0,n,7))/m) for k in range(3)],'p50',mx[m//2],'p90',mx[int(m*.9)],'p97',mx[int(m*.97)],'>140 %',round(sum(1 for v in mx if v>140)/m*100,1))
def save(path, w, h, bpp, px):
    ct = {3: 2, 4: 6}[bpp]
    raw = b''.join(b'\x00' + bytes(px[y*w*bpp:(y+1)*w*bpp]) for y in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, ct, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))
