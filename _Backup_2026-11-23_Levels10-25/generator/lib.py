import json,re,os,glob
B=os.path.expanduser('~/mnt/Build A Doll/Assets/')
M=json.load(open(os.path.expanduser('~/guidmap.json')))
G={os.path.normpath(v):k for k,v in M.items()}
SLOTS=['Hair','Makeup','Top','Bottom','Dress','Shoes','Bag','Accessory','Hat','HandItem','Glasses','Wings','Socks']
def rd(g): return open(os.path.join(B,M[g])).read()
def refs(t,key):
    m=re.search(key+r':\n((?:  - \{.*\}\n)*)',t)
    return re.findall(r'guid: (\w+)',m.group(1)) if m else []
def item(g):
    t=rd(g); return dict(guid=g,name=re.search(r'm_Name: (.*)',t).group(1),slot=int(re.search(r'\n  slot: (\d+)',t).group(1)),icon='fileID: 0' not in re.search(r'icon: (.*)',t).group(1))
def variant(name):
    p=glob.glob(B+'_Core/_Data/_Outfits/*/%s.asset'%name)[0]
    t=open(p).read()
    own=refs(t,'parts')
    og=re.search(r'\n  outfit: \{fileID: \d+, guid: (\w+)',t).group(1)
    shared=refs(rd(og),'sharedParts')
    allp=[];used=set();dups=[]
    for g in own:
        it=item(g)
        if it['slot'] in used: dups.append(it['name'])
        else: used.add(it['slot']);allp.append(it)
    for g in shared:
        it=item(g)
        if it['slot'] not in used: used.add(it['slot']);allp.append(it)
    vg=G[os.path.normpath(os.path.relpath(p,B))] if os.path.normpath(os.path.relpath(p,B)) in G else None
    return dict(name=name,guid=vg,parts=allp,dups=dups)
