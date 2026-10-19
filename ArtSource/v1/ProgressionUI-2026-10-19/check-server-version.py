import json,os,pathlib,websocket
ext=pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab'
cfg=json.loads((ext/'package.json').read_text(encoding='utf8'))
ws=websocket.create_connection('wss://api.pixellab.ai/get-latest-pixellab',timeout=30)
ws.send(json.dumps({k:cfg[k] for k in ('secret','tier','version')}))
r=json.loads(ws.recv());ws.close()
out={'installedVersion':cfg['version'],'responseKeys':list(r),'installedFilesChanged':False}
v=r.get('latest_plugin_version')
if isinstance(v,dict):
 for k,val in v.items():
  if k.endswith('package.json'):
   try: out['serverPackageVersion']=json.loads(val).get('version')
   except Exception: pass
elif isinstance(v,str):out['latest_plugin_version']=v
if 'detail' in r:out['detail']=r['detail']
pathlib.Path(__file__).with_name('server-version.json').write_text(json.dumps(out,indent=2),encoding='utf8')
print(json.dumps(out))
