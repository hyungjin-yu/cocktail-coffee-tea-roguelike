import pathlib
r=pathlib.Path(__file__).resolve().parent
block=bytes.fromhex('21ff0b')+b'NETSCAPE2.0'+bytes.fromhex('0301000000')
for p in (r/'preview').glob('*.gif'):
 data=p.read_bytes()
 if block in data:data=data.replace(block,b'');p.write_bytes(data)
print('GIF preview loop metadata removed; original Aseprite frame pixels unchanged')
