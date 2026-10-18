local root=app.params.root;local pc=app.pixelColor
app.fs.makeAllDirectories(root..'/qa')
local ids={'dl_leaf_black_tea','dl_leaf_green_tea','dl_leaf_heap_black_tea','dl_leaf_heap_green_tea','dl_steam','dl_pour_stream','dl_bubbles','dl_ice_cube','dl_buff_gain','dl_barspoon','dl_shaker_shake','dl_drink_reveal'}
local sheet=Image(900,160*#ids,ColorMode.RGB);sheet:clear(pc.rgba(32,29,41,255))
for row,id in ipairs(ids) do
 for f=0,5 do local path=root..'/'..id..'_'..string.format('%02d',f)..'.png';if app.fs.isFile(path) then
  local im=Image{fromFile=path};local z=math.min(5,140/im.width,148/im.height);im:resize{width=math.floor(im.width*z),height=math.floor(im.height*z),method='nearest'};sheet:drawImage(im,Point(f*150+(150-im.width)//2,(row-1)*160+6))
 end end
 print('motion row '..row..' '..id)
end
sheet:saveAs(root..'/qa/motion-contact.png')
local vessels={'dl_jigger','dl_mixing_glass','dl_glass_cocktail','dl_shaker_open','dl_chaha','dl_teapot','dl_teacup'}
local fill=Image(960,240*#vessels,ColorMode.RGB);fill:clear(pc.rgba(32,29,41,255))
local cols={pc.rgba(167,177,194,255),pc.rgba(49,148,221,255),pc.rgba(196,43,72,255),pc.rgba(239,216,139,255)}
for row,id in ipairs(vessels) do if app.fs.isFile(root..'/'..id..'_mask.png') then
 local art=Image{fromFile=root..'/'..id..'.png'};local mask=Image{fromFile=root..'/'..id..'_mask.png'}
 local top,bottom=mask.height,-1;for p in mask:pixels() do if pc.rgbaA(p())>0 then top=math.min(top,p.y);bottom=math.max(bottom,p.y) end end
 for j=0,3 do local im=Image(art.width,art.height,ColorMode.RGB)
  local amount=({.1,.5,.9,.65})[j+1]
  for p in mask:pixels() do if pc.rgbaA(p())>0 and p.y>=bottom-(bottom-top)*amount then im:drawPixel(p.x,p.y,cols[j+1]) end end
  im:drawImage(art,Point(0,0));local z=math.min(4,220/im.width,220/im.height);im:resize{width=math.floor(im.width*z),height=math.floor(im.height*z),method='nearest'}
  fill:drawImage(im,Point(j*240+(240-im.width)//2,(row-1)*240+6))
 end
end end
fill:saveAs(root..'/qa/fill-colour-contact.png')
