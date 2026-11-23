import sys,os,random
sys.path.insert(0,os.path.expanduser('~'))
exec(open(os.path.expanduser('~/mnt/Build A Doll/_Backup_2026-11-23_Levels10-25/generator/gen.py')).read())
P['Kit_Bollard']=P.get('Kit_Bollard')
COIN={'coinA':[(2,2),(1,10)],'coinB':[(0,30),(3,2)],'mcoin':[(0,50)]}
# element -> list of (prefab, dz, lateral, extra)
EL={
 'coneL':[('ConeHurdle',0,-1.8)],'coneR':[('ConeHurdle',0,1.8)],'coneM':[('ConeHurdle',0,0)],
 'fanL':[('Kit_Fan',0,-1.95)],'fanR':[('Kit_Fan',0,1.95)],
 'slimeL':[('Kit_SlimeTray',0,-2)],'slimeR':[('Kit_SlimeTray',0,2)],
 'puddleL':[('Kit_Puddle',0,-1.8)],'puddleR':[('Kit_Puddle',0,1.8)],'puddleM':[('Kit_Puddle',0,0)],
 'jump':[('JumpHurdles',0,0)],'slide':[('SlideHurdle',0,0)],
 'slideblock':[('Kit_SlideBlock',0,0)],'doors':[('Kit_ClosingDoors',0,0)],'pillars':[('Kit_ClosingPillars',0,0)],
 'pendulum':[('Kit_Pendulum',0,0)],'spinbar':[('Kit_Spinner_Bar',0,0)],'spincross':[('Kit_Spinner_Cross',0,0)],
 'spintri':[('Kit_Spinner_Tri',0,0)],'paddles':[('Kit_Paddles',0,0)],'roller':[('Kit_SpikeRoller',0,0)],
 'rollergate':[('Kit_RollerGate',0,0)],
 'sweepL':[('Kit_SweepArm_Long',0,-3)],'sweepR':[('Kit_SweepArm_Long',0,3)],
 'sweepS_L':[('Kit_SweepArm_Short',0,-3)],'sweepS_R':[('Kit_SweepArm_Short',0,3)],
 'punchL':[('Kit_Puncher',0,-2.4)],'punchR':[('Kit_Puncher',0,2.4)],
 'raillowL':[('Kit_SpikeRail_Low',0,-2)],'raillowR':[('Kit_SpikeRail_Low',0,2)],'railwide':[('Kit_SpikeRail_Wide',0,0)],
 'bollards':[('Kit_Bollard',0,x) for x in (-3.2,-2.2,-1.2,1.2,2.2,3.2)],
 'bollardsL':[('Kit_Bollard',0,x) for x in (-3.2,-2.2,-1.2,-0.2)],'bollardsR':[('Kit_Bollard',0,x) for x in (0.2,1.2,2.2,3.2)],
 'scissorsL':[('Kit_Scissors',0,-1)],'scissorsR':[('Kit_Scissors',0,1)],
 'adL':[('RewardedAdGate',0,-LANE)],'adR':[('RewardedAdGate',0,LANE)],
 'coinA':[('CoinGatePair',0,0,'coinA')],'coinB':[('CoinGatePair',0,0,'coinB')],'mcoin':[('CoinGate_SingleMoving',0,0,'mcoin')],
 'wave':[('Coins_Wave',0,0)],'line':[('Coins_Line',0,0)],
}
def build2(L):
    rnd=random.Random(L['num']*104729)
    v=variant(L['outfit']); family=L['outfit'].rsplit('_',1)[0]
    parts=sorted(v['parts'],key=lambda p:ORDER.index(p['slot'])); n=len(parts)
    first,last=L['first'],L['last']; gap=(last-first)/(n-1)
    a,b=L.get('after',11),L.get('before',10)
    E=[];warn=[]
    def put(name,z):
        for c in EL[name]: E.append(dict(prefab=c[0],z=round(z,1),lat=c[2],extra=c[3] if len(c)>3 else None))
    def spread(names,z0,z1,tag):
        k=len(names)
        if k==0: return
        zs=[(z0+z1)/2] if k==1 else [z0+i*(z1-z0)/(k-1) for i in range(k)]
        if k>1 and (z1-z0)/(k-1)<7.5: warn.append('%s spacing %.1f'%(tag,(z1-z0)/(k-1)))
        for nm,z in zip(names,zs): put(nm,z)
    put('line',10)
    spread(L['intro'],22,first-b,'intro')
    sides=L['sides']; assert len(sides)==n
    for i,p in enumerate(parts):
        g=first+i*gap; s=sides[i]
        ring=p['slot'] in RING
        pref='RingPair' if ring else ('GatePair_Moving' if i in L['moving'] else 'GatePair')
        assert not (ring and i in L['moving']),(L['num'],i)
        d=decoy(p,rnd,family,L['num'])
        E.append(dict(prefab=pref,z=round(g,1),lat=0,items=[p['guid'],d[1]] if s==2 else [d[1],p['guid']],green=s,part=p['name'],decoy=d[0]))
        if i<n-1: spread(L['gaps'][i],g+a,g+gap-b,'gap%d'%i)
    for nm in L.get('end',[]): pass
    put('line',last+16)
    E.sort(key=lambda e:(e['z'],e['lat']))
    L.update(_parts=parts,_entries=E,_vguid=v['guid'],_warn=warn)
    return L
_y=yaml
def yaml2(L):
    kinds=[]
    for e in L['_entries']:
        if e.get('extra') in COIN: kinds.append(e['extra']); e['extra']='coin'
    s=_y(L)
    old='    coinGates:\n    - operation: 2\n      amount: 2\n    - operation: 1\n      amount: 10\n'
    parts=s.split(old); out=parts[0]
    for k,rest in zip(kinds,parts[1:]):
        out+='    coinGates:\n'+''.join('    - operation: %d\n      amount: %d\n'%x for x in COIN[k])+rest
    assert len(parts)-1==len(kinds)
    return out
