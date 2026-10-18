local root=app.params.root
local files=app.fs.listFiles(root..'/provenance')
local thumbs={}
for _,f in ipairs(files) do
 if f:match('%.raw%.png$') then
  local im=Image{fromFile=root..'/provenance/'..f}
  im:resize{width=im.width*4,height=im.height*4,method='nearest'}
  table.insert(thumbs,{f,im})
 end
end
local out=Image(640,math.max(1,math.ceil(#thumbs/4))*360,ColorMode.RGB)
out:clear(app.pixelColor.rgba(32,29,41,255))
for i,t in ipairs(thumbs) do
 local x=((i-1)%4)*160; local y=math.floor((i-1)/4)*360
 out:drawImage(t[2],Point(x+(160-t[2].width)//2,y+8))
 print(i..' '..t[1])
end
out:saveAs(root..'/raw-contact.png')
