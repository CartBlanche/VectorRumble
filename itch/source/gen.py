import math, random, sys, os
HERE=os.path.dirname(os.path.abspath(__file__))
OUT=os.path.join(HERE,'out')  # generated SVGs go here (not committed)
os.makedirs(OUT,exist_ok=True)
SH={
'Dest':('FF00FF',"0 -16 6 0 12 -6 12 2 11 6 8 10 4 12 0 13 -4 12 -8 10 -11 6 -12 2 -12 -6 -6 0"),
'Blas':('00FF00',"-2 -2 -13 0 -15 -10 -15 2 0 15 15 2 15 -10 13 0 2 -2 2 -10 0 -15 -2 -10"),
'Defe':('6495ED',"-15 -2 -11 -6 -7 -2 0 -9 7 -2 11 -6 15 -2 4 9 0 5 -4 9"),
'Nimb':('FF2222',"0 -10 -5 0 -2 10 0 4 2 10 5 0"),
'Bat':('FF0000',"1.03 4.99 0 3.02 -1 4.99 -2.99 0.96 -7.99 4.04 -8.98 8 -10.03 4.02 -10.05 -4.02 -9.04 -8.05 -7.98 -4.02 -2.98 -1.02 -1.02 -1.02 -0.03 -5.04 1.01 -1 3.05 -0.97 8 -4.12 9 -7.97 10.02 -4.02 9.97 3.99 8.96 8 7.97 4.02 3.01 1"),
}
def pts(s):
    v=[float(x) for x in s.split()]; return list(zip(v[::2],v[1::2]))
def f(x): return ('%.1f'%x).rstrip('0').rstrip('.')
def pathd(s): return 'M'+' '.join(f(a)+' '+f(b) for a,b in pts(s))+'Z'
def defs(w,h,used):
    d='<defs>'
    d+='<filter id="b" filterUnits="userSpaceOnUse" x="0" y="0" width="%d" height="%d"><feGaussianBlur stdDeviation="4"/></filter>'%(w,h)
    d+='<filter id="s" x="-60%" y="-60%" width="220%" height="220%"><feGaussianBlur stdDeviation="1.3"/></filter>'
    d+='<linearGradient id="t" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffc233"/><stop offset=".45" stop-color="#ff8400"/><stop offset="1" stop-color="#8a4fd0"/></linearGradient>'
    d+='<radialGradient id="v" cx=".5" cy=".5" r=".7"><stop offset=".5" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity=".7"/></radialGradient>'
    for k in used:
        d+='<path id="%s" d="%s" fill="none" stroke-linejoin="round"/>'%(k,pathd(SH[k][1]))
    # rocket (nose up), power-ups
    O,BL,LM,CB,W="#ff8c1a","#4db8ff","#9dff2e","#6495ed","#fff"
    # the real in-game pickup icons (Core/Gameplay/*PowerUp.cs), at the in-game 1.25 scale
    d+='<g id="rk" stroke-linejoin="round" fill="none"><path d="M0 -12.5 L2.75 -5 L3.5 6.25 H-3.5 L-2.75 -5Z" stroke="%s"/><path d="M-2.25 6.25 L0 13.1 L2.25 6.25" stroke="%s"/></g>'%(O,BL)
    d+='<g id="p1" fill="none" stroke-linejoin="round"><circle r="20" stroke="%s"/><path d="M-13.75 -8.75 H6.25 L12.5 -6.25 L6.25 -3.75 H-13.75Z M-13.75 3.75 H6.25 L12.5 6.25 L6.25 8.75 H-13.75Z" stroke="%s"/></g>'%(W,LM)
    d+='<g id="p2" fill="none" stroke-linejoin="round"><path d="M0 18.75 L-18.75 -15 H18.75Z M0 7.5 L2.5 10.6 L0 13.75 L-2.5 10.6Z" stroke="%s"/><path d="M0 -12.5 L3.1 -3.75 L0 5 L-3.1 -3.75Z M-10 -10 L-5 -5 L-1.9 3.75 L-7.5 -1.25Z M10 -10 L5 -5 L1.9 3.75 L7.5 -1.25Z" stroke="%s"/></g>'%(W,CB)
    d+='<g id="p3" fill="none" stroke-linejoin="round"><path d="M-17.5 -10 V-17.5 H-10 M10 -17.5 H17.5 V-10 M17.5 10 V17.5 H10 M-10 17.5 H-17.5 V10" stroke="%s"/><path d="M8.84 -8.84 L5.48 -1.59 L-1.95 6.9 L-6.9 1.95 L1.59 -5.48Z" stroke="%s"/><path d="M-6.01 2.83 L-9.28 9.28 L-2.83 6.01" stroke="%s"/></g>'%(W,O,BL)
    d+='</defs>'
    return d
def ship(k,x,y,rot,s,op=1):
    c='#'+SH[k][0]
    return ('<g transform="translate(%s %s) rotate(%s) scale(%s)" opacity="%s"><use href="#%s" stroke="%s" stroke-width="%s" filter="url(#s)"/>'
            '<use href="#%s" stroke="%s" stroke-width="%s"/><use href="#%s" stroke="#fff" stroke-opacity=".55" stroke-width="%s"/></g>')%(f(x),f(y),f(rot),f(s),op,k,c,f(7/s),k,c,f(2.6/s),k,f(.9/s))
def icon(k,x,y,s,rot=0):  # powerups and rocket, glow + crisp
    return ('<g transform="translate(%s %s) rotate(%s) scale(%s)"><use href="#%s" stroke-width="%s" filter="url(#s)" opacity=".9"/><use href="#%s" stroke-width="%s"/></g>')%(f(x),f(y),f(rot),f(s),k,f(3.2/s),k,f(2.2/s))
def glowgrp(inner,gid):
    return '<g id="%s">%s</g><use href="#%s" filter="url(#b)"/><use href="#%s"/>'%(gid,inner,gid,gid)
def line(x1,y1,x2,y2,c,w=2): return '<path d="M%s %sL%s %s" stroke="%s" stroke-width="%s" stroke-linecap="round"/>'%(f(x1),f(y1),f(x2),f(y2),c,w)
def stars(w,h,n,seed):
    r=random.Random(seed); d=''.join('M%d %dh1'%(r.randrange(w),r.randrange(h)) for _ in range(n))
    return '<path d="%s" stroke="#9aa6ff" stroke-width="1.3" stroke-linecap="round" opacity=".55"/>'%d
def burst(cx,cy,r0,r1,n,seed,cols=('#ffe14a','#ff9a1a','#ff3b1a','#fff2a0')):
    r=random.Random(seed); g={c:'' for c in cols}
    for _ in range(n):
        a=r.uniform(0,6.2832); d=r.uniform(r0,r1); L=r.uniform(4,18)*(0.4+d/r1); c=r.choice(cols)
        g[c]+='M%s %sl%s %s'%(f(cx+d*math.cos(a)),f(cy+d*math.sin(a)),f(L*math.cos(a)),f(L*math.sin(a)))
    return ''.join('<path d="%s" stroke="%s" stroke-width="2.2" stroke-linecap="round"/>'%(v,c) for c,v in g.items())
def rock(cx,cy,R,seed):
    r=random.Random(seed); n=r.randrange(9,13); p=[]
    for i in range(n):
        a=6.2832*i/n; d=R*r.uniform(.78,1.08); p.append((cx+d*math.cos(a),cy+d*math.sin(a)))
    return '<path d="M%sZ" fill="#05050c" stroke="#cfd6ff" stroke-width="1.6" stroke-linejoin="round" opacity=".85"/>'%' '.join(f(a)+' '+f(b) for a,b in p)
def chamfer(x,y,w,h,c): return 'M%s %sH%sL%s %sV%sL%s %sH%sL%s %sV%sZ'%(f(x+c),f(y),f(x+w-c),f(x+w),f(y+c),f(y+h-c),f(x+w-c),f(y+h),f(x+c),f(x),f(y+h-c),f(y+c))
def frame(x,y,w,h,c,col,wd=3,gid=None):
    d=chamfer(x,y,w,h,c)
    return '<path d="%s" fill="none" stroke="%s" stroke-width="%s" filter="url(#b)" opacity=".9"/><path d="%s" fill="none" stroke="%s" stroke-width="%s"/>'%(d,col,wd*2.2,d,col,wd)
FONT="'Audiowide','Orbitron',Impact,'Arial Black',sans-serif"
import base64
LOGO=base64.b64encode(open(os.path.join(HERE,'..','..','Content','Textures','title.png'),'rb').read()).decode()
def title(cx,y,w,size,sw=None):
    h=w*229/1920.0
    ty=(y-0.35*size)-h/2
    im='<image x="%s" y="%s" width="%s" height="%s" xlink:href="data:image/png;base64,%s"/>'%(f(cx-w/2),f(ty),f(w),f(h),LOGO)
    glow=im.replace('<image','<image filter="url(#b)" opacity=".55"',1)
    return glow+im
def tag(cx,y,w,size,txt):
    w=len(txt)*size*0.74
    return '<text x="%s" y="%s" text-anchor="middle" font-family="\'Orbitron\',\'Eurostile\',\'Arial\',sans-serif" font-weight="600" font-size="%s" textLength="%s" lengthAdjust="spacingAndGlyphs" fill="#eaf0ff" stroke="#000" stroke-width="%s" paint-order="stroke">%s</text>'%(f(cx),f(y),f(size),f(w),f(size*.2),txt)
def svg(w,h,used,body,name):
    s='<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 %d %d" width="%d" height="%d">'%(w,h,w,h)
    s+=defs(w,h,used)+'<rect width="%d" height="%d" fill="#02020a"/>'%(w,h)+vig(w,h)+body+'</svg>'
    open(os.path.join(OUT,name),'w').write(s); print(name,len(s))
    return s
def face(x1,y1,x2,y2): return math.degrees(math.atan2(y2-y1,x2-x1))+90

def vig(w,h): return '<rect width="%d" height="%d" fill="url(#v)"/>'%(w,h)

# ================= A: SWARM =================
def A_banner():
    b=stars(1600,400,160,1)
    sp=burst(560,200,10,70,0,1)
    swarm=''; r=random.Random(5)
    # swarm in V streaming from right; hero at left-centre in front of title area flank
    hx,hy=250,205
    for i in range(18):
        row=i//2+1; side=1 if i%2 else -1
        x=1190+row*27+r.uniform(-5,5); y=200+side*row*17+r.uniform(-6,6)
        swarm+=ship(['Nimb','Bat','Nimb'][i%3],x,y,face(x,y,hx,hy)+r.uniform(-4,4),2.2 if i%3 else 2.0,.95)
    lasers=''.join(line(1250-i*7,200+(i%3-1)*20,1215-i*7,200+(i%3-1)*20,'#ff3030',2.2) for i in range(0,6))
    ls=glowgrp(''.join(line(1100+o,y,1070+o,y2,'#ff4040',2.4) for o,y,y2 in [(0,95,120),(-30,310,285),(20,350,322),(-60,70,92)]),'l1')
    sparks=glowgrp(burst(250,205,40,150,90,3),'sp')
    b+=sparks+swarm+ls
    b+=ship('Dest',hx,hy,90,5.5)
    b+=line(335,205,420,205,'#ff7ad9',3)  # hero laser (visual)
    b+=title(800,205,700,94)+tag(800,262,520,24,'ONE SHIP. A WHOLE SWARM. LAST PILOT FLYING WINS.')
    b+=icon('p1',560,330,1.3)+icon('rk',1060,70,1.5,70)
    return svg(1600,400,['Dest','Nimb','Bat'],b,'A_swarm_banner.svg')
def A_cover():
    b=stars(630,500,100,2)
    r=random.Random(9); sw=''
    hx,hy=315,250
    for i in range(18):
        a=r.uniform(-1.2,1.2)-math.pi/2+0; d=r.uniform(190,260)
        x=hx+d*math.cos(a)*1.15; y=hy+d*math.sin(a)*0.9-10
        if y>230: continue
        sw+=ship(['Nimb','Bat','Nimb'][i%3],x,y,face(x,y,hx,hy),2.1,.95)
    b+=glowgrp(burst(hx,hy+10,70,190,70,4),'sp')+sw
    b+=glowgrp(''.join(line(hx+dx,hy-40+k*0,hx+dx*1.0,hy-95,'#ff7ad9',3) for dx in (-16,16) for k in [0]),'hl')
    b+=ship('Dest',hx,hy+30,0,8.5)
    b+=title(315,410,560,78)+tag(315,455,540,17,'ONE SHIP. A WHOLE SWARM.')
    return svg(630,500,['Dest','Nimb','Bat'],b,'A_swarm_cover.svg')

# ================= B: FOUR-WAY DOGFIGHT =================
def B_banner():
    b=stars(1600,400,150,11)
    b+=frame(14,14,1572,372,40,'#3d5bff',3)
    rr=random.Random(3)
    b+=rock(180,320,34,1)+rock(1420,90,30,2)+rock(1080,340,22,3)+rock(520,60,20,4)
    cx,cy=800,200
    pos=[('Dest',250,110),('Blas',1350,120),('Defe',300,300),('Nimb',1300,300)]
    ls=''
    for k,x,y in pos:
        ang=math.atan2(cy-y,cx-x); 
        ls+=line(x+60*math.cos(ang),y+60*math.sin(ang),x+110*math.cos(ang),y+110*math.sin(ang),'#'+SH[k][0],2.6)
    # cross lasers between neighbours
    ls+=line(1195,135,1130,148,'#00ff00',2.6)+line(380,282,440,268,'#6495ed',2.6)+line(1190,282,1130,268,'#ff3030',2.6)+line(350,128,410,140,'#ff00ff',2.6)
    b+=glowgrp(ls,'l')
    b+=glowgrp(burst(1215,128,3,24,16,8)+burst(380,138,3,24,16,9),'sp')
    for k,x,y in pos:
        b+=ship(k,x,y,face(x,y,cx,cy),3.6 if k!='Nimb' else 4.4)
    b+=icon('p2',650,66,2.0)+icon('p3',950,66,2.0)+icon('p1',800,332,1.8)
    b+=title(800,195,640,86)+tag(800,252,560,24,'FOUR PILOTS. ONE ARENA. ZERO MERCY.')
    return svg(1600,400,['Dest','Blas','Defe','Nimb'],b,'B_dogfight_banner.svg')
def B_cover():
    b=stars(630,500,100,12)+frame(12,12,606,476,34,'#3d5bff',3)
    b+=rock(70,430,28,5)+rock(570,70,24,6)
    pos=[('Dest',120,120,50),('Blas',510,120,130),('Defe',120,300,-30),('Nimb',510,300,-150)]
    ls=''
    for k,x,y,_ in pos:
        ang=math.atan2(210-y,315-x); ls+=line(x+50*math.cos(ang),y+50*math.sin(ang),x+95*math.cos(ang),y+95*math.sin(ang),'#'+SH[k][0],3)
    b+=glowgrp(ls+burst(315,205,4,40,30,2),'l')
    for k,x,y,_ in pos: b+=ship(k,x,y,face(x,y,315,205),4.6 if k!='Nimb' else 5.8)
    b+=icon('p1',315,60,1.8)
    b+=title(315,410,540,74)+tag(315,452,520,17,'FOUR PILOTS. ONE ARENA.')
    return svg(630,500,['Dest','Blas','Defe','Nimb'],b,'B_dogfight_cover.svg')

# ================= C: FRAME + ORBIT =================
def orbit(cx,cy,rx,ry,items,w,h,ships_s):
    s='<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="none" stroke="#6a7cff" stroke-width="1.4" stroke-dasharray="3 9" opacity=".6"/>'%(f(cx),f(cy),f(rx),f(ry))
    out=''
    for kind,k,t in items:
        a=math.radians(t); x=cx+rx*math.cos(a); y=cy+ry*math.sin(a)
        tx=-rx*math.sin(a); ty=ry*math.cos(a); rot=face(0,0,tx,ty)
        if kind=='s': out+=ship(k,x,y,rot,ships_s)
        else: out+=icon(k,x,y,1.3)
    return s+out
def C_banner():
    b=stars(1600,400,140,21)
    b+=glowgrp(burst(800,200,300,700,60,5,('#3d5bff','#8a4bff','#6a7cff')),'dust')
    items=[('s','Dest',200),('s','Defe',255),('i','p1',305),('s','Nimb',350),('s','Blas',20),('i','p2',60),('s','Bat',100),('i','p3',150)]
    b+=orbit(800,200,700,150,items,1600,400,2.6)
    fx,fy,fw,fh=450,95,700,210
    b+='<path d="%s" fill="#06061a" opacity=".85"/>'%chamfer(fx,fy,fw,fh,36)
    b+=frame(fx,fy,fw,fh,36,'#3d5bff',3)
    b+=frame(fx+10,fy+10,fw-20,fh-20,28,'#8a4bff',1.5)
    b+=title(800,198,590,84)+tag(800,262,470,20,'PICK A SHIP. GRAB POWER-UPS. OWN THE ARENA.')
    return svg(1600,400,['Dest','Defe','Nimb','Blas','Bat'],b,'C_frame_orbit_banner.svg')
def C_cover():
    b=stars(630,500,100,22)
    items=[('s','Dest',210),('s','Defe',255),('i','p1',-60),('s','Nimb',300),('s','Blas',20),('i','p2',55),('s','Bat',110),('i','p3',160)]
    b+=orbit(315,250,282,210,items,630,500,2.8)
    fx,fy,fw,fh=110,160,410,180
    b+='<path d="%s" fill="#06061a" opacity=".9"/>'%chamfer(fx,fy,fw,fh,30)
    b+=frame(fx,fy,fw,fh,30,'#3d5bff',3)+frame(fx+9,fy+9,fw-18,fh-18,22,'#8a4bff',1.5)
    b+=title(315,252,350,54)+tag(315,300,400,15,'OWN THE ARENA')
    return svg(630,500,['Dest','Defe','Nimb','Blas','Bat'],b,'C_frame_orbit_cover.svg')

# ================= D: SHATTERED =================
def shatter(k,cx,cy,s,rot,push,seed):
    P=pts(SH[k][1]); r=random.Random(seed); col='#'+SH[k][0]; out=''
    ra=math.radians(rot)
    for i in range(len(P)):
        a=P[i]; b=P[(i+1)%len(P)]
        mx,my=(a[0]+b[0])/2,(a[1]+b[1])/2
        L=math.hypot(mx,my) or 1; dx,dy=mx/L,my/L; d=push*r.uniform(.5,1.3); tw=r.uniform(-.5,.5)
        def tr(p):
            px,py=p[0]+dx*d,p[1]+dy*d
            c,sn=math.cos(tw+ra),math.sin(tw+ra)
            qx=(px-mx-dx*d)*c-(py-my-dy*d)*sn+mx+dx*d; qy=(px-mx-dx*d)*sn+(py-my-dy*d)*c+my+dy*d
            return (cx+qx*s,cy+qy*s)
        A,B=tr(a),tr(b)
        out+='M%s %sL%s %s'%(f(A[0]),f(A[1]),f(B[0]),f(B[1]))
    return '<path d="%s" stroke="%s" stroke-width="3.2" stroke-linecap="round"/>'%(out,col)
def D_banner():
    b=stars(1600,400,150,31)
    cx,cy=800,125
    b+=glowgrp(burst(cx,cy,25,150,130,7)+burst(cx,cy,10,60,60,8,('#fff7c0','#ffe14a')),'sp')
    b+='<circle cx="%d" cy="%d" r="26" fill="#fff6c8" filter="url(#b)"/>'%(cx,cy)
    b+=glowgrp(shatter('Defe',cx,cy,3.4,0,7,2)+shatter('Dest',cx,cy,3.0,40,5,3),'sh')
    b+=glowgrp(line(420,cy+8,690,cy,'#ffe14a',3)+line(380,cy+12,420,cy+8,'#ffe14a',3),'ls')
    b+=icon('rk',1150,95,2.4,-76)+icon('p1',1330,330,1.2)
    b+=title(800,300,760,92)+tag(800,356,640,22,'EVERY HIT ENDS IN SPARKS.')
    return svg(1600,400,['Dest','Defe'],b,'D_shatter_banner.svg')
def D_cover():
    b=stars(630,500,100,32)
    cx,cy=315,185
    b+=glowgrp(burst(cx,cy,30,260,170,7)+burst(cx,cy,10,80,70,8,('#fff7c0','#ffe14a')),'sp')
    b+='<circle cx="%d" cy="%d" r="30" fill="#fff6c8" filter="url(#b)"/>'%(cx,cy)
    b+=glowgrp(shatter('Defe',cx,cy,6.0,0,7,2)+shatter('Dest',cx,cy,5.2,40,5,3),'sh')
    b+=icon('rk',560,60,2.2,-80)
    b+=title(315,415,570,80)+tag(315,460,520,17,'EVERY HIT ENDS IN SPARKS.')
    return svg(630,500,['Dest','Defe'],b,'D_shatter_cover.svg')
for fn in (B_banner,B_cover): fn()

import re
for fn in os.listdir(OUT):
    if fn.endswith('.svg') and not fn.startswith('pv_'):
        t=open(os.path.join(OUT,fn)).read()
        m=re.search(r'width="(\d+)" height="(\d+)"',t); w,h=int(m[1]),int(m[2]); k=1000/w if w>700 else 1
        k=min(1,760/w)
        open(os.path.join(OUT,'pv_'+fn),'w').write(t.replace('width="%d" height="%d"'%(w,h),'width="%d" height="%d"'%(w*k,h*k),1))
