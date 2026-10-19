local root=app.params.root
local s=Sprite(8,8,ColorMode.RGB)
s:saveAs(root..'/aseprite-access-probe.aseprite')
s:close()
print('Aseprite batch Lua write verified')
