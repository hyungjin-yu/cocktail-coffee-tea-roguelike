local base=app.params['root']
local defs={
 {'card_gin','card_gin',64,64,58,58},
 {'card_jigger','card_jigger',64,64,58,58},
 {'card_bar_spoon','card_bar_spoon',64,64,58,58},
 {'card_shaker','card_shaker',64,64,58,58},
 {'card_double_strain','card_double_strain',64,64,58,58},
 {'card_negroni','card_negroni',64,64,58,58},
 {'card_old_fashioned','card_old_fashioned',64,64,58,58},
 {'card_martini','card_martini',64,64,58,58},
 {'card_blue_blazer','card_blue_blazer',64,64,58,58},
 {'card_green_tea','card_green_tea',64,64,58,58},
 {'card_tea_whisk','card_tea_whisk',64,64,58,58},
 {'card_gaiwan','card_gaiwan',64,64,58,58},
 {'card_oolong','card_oolong',64,64,58,58},
 {'card_puerh','card_puerh',64,64,58,58},
 {'card_chamomile','card_chamomile',64,64,58,58},
 {'card_rooibos','card_rooibos',64,64,58,58}
}
local out=app.params['out']
app.fs.makeAllDirectories(out)
local pc=app.pixelColor
for _,d in ipairs(defs) do
 local rawpath=base..'/'..d[1]..'.raw.png'
 if app.fs.isFile(rawpath) and app.fs.isFile(base..'/'..d[1]..'.receipt.json') then
 local raw=Image{fromFile=rawpath}
 local minx,miny,maxx,maxy=raw.width,raw.height,-1,-1
 for p in raw:pixels() do
  local c=p(); if pc.rgbaA(c)>=128 then
   p(pc.rgba(pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c),255))
   minx=math.min(minx,p.x); miny=math.min(miny,p.y); maxx=math.max(maxx,p.x); maxy=math.max(maxy,p.y)
  else p(0) end
 end
 assert(maxx>=minx,'Empty output')
 local bw,bh=maxx-minx+1,maxy-miny+1
 local crop=Image(bw,bh,ColorMode.RGB); crop:drawImage(raw,Point(-minx,-miny))
 local scale=math.min(d[5]/bw,d[6]/bh)
 local nw,nh=math.max(1,math.floor(bw*scale+.5)),math.max(1,math.floor(bh*scale+.5))
 -- Deliberately broaden the vial body so its orange glass reads at gameplay scale.
 if d[1]=='pickup_cocktail' then nw=22; nh=28 end
 crop:resize{width=nw,height=nh,method='nearest'}
 local s=Sprite(d[3],d[4],ColorMode.RGB)
 s.layers[1].name='PixelLab normalized - crisp alpha'
 local x,y=math.floor((d[3]-nw)/2),math.floor((d[4]-nh)/2)
 s:newCel(s.layers[1],1,crop,Point(x,y))
 local source=s:newLayer(); source.name='PixelLab original - retained'; source.isVisible=false
 s:newCel(source,1,Image{fromFile=rawpath},Point(0,0))
 app.activeSprite=s
 s:saveAs(out..'/'..d[2]..'.aseprite')
 local export=Image(d[3],d[4],ColorMode.RGB); export:drawImage(crop,Point(x,y)); export:saveAs(out..'/'..d[2]..'.png')
 print(d[2]..' '..d[3]..'x'..d[4]..' bounds '..x..','..y..','..nw..','..nh)
 s:close()
 end
end
