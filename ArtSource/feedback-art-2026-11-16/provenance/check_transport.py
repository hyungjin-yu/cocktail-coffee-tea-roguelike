import websocket,json
results=[]
for url in ['ws://api.pixellab.ai/check-latest-pixellab','wss://api.pixellab.ai/check-latest-pixellab']:
 try:
  ws=websocket.create_connection(url,timeout=8); result={'url':url,'handshake':'ok'}; ws.close()
 except Exception as e:
  result={'url':url,'error_type':type(e).__name__,'error':str(e)[:350]}
 results.append(result)
print(json.dumps(results,indent=2))
