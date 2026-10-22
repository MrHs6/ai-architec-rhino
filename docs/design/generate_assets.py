"""Generate editable, self-contained SVG documentation assets (Python standard library)."""
from pathlib import Path
from html import escape

OUT = Path(__file__).resolve().parents[1] / 'assets'
OUT.mkdir(exist_ok=True)
INK='#F3F2FA'; MUTED='#A5ABC4'; PURPLE='#AD89FF'; MINT='#8DE4CB'

def text(x,y,s,size=24,color=INK,weight=400):
    return f'<text x="{x}" y="{y}" font-size="{size}" fill="{color}" font-weight="{weight}">{escape(s)}</text>'
def lines(x,y,items,size=22,color=MUTED,step=32):
    return ''.join(text(x,y+i*step,s,size,color) for i,s in enumerate(items))
def rect(x,y,w,h,fill='#12162B',stroke='#30364F',r=20):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}"/>'
def path(d,color=PURPLE,width=2,dash='',arrow=False):
    return f'<path d="{d}" fill="none" stroke="{color}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"'+(f' stroke-dasharray="{dash}"' if dash else '')+(' marker-end="url(#arrow)"' if arrow else '')+'/>'
def begin(w,h,title,desc):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}" role="img" aria-labelledby="title desc">
<title id="title">{escape(title)}</title><desc id="desc">{escape(desc)}</desc>
<defs><linearGradient id="bg" x2="1" y2="1"><stop stop-color="#090C18"/><stop offset="1" stop-color="#15142D"/></linearGradient>
<linearGradient id="accent"><stop stop-color="#AA6DFF"/><stop offset="1" stop-color="#6579FF"/></linearGradient>
<marker id="arrow" markerWidth="10" markerHeight="10" refX="7" refY="4" orient="auto"><path d="M0 0L8 4L0 8" fill="none" stroke="#AD89FF" stroke-width="1.4"/></marker></defs>
<g font-family="Segoe UI,Arial,sans-serif"><rect width="{w}" height="{h}" rx="28" fill="url(#bg)"/>
<rect x="1" y="1" width="{w-2}" height="{h-2}" rx="27" fill="none" stroke="#303149"/>
'''
def end(name,s):
    (OUT/name).write_text(s+'</g></svg>\n',encoding='utf-8')
def heading(kicker,title,subtitle):
    return text(64,62,kicker,18,PURPLE,600)+text(64,114,title,42,INK,600)+text(64,153,subtitle,23,MUTED)
def pill(x,y,w,label):
    return rect(x,y,w,42,'#1C2039','#3A405D',21)+text(x+18,y+28,label,18,'#D9D4EF',500)

# Product cover: an architectural wireframe illustration, not a generated model screenshot.
s=begin(1600,780,'AI Archi-Tec for Rhino 8','Architectural modeling with your own API or a desktop MCP client. Conceptual architectural wireframe illustration; not a product output claim.')
for i in range(0,1600,48):s+=path(f'M{i} 0V780','#252840',.6)
for i in range(0,780,48):s+=path(f'M0 {i}H1600','#252840',.6)
s+='<rect x="1" y="1" width="800" height="778" fill="#0C0F1F" opacity=".92"/>'
s+=text(72,83,'AI ARCHI-TEC  /  RHINO 8',21,PURPLE,600)
s+=text(72,187,'Architecture,',74,INK,600)+text(72,274,'in your workflow.',74,INK,600)
s+=lines(76,340,['An architectural modeling assistant','for editable native Rhino geometry.'],29,'#BCC1D8',41)
s+=pill(76,462,260,'YOUR API. YOUR MODEL.')+pill(350,462,268,'DESKTOP MCP CLIENTS')
s+=text(77,567,'GENERATE',19,PURPLE,600)+text(263,567,'ANALYZE',19,PURPLE,600)+text(436,567,'MODEL ASSIST',19,PURPLE,600)
s+=path('M76 605H668','#343A57',1)
s+=text(76,658,'Preview → Apply → Refine',29,INK,500)+text(77,704,'Windows x64  ·  Rhino 8.31+  ·  Concept-modeling beta',19,MUTED)
# Axonometric building with recessed courtyard and horizontal floor plates.
def iso(x,y,z):return (1040+x*1.03-y*.78,570+x*.32+y*.47-z)
def poly(points,fill,stroke=PURPLE,opacity=1):
    return '<polygon points="'+' '.join(f'{x:.1f},{y:.1f}' for x,y in points)+f'" fill="{fill}" stroke="{stroke}" stroke-width="1.5" opacity="{opacity}"/>'
foot=[(0,0),(320,0),(320,220),(210,220),(210,100),(110,100),(110,220),(0,220)]
s+='<g transform="translate(100 -15) scale(.9)">'
s+=poly([iso(x,y,0) for x,y in [(-45,-30),(365,-30),(365,270),(-45,270)]],'#171A32','#454665')
for a,b in zip(foot,foot[1:]+foot[:1]):
    s+=poly([iso(*a,0),iso(*b,0),iso(*b,275),iso(*a,275)],'#343251','#8B74C5',.65)
for z in [0,55,110,165,220,275]:
    s+=poly([iso(x,y,z) for x,y in foot],'#33304F' if z==275 else 'none','#C4AEFF',.95)
for x in range(0,321,40):
    a=iso(x,0,0);b=iso(x,0,275);s+=path(f'M{a[0]} {a[1]}L{b[0]} {b[1]}','#7F8DBB',1)
for y in range(0,221,44):
    for x in [0,320]:
        a=iso(x,y,0);b=iso(x,y,275);s+=path(f'M{a[0]} {a[1]}L{b[0]} {b[1]}','#7F8DBB',1)
s+=text(872,133,'DESIGN INTENT',17,MINT,600)+path('M880 152H988L1040 215',MINT,1)
s+='</g>'
s+=text(1240,698,'NATIVE GEOMETRY',17,MINT,600)+path('M1225 686H1155L1120 630',MINT,1)
s+=text(920,750,'CONCEPTUAL WORKFLOW ILLUSTRATION',14,MUTED)
end('product-cover.svg',s)

# Main workflow: five distinct stages and a real feedback loop.
s=begin(1600,620,'From design intent to native geometry','Describe, validate, preview, explicitly Apply in Rhino, then inspect and refine. Analyze is read-only.')
s+=heading('01 / MODELING WORKFLOW','From intent to editable geometry.','One proposal. A visible preview. A confirmed native result.')
cards=[('01','DESCRIBE',['Prompt + dimensions','Optional reference¹']),('02','VALIDATE',['Structured action plan','Units + target checks']),('03','PREVIEW',['Inspect in the viewport','No new geometry yet']),('04','APPLY',['Click Apply in Rhino','Native batch + Undo']),('05','REFINE',['Read measured results','Edit the same objects'])]
for i,(n,title,body) in enumerate(cards):
    x=64+i*300
    s+=rect(x,216,272,216,'#191A34' if i==3 else '#12162B','#8C6BDA' if i==3 else '#343A55')
    s+=text(x+24,258,n,21,MINT if i==3 else PURPLE,600)+text(x+24,305,title,25,INK,600)+lines(x+24,350,body,20,MUTED,30)
    if i<4:s+=path(f'M{x+280} 321H{x+294}',PURPLE,2,arrow=True)
s+=path('M1400 448V491H191V450',PURPLE,2,'7 7',True)
s+=rect(566,470,477,43,'#101428','#101428',10)+text(587,499,'ITERATE WITH DOCUMENT CONTEXT',19,PURPLE,600)
s+=text(64,563,'¹ Reference support depends on the connected model. Local Analyze reads geometry without modifying it.',19,MUTED)
end('modeling-workflow.svg',s)

# Technical topology. No account/tunnel provisioning is implied.
s=begin(1600,1060,'AI Archi-Tec connection architecture','Two conversation routes converge on validated plans, a viewport preview and explicit user Apply. MCP has a reconnecting local bridge; provider keys and session discovery are per Windows user.')
s+=heading('02 / CONNECTION ARCHITECTURE','Two routes. One Rhino document.','API chat runs in Rhino. MCP chat runs in your desktop AI client.')
s+=rect(48,196,1504,648,'#0D1222','#3C4360')+text(76,232,'YOUR WINDOWS WORKSTATION',17,MINT,600)
s+=rect(82,270,360,170)+text(106,311,'Rhino Studio · API route',25,INK,600)+lines(106,352,['Prompt + optional reference','Provider / endpoint / model','Independent API profiles'],21,MUTED,30)
s+=rect(82,493,360,170)+text(106,535,'Codex / Claude Desktop',25,INK,600)+lines(106,577,['Chat and attach references here','Use ai-architec-rhino tools'],21,MUTED,31)
s+=rect(508,493,355,170)+text(532,535,'Reconnecting MCP bridge',24,INK,600)+lines(532,578,['STDIO · UTF-8 JSON-RPC','Local HTTP · 127.0.0.1:5188'],21,MUTED,31)
s+=path('M450 578H493',PURPLE,2,arrow=True)+text(457,553,'MCP',15,PURPLE)
s+=rect(930,270,582,393,'#16172F','#8B6DD1')+text(960,315,'AI Archi-Tec · Rhino host',28,INK,600)
s+=lines(960,361,['Document context  /  read-only messages','Action-plan validation  /  scoped targets'],23,'#C1C7DD',36)
s+=rect(958,427,526,66,'#222140','#685384',14)+text(982,469,'Viewport preview → User clicks Apply',23,INK,600)
s+=rect(958,516,526,116,'#152D2F','#46786D',14)+text(983,556,'RhinoDoc · native objects',25,MINT,600)+text(983,594,'Measured feedback  ·  Stable GUIDs  ·  Undo',21,'#C4E3DC')
s+=path('M871 579H914',PURPLE,2,arrow=True)+text(877,555,'LOCAL',14,PURPLE)
s+=path('M450 354H914',PURPLE,2,arrow=True)+text(603,333,'ACTION PLAN',17,PURPLE,600)
s+=rect(82,708,781,96,'#141B30','#343F5A',14)+text(106,746,'Per-user Windows DPAPI',23,INK,600)+text(106,780,'Optional API-key storage  ·  Rotating local MCP session discovery',20,MUTED)
s+=path('M686 699V675',MINT,1,'4 5')
s+=rect(930,708,582,96,'#141B30','#343F5A',14)+text(955,746,'Native results return to your conversation',22,INK,600)+text(955,780,'post_message  ·  get_plan_status  ·  document context',19,MUTED)
s+=path('M1220 670V697',PURPLE,2,arrow=True)
# Remote provider is explicitly outside the workstation boundary.
s+=rect(82,896,781,112,'#211A33','#645071',17)+text(108,936,'Your configured AI provider · API route only',24,INK,600)+text(108,974,'HTTPS  ·  Your account, key, model and provider billing',21,MUTED)
s+=path('M65 354H28V951H69',PURPLE,2,arrow=True)
s+=lines(930,929,['Desktop AI clients use their own account access.','Browser ChatGPT needs separate connector/tunnel setup.','No public tunnel or account login is provisioned here.'],20,MUTED,32)
end('connection-architecture.svg',s)

# End-user install poster: no developer tools in the default path.
s=begin(1600,720,'Installation quick start','Download the Windows installer ZIP, extract all files, close Rhino, run install.cmd, reopen Rhino and type archtec. Then choose API chat or desktop MCP.')
s+=heading('03 / INSTALLATION QUICK START','From download to your first preview.','Windows x64 · Licensed Rhino 8.31+ within Rhino 8 · .NET 8 runtime')
steps=[('01','DOWNLOAD','Windows installer ZIP','Source code is not the installer.'),('02','INSTALL','Extract → close Rhino → install.cmd','Keep the extracted files together.'),('03','OPEN','Reopen Rhino → type archtec','Studio opens inside your document.')]
for i,(num,title,a,b) in enumerate(steps):
    x=64+i*500
    s+=rect(x,214,472,211)+text(x+26,258,num,22,PURPLE,600)+text(x+26,303,title,27,INK,600)+text(x+26,349,a,21,'#D7DCEC')+text(x+26,385,b,19,MUTED)
    if i<2:s+=path(f'M{x+479} 320H{x+493}',PURPLE,2,arrow=True)
s+=rect(64,470,721,171,'#181B33','#484A70')+text(91,512,'A / CHAT IN RHINO',20,PURPLE,600)+text(91,550,'API profiles → key → models → select a model',24,INK,500)+text(91,593,'Describe → Generate → inspect the preview → Apply',20,MUTED)
s+=rect(813,470,721,171,'#15252C','#3D655F')+text(840,512,'B / CHAT IN YOUR AI CLIENT',20,MINT,600)+text(840,550,'MCP → Codex / Claude Desktop → Connect',24,INK,500)+text(840,593,'First setup: refresh/reopen client once. Apply in Rhino.',20,MUTED)
s+=text(64,687,'No Visual Studio, Node or SDK needed for the ready-made installer. Full instructions: docs/INSTALLATION.en.md',19,MUTED)
end('installation-quickstart.svg',s)
print('Generated 4 self-contained English SVG assets in docs/assets')
