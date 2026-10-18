local root=app.params['root']
local names={'card_gin','card_jigger','card_bar_spoon','card_shaker','card_double_strain','card_negroni','card_old_fashioned','card_martini','card_blue_blazer','card_green_tea','card_tea_whisk','card_gaiwan','card_oolong','card_puerh','card_chamomile','card_rooibos'}
local pc=app.pixelColor
local sheet=Image(768,768,ColorMode.RGB)
sheet:clear(pc.rgba(41,35,40,255))
app.fs.makeAllDirectories(root..'/roundtrip')
for i,name in ipairs(names) do
 if app.fs.isFile(root..'/'..name..'.png') then
 local im=Image{fromFile=root..'/'..name..'.png'}
 im:resize{width=128,height=128,method='nearest'}
 local col=(i-1)%4; local row=math.floor((i-1)/4)
 sheet:drawImage(im,Point(col*192+32,row*192+32))
 local s=app.open(root..'/'..name..'.aseprite')
 assert(#s.layers==2,'Expected source and clean layers')
 local out=Image(s.width,s.height,ColorMode.RGB); out:drawSprite(s,1)
 out:saveAs(root..'/roundtrip/'..name..'.png')
 s:close()
 end
end
sheet:saveAs(root..'/contact-preview.png')
