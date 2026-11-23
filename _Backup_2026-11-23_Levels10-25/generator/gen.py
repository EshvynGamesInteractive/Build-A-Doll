import sys,os,json,random,uuid,struct
sys.path.insert(0,os.path.expanduser('~'))
from lib import *
P=json.load(open(os.path.expanduser('~/prefabs.json')))
P['ConeHurdle']=('967a61fe6f72544a896ec0282a818097','3337685756884729604')
P['JumpHurdles']=('b2aa747aed715406ea60ab5b826b5753','8944613449996735016')
P['SlideHurdle']=('01dd75c4d9d064cc4aa3718f102935e2','5120549408769713660')
POOL={int(k):v for k,v in json.load(open(os.path.expanduser('~/pool.json'))).items()}
LV=B+'_Core/_Data/_Levels/'
AI=dict(Easy='49244a2ced3f64b56bc59204ec54fbbb',Normal='f06fae1e1c92a40adbe657d1a2fb91cd',Hard='660780bfe5970418998ef6ebbe1908a0',Expert='98dcd2fa8e74446938426f01a9b7a722')
ORDER=[3,4,2,0,5,12,8,10,9,6,11,7,1]  # Bottom/Dress, Top, Hair, Shoes, Socks, Hat, Glasses, HandItem, Bag, Wings, Accessory, Makeup
RING={0,8,10,9,6,11,7,1}
LANE=2.25

# ---------- chunks: list of (prefab, dz, lateral[, extra]) and occupied length ----------
def row(prefab,z,lats): return [(prefab,z,l) for l in lats]
C={
 'puddles':     ([('Kit_Puddle',0,-2.2),('Kit_Puddle',5,1.5),('Kit_Puddle',10,-0.8)],10),
 'slime_pair':  ([('Kit_SlimeTray',0,2),('Kit_SlimeTray',8,-2)],8),
 'fans':        ([('Kit_Fan',0,-1.95),('Kit_Fan',12,1.95)],12),
 'cones':       ([('ConeHurdle',0,-1.8),('ConeHurdle',8,1.8),('ConeHurdle',16,-1.8)],16),
 'bollard_gate':(row('Kit_Bollard',0,[-3.2,-2.2,-1.2,1.2,2.2,3.2]),1),
 'roadblock':   ([('ConeHurdle',0,-1.2)]+row('Kit_Bollard',8,[-0.6,0.6,1.8,3]),8),
 'hurdle_row':  ([('JumpHurdles',3.5,0)],4),
 'hurdle_double':([('JumpHurdles',3.5,0),('JumpHurdles',13.5,0)],14),
 'slide_gate':  ([('SlideHurdle',0,0)],2),
 'spinner_bar': ([('Kit_Spinner_Bar',0,0)],4),
 'spinner_cross':([('Kit_Spinner_Cross',0,0)],4),
 'spinner_tri': ([('Kit_Spinner_Tri',0,0)],4),
 'toy_walls':   ([('ConeHurdle',0,-LANE),('ConeHurdle',10,LANE)],10),
 'spike_rail_low':([('Kit_SpikeRail_Low',0,-2),('Kit_SpikeRail_Low',10,2)],10),
 'spike_rail_wide':(row('Kit_LowBlock',0,[-2.5,0,2.5])+[('Kit_SpikeRail_Wide',3.5,0)],4),
 'paddles':     ([('Kit_Paddles',0,0)],3),
 'sweep_long':  ([('Kit_SweepArm_Long',0,-3)],4),
 'sweep_short': ([('Kit_SweepArm_Short',0,3),('Kit_SweepArm_Short',10,-3)],10),
 'spike_roller':([('Kit_SpikeRoller',0,0)],2),
 'roller_gate': ([('Kit_RollerGate',0,0)],2),
 'closing_doors':([('Kit_ClosingDoors',0,0)],2),
 'closing_pillars':([('Kit_ClosingPillars',0,0)],2),
 'punchers':    ([('Kit_Puncher',0,-2.4),('Kit_Puncher',10,2.4)],10),
 'pendulum':    ([('Kit_Pendulum',0,0)],2),
 'pendulum_pair':([('Kit_Pendulum',0,0),('Kit_Pendulum',10,0)],10),
 'slide_block': ([('Kit_SlideBlock',0,0)],2),
 'coins_wave':  ([('Coins_Wave',0,0)],8),
 'coins_line':  ([('Coins_Line',0,0)],8),
 'coin_gate':   ([('CoinGatePair',0,0,'coin')],2),
 'ad_gate_l':   ([('RewardedAdGate',0,-LANE)],2),
 'ad_gate_r':   ([('RewardedAdGate',0,LANE)],2),
}
GUARDS={1:['Kit_LowBlock','Kit_Hurdle','ConeHurdle'],2:['Kit_LowBlock','Kit_Hurdle','ConeHurdle'],3:['ConeHurdle','Kit_Hurdle','Kit_SpikeRail_Low','Kit_LowBlock']}

LOCKED={'Nurse':13,'Police':17,'Maid':21,'Basketball':25}
def decoy(part,rnd,family,num):
    c=[x for x in POOL[part['slot']] if not x[0].startswith(family+'_')
       and not any(x[0].startswith(f+'_') and num<=u for f,u in LOCKED.items())]
    return rnd.choice(c)

def build(L):
    rnd=random.Random(L['num']*7919)
    v=variant(L['outfit']); family=L['outfit'].rsplit('_',1)[0]
    parts=sorted(v['parts'],key=lambda p:ORDER.index(p['slot']))
    n=len(parts); first,last=L.get('first',44),L.get('last',208)
    gap=(last-first)/(n-1)
    E=[]
    def add(pref,z,lat,**kw): E.append(dict(prefab=pref,z=round(z,1),lat=lat,**kw))
    def chunk(name,z):
        for c in C[name][0]: add(c[0],z+c[1],c[2],extra=c[3] if len(c)>3 else None)
    chunk('coins_line',10)
    chunk(L['intro'],first-20)
    moving=set(L.get('moving',[]))
    sides=[];
    for i,p in enumerate(parts):
        g=first+i*gap
        s=rnd.choice([1,2])
        if len(sides)>=2 and sides[-1]==sides[-2]==s: s=3-s
        sides.append(s)
        green=LANE if s==2 else -LANE
        ring=p['slot'] in RING
        pref='RingPair' if ring else ('GatePair_Moving' if i in moving else 'GatePair')
        d=decoy(p,rnd,family,L['num'])
        items=[p['guid'],d[1]] if s==2 else [d[1],p['guid']]   # hierarchy: index 0 = right gate, 1 = left gate
        # cash lure toward the red gate
        if L.get('lure',True):
            for k,(dz,f) in enumerate([(-8,0.35),(-5,0.7),(-2,1.0)]):
                add('CashBundle',g+dz,round(-green*f,2))
        add(pref,g,0,items=items,green=s,part=p['name'],decoy=d[0])
        if i==n-1:
            add(rnd.choice(GUARDS[L['tier']]),g+12,green); chunk('coins_line',g+20); break
        gc=L.get('guard_chunks',{}).get(i)
        if gc: chunk(gc,g+14); z=g+14+C[gc][1]+12
        else: add(GUARDS[L['tier']][i%len(GUARDS[L['tier']])],g+12,green); z=g+24
        end=g+gap-12 if L.get('lure',True) else g+gap-8
        for name in (L['gaps'][i] if i<len(L['gaps']) else []):
            ln=C[name][1]
            if z+ln>end: L.setdefault('_skipped',[]).append((i,name)); continue
            chunk(name,z); z+=ln+12
    E.sort(key=lambda e:(e['z'],e['lat']))
    L['_parts']=parts; L['_entries']=E; L['_vguid']=v['guid']
    return L

def ref(g,fid=11400000,t=2): return '{fileID: %s, guid: %s, type: %d}'%(fid,g,t)
def ints(xs): return ''.join(struct.pack('<i',x).hex() for x in xs)
def yaml(L):
    parts=L['_parts']; pp=L['pass']
    o=["%YAML 1.1","%TAG !u! tag:unity3d.com,2011:","--- !u!114 &11400000","MonoBehaviour:",
"  m_ObjectHideFlags: 0","  m_CorrespondingSourceObject: {fileID: 0}","  m_PrefabInstance: {fileID: 0}","  m_PrefabAsset: {fileID: 0}",
"  m_GameObject: {fileID: 0}","  m_Enabled: 1","  m_EditorHideFlags: 0",
"  m_Script: {fileID: 11500000, guid: 55c96d3c9fc6e46e98cd565346b51770, type: 3}",
"  m_Name: Level_%02d"%L['num'],"  m_EditorClassIdentifier: ",
"  displayName: "+L['name'],"  brief: "+L['brief'],"  showSwipeGuide: 0",
"  isSpecial: %d"%(1 if L.get('special') else 0),"  specialTitle: "+L.get('special',''),
"  specialIcon: {fileID: 0}","  specialIconItem: {fileID: 0}","  finishDistance: 268",
"  playerPrefab: {fileID: 5635630314507133769, guid: b6151af2959704b0190626de278d82f3, type: 3}",
"  outfit: "+ref(L['_vguid']),"  outfitPassPercent: %d"%pp,"  outfitStarPercents: "+ints([pp,pp,100]),
"  outfitMakeup: []","  gateOrder: "+ints([p['slot'] for p in parts]),
"  hasTargetSkin: 0","  targetSkin: {r: 1, g: 1, b: 1, a: 1}","  hasTargetDressColor: 0","  targetDressColor: {r: 1, g: 1, b: 1, a: 1}",
"  targetLook: {fileID: 0}","  hasAiOpponent: 1","  aiProfile: "+ref(AI[L['ai']]),"  startingOutfit:",
"  - {fileID: 11400000, guid: 832b10d8c20e04a8abb729a67a9ac22e, type: 2}","  - {fileID: 11400000, guid: e979a7ae83ca6413c917f29e1aafabdc, type: 2}","  entries:"]
    for e in L['_entries']:
        g,fid=P[e['prefab']]
        o.append("  - prefab: "+ref(g,fid,3)); o.append("    distance: %g"%e['z']); o.append("    lateral: %g"%e['lat'])
        if e.get('items'):
            o.append("    gateItems:"); o+=["    - "+ref(x) for x in e['items']]
        else: o.append("    gateItems: []")
        o.append("    greenSide: %d"%e.get('green',0)); o.append("    paintGates: []")
        if e.get('extra')=='coin':
            o+=["    coinGates:","    - operation: 2","      amount: 2","    - operation: 1","      amount: 10"]
        else: o.append("    coinGates: []")
        o.append("    skinGates: []"); o.append("    dressColorGates: []")
    o+=["  scoreToPass: 1","  starScores: 040000000a00000010000000","  completionCoins: %d"%L['coins'],""]
    return '\n'.join(o)
META="""fileFormatVersion: 2
guid: %s
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
