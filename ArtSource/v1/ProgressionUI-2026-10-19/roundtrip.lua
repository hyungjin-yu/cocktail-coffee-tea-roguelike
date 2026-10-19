local root=app.params.root
app.fs.makeAllDirectories(root..'/qa/native-roundtrip')
for _,id in ipairs(app.fs.listFiles(root..'/assets')) do
 local dir=root..'/assets/'..id
 local source=dir..'/'..id..'.aseprite'
 if app.fs.isFile(source) then
  local s=app.open(source);app.fs.makeAllDirectories(root..'/qa/native-roundtrip/'..id)
  for i=1,#s.frames do
   local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,i)
   im:saveAs(root..'/qa/native-roundtrip/'..id..'/'..string.format('%02d',i-1)..'.png')
  end
  s:close()
 end
end
print('native roundtrip exports finished')
