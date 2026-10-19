local root=app.params.root
local srcroot=app.params.source
local pc=app.pixelColor;local white=pc.rgba(255,255,255,255)
local defs={
 {id='gin',mouth=10,label={7,24,12,20},pts={{10,10},{15,10},{15,15},{18,19},{18,49},{16,51},{8,51},{6,49},{6,20},{10,15}}},
 {id='rum',mouth=15,label={4,26,11,17},pts={{11,15},{14,15},{14,18},{18,20},{20,23},{20,43},{17,46},{8,46},{5,43},{4,24},{7,21},{11,18}}},
 {id='blue_curacao',mouth=13,label={6,24,8,24},pts={{9,13},{14,13},{14,17},{18,20},{18,48},{16,51},{8,51},{6,48},{6,22},{9,18}}},
 {id='grenadine',mouth=9,label={6,25,10,20},pts={{10,9},{14,9},{14,16},{18,20},{18,49},{15,51},{8,51},{5,48},{5,22},{9,17},{10,14}}},
 {id='lemon_juice',mouth=12,label={4,26,16,17},pts={{9,12},{15,12},{15,16},{19,20},{19,46},{17,48},{7,48},{5,45},{5,20},{9,16}}},
 {id='honey',mouth=22,label={6,28,14,10},pts={{7,22},{18,22},{19,25},{19,36},{17,38},{6,38},{5,36},{5,25}}}
}
local function inside(x,y,pts)
 local yes=false;local j=#pts
 for i=1,#pts do local a,b=pts[i],pts[j];if ((a[2]>y)~=(b[2]>y)) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1] then yes=not yes end;j=i end
 return yes
end
for _,d in ipairs(defs) do
 local id='dl_bottle_'..d.id;local raw=Image{fromFile=srcroot..'/'..id..'.png'}
 local fg=Image(raw);local mask=Image(24,56,ColorMode.RGB);local visible=Image(24,56,ColorMode.RGB);local cap=Image(24,56,ColorMode.RGB)
 for y=0,55 do for x=0,23 do
  if y<d.mouth then cap:drawPixel(x,y,raw:getPixel(x,y)) end
  local enclosed=x>0 and x<23 and y>0 and y<55
  if enclosed then for _,q in ipairs({{0,0},{-1,0},{1,0},{0,-1},{0,1}}) do if pc.rgbaA(raw:getPixel(x+q[1],y+q[2]))==0 then enclosed=false;break end end end
  if enclosed and inside(x+.5,y+.5,d.pts) then
   mask:drawPixel(x,y,white)
   local a=d.label;local label=x>=a[1] and x<a[1]+a[3] and y>=a[2] and y<a[2]+a[4]
   if not label then fg:drawPixel(x,y,0);visible:drawPixel(x,y,white) end
  end
 end end
 local opened=Image(fg)
 for y=0,d.mouth-1 do for x=0,23 do opened:drawPixel(x,y,0) end end
 fg:saveAs(root..'/'..id..'_empty.png');mask:saveAs(root..'/'..id..'_mask.png');visible:saveAs(root..'/'..id..'_visible_mask.png');cap:saveAs(root..'/'..id..'_cap.png');opened:saveAs(root..'/'..id..'_open_empty.png')
 local s=Sprite(24,56,ColorMode.RGB);s.layers[1].name='Empty bottle below cap';s:newCel(s.layers[1],1,opened,Point(0,0))
 local l=s:newLayer();l.name='Original cap - hide for pouring';s:newCel(l,1,cap,Point(0,0))
 l=s:newLayer();l.name='Physical liquid interior mask - includes hidden label area';l.isVisible=false;s:newCel(l,1,mask,Point(0,0))
 l=s:newLayer();l.name='Visible interior only - NOT volume profile';l.isVisible=false;s:newCel(l,1,visible,Point(0,0))
 l=s:newLayer();l.name='Original unmodified reference';l.isVisible=false;s:newCel(l,1,raw,Point(0,0))
 s:saveAs(root..'/'..id..'.aseprite')
 local rt=Image(24,56,ColorMode.RGB);rt:drawSprite(s,1);rt:saveAs(root..'/qa/'..id..'_roundtrip.png');s:close()
 print(id..' separated')
end
