import websocket,json,pathlib
out=pathlib.Path(__file__).parent
for endpoint in ['check-latest-pixellab','get-latest-pixellab']:
 try:
  ws=websocket.create_connection('wss://api.pixellab.ai/'+endpoint,timeout=10)
  ws.send(json.dumps({'version':'0.4.130','tier':1}))
  data=json.loads(ws.recv()); ws.close()
  summary={'endpoint':endpoint,'keys':list(data)}
  if 'detail' in data: summary['detail']=data['detail']
  value=data.get('latest_plugin_version')
  if isinstance(value,str): summary['latest_plugin_version']=value
  elif isinstance(value,dict): summary['file_count']=len(value)
  print(json.dumps(summary))
 except Exception as e: print(endpoint,type(e).__name__,str(e)[:180])
