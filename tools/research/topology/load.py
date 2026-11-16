import re,numpy as np
D=r"D:\code\c#\poe 2 access mod\host\Poe2Access\bin\Release\net10.0-windows\win-x64\Plugins\Poe2Accessibility\debug\\"
def load(stamp,low_first=True):
    t=open(D+f"terrain-{stamp}.txt").readline()
    px,py,n=map(int,re.search(r"\((\d+), (\d+)\), (\d+) bytes",t).groups())
    b=np.fromfile(D+f"terrain-{stamp}.walkable",np.uint8); rows=len(b)//n
    b=b[:rows*n].reshape(rows,n)
    g=np.empty((rows,n*2),np.uint8)
    a,c=(b&15,b>>4) if low_first else (b>>4,b&15)
    g[:,0::2]=a; g[:,1::2]=c
    return g,(px,py)
if __name__=="__main__":
    for s in ["20261108-124237","20261108-115707","20261108-133607","20261108-134540"]:
        for lf in (True,False):
            g,(px,py)=load(s,lf); print(s,lf,g.shape,g[py,px],g[py,px-1:px+2], (g>0).sum())
