import sys,zlib,struct
def dec(p):
    d=open(p,'rb').read()[8:];i=0;idat=b'';
    while i<len(d):
        n,=struct.unpack('>I',d[i:i+4]);t=d[i+4:i+8];c=d[i+8:i+8+n];i+=12+n
        if t==b'IHDR':w,h,bd,ct=struct.unpack('>IIBB',c[:10])
        elif t==b'IDAT':idat+=c
    ch={2:3,6:4,0:1,4:2}[ct];bpp=ch*(bd//8);raw=zlib.decompress(idat);st=w*bpp;prev=bytearray(st);o=0;rows=[]
    for y in range(h):
        f=raw[o];line=bytearray(raw[o+1:o+1+st]);o+=1+st
        for x in range(st):
            a=line[x-bpp] if x>=bpp else 0;b=prev[x];c_=prev[x-bpp] if x>=bpp else 0
            if f==1:line[x]=(line[x]+a)&255
            elif f==2:line[x]=(line[x]+b)&255
            elif f==3:line[x]=(line[x]+(a+b)//2)&255
            elif f==4:
                pa=abs(b-c_);pb=abs(a-c_);pc=abs(a+b-2*c_)
                pr=a if pa<=pb and pa<=pc else (b if pb<=pc else c_)
                line[x]=(line[x]+pr)&255
        rows.append(line);prev=line
    return w,h,ch,bd,rows
for p in sys.argv[1:]:
    w,h,ch,bd,rows=dec(p)
    s=0;n=0;R=G=B=0;L=[]
    for y in range(0,h,8):
        r=rows[y]
        for x in range(0,w,8):
            k=x*ch*(bd//8)
            if ch<3: v=(r[k],)*3
            else: v=(r[k],r[k+2*(bd//8)//(bd//8)*0+ (bd//8)],r[k+2*(bd//8)])
            R+=v[0];G+=v[1];B+=v[2];n+=1
    print(p.split('/')[-1],w,h,'mean RGB 0-1: %.3f %.3f %.3f'%(R/n/255,G/n/255,B/n/255))
