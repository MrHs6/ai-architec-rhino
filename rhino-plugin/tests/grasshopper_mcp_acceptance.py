import subprocess,os,json,queue,threading,uuid,argparse
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description='Run only in a disposable Rhino document: creates graphs and bakes a box.')
parser.add_argument('--port',type=int,default=5190)
parser.add_argument('--output',type=Path,default=ROOT/'outputs'/'grasshopper-acceptance')
args=parser.parse_args()
OUT=args.output
OUT.mkdir(parents=True,exist_ok=True)
env=dict(os.environ,ARCHITEC_MCP_PORT=str(args.port))
p=subprocess.Popen([str(ROOT/'rhino-plugin/src/ArchiTec.McpBridge/bin/Release/net8.0/AIArchiTec.McpBridge.exe')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8',env=env)
lines=queue.Queue()
def reader():
 for line in p.stdout: lines.put(line)
threading.Thread(target=reader,daemon=True).start()
counter=0
def rpc(method,params):
 global counter
 counter+=1
 p.stdin.write(json.dumps({'jsonrpc':'2.0','id':counter,'method':method,'params':params})+'\n');p.stdin.flush()
 result=json.loads(lines.get(timeout=90))
 if 'error' in result: raise RuntimeError(result['error'])
 return result['result']
def tool(name,args):
 result=rpc('tools/call',{'name':name,'arguments':args})
 text='\n'.join(c.get('text','') for c in result.get('content',[]) if c['type']=='text')
 if result.get('isError'):raise RuntimeError(text)
 return json.loads(text)
try:
 initialized=rpc('initialize',{'protocolVersion':'2025-03-26','capabilities':{},'clientInfo':{'name':'ArchiTec Grasshopper Acceptance','version':'1.0'}})
 tools=rpc('tools/list',{})
 assert any(t['name']=='search_grasshopper_components' for t in tools['tools'])
 context=tool('get_context',{})
 doc=context['document_id']
 gh=tool('get_grasshopper_context',{'document_id':doc})
 catalog={q:tool('search_grasshopper_components',{'document_id':doc,'query':q,'limit':30}) for q in ['Box','Construct Domain']}
 (OUT/'catalog.json').write_text(json.dumps(catalog,indent=2),encoding='utf-8')
 (OUT/'context-before.json').write_text(json.dumps({'rhino':context,'grasshopper':gh},indent=2),encoding='utf-8')
 print(json.dumps({'server':initialized['serverInfo'],'document_id':doc,'discovered_components':{q:v['total'] for q,v in catalog.items()}}),flush=True)
 box=next(c for c in catalog['Box']['components'] if c['name']=='Domain Box')
 domain=next(c for c in catalog['Construct Domain']['components'] if c['name']=='Construct Domain')
 nodes=[];wires=[]
 for i,(name,value) in enumerate([('width',2),('depth',4),('height',3)]):
  nodes.append({'id':name,'kind':'slider','nickname':name.title(),'x':40,'y':80+i*120,'value':value,'minimum':0.1,'maximum':20})
  nodes.append({'id':name+'_domain','component_id':domain['component_id'],'x':300,'y':80+i*120,'inputs':[{'index':0,'type':'number','values':[0]}]})
  wires.append({'from':name,'output':0,'to':name+'_domain','input':1})
  wires.append({'from':name+'_domain','output':0,'to':'box','input':i+1})
 nodes.append({'id':'box','component_id':box['component_id'],'nickname':'Editable Box','x':580,'y':180,'inputs':[{'index':0,'type':'plane_xy','values':[[0,0,0]]}]})
 graph={'name':'Archi-Tec Parametric Box','nodes':nodes,'wires':wires,'bake_nodes':[]}
 def execute(graph,label,request_id=None):
  request={'document_id':doc,'request_id':request_id or str(uuid.uuid4()),'plan':{'version':1,'summary':label,'length_unit':'document','actions':[{'tool':'grasshopper_definition','parameters':graph}]}}
  (OUT/(label+'-request.json')).write_text(json.dumps(request,indent=2),encoding='utf-8')
  receipt=tool('execute_plan',request)
  (OUT/(label+'-receipt.json')).write_text(json.dumps(receipt,indent=2),encoding='utf-8')
  assert receipt['status']=='applied',receipt
  print('PASS '+label+' '+json.dumps({'definition_id':receipt['grasshopper']['definition_id'],'nodes':receipt['grasshopper']['node_count'],'baked':len(receipt['created'])}),flush=True)
  return request,receipt
 request,first=execute(graph,'Create-editable-definition')
 assert len(first['created'])==0 and first['grasshopper']['node_count']==7
 again=tool('execute_plan',request)
 assert again==first
 print('PASS Duplicate request returns the identical receipt',flush=True)
 graph['definition_id']=first['grasshopper']['definition_id']
 graph['nodes'][0]['value']=5
 graph['bake_nodes']=['box']
 request,second=execute(graph,'Update-width-and-bake')
 assert len(second['created'])==1
 actual=tool('get_objects',{'document_id':doc,'object_ids':second['created']})
 (OUT/'baked-box-inspection.json').write_text(json.dumps(actual,indent=2),encoding='utf-8')
 final=tool('get_grasshopper_context',{'document_id':doc})
 (OUT/'context-after.json').write_text(json.dumps(final,indent=2),encoding='utf-8')
 (OUT/'example-definition-plan.json').write_text(json.dumps(request['plan'],indent=2),encoding='utf-8')
 assert len(actual['objects'])==1
 solid=actual['objects'][0]
 assert solid['valid'] and solid['solid'] and abs(solid['volume']-60)<1e-6
 assert solid['info']['min']==[0,0,0] and solid['info']['max']==[5,4,3]
 print('BAKED '+json.dumps(actual),flush=True)
finally:
 p.stdin.close()
 try:p.wait(timeout=5)
 except subprocess.TimeoutExpired:p.terminate()
