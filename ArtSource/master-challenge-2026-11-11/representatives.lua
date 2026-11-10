local root='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/'
local out=root..'ArtSource/master-challenge-2026-11-11/'
local pc=app.pixelColor
local function col(h) return Color{r=tonumber(h:sub(1,2),16),g=tonumber(h:sub(3,4),16),b=tonumber(h:sub(5,6),16),a=255}.rgbaPixel end
local O=col('30232f');local C=col('f4e4bf');local L=col('fff6da');local M=col('c6a878');local S=col('927451');local G=col('d9b66d')
local function rect(im,x,y,w,h,c) for yy=y,y+h-1 do for xx=x,x+w-1 do if xx>=0 and yy>=0 and xx<im.width and yy<im.height then im:drawPixel(xx,yy,c) end end end end
local function poly(im,pts,c)
 local ymin=im.height;local ymax=0;for _,p in ipairs(pts) do ymin=math.min(ymin,p[2]);ymax=math.max(ymax,p[2]) end
 for y=math.max(0,ymin),math.min(im.height-1,ymax) do local xs={};for i,p in ipairs(pts) do local q=pts[i%#pts+1];if (p[2]<=y and q[2]>y) or (q[2]<=y and p[2]>y) then xs[#xs+1]=p[1]+(y-p[2])*(q[1]-p[1])/(q[2]-p[2]) end end; table.sort(xs);for i=1,#xs-1,2 do for x=math.max(0,math.ceil(xs[i])),math.min(im.width-1,math.floor(xs[i+1])) do im:drawPixel(x,y,c) end end end
end
local function layer(sp,name,im) local l=sp:newLayer();l.name=name;sp:newCel(l,1,im,Point(0,0));return l end
local function save(sp,name) sp:saveAs(out..name..'.aseprite');sp:saveCopyAs(out..name..'.png');sp:close() end
local function rolled(im,x,y,w,h)
 rect(im,x+3,y,w-6,h,O);rect(im,x,y+3,w,h-6,O);rect(im,x+4,y+3,w-8,h-6,M);rect(im,x+6,y+3,w-12,3,L);rect(im,x+6,y+7,w-12,h-12,C);rect(im,x+6,y+h-6,w-12,3,S)
 rect(im,x+12,y+2,5,h-4,S);rect(im,x+13,y+2,2,h-4,G);rect(im,x+w-18,y+2,5,h-4,S);rect(im,x+w-17,y+2,2,h-4,G)
end
-- Representative only: source standing bodies reused, identity palette adapted, added task props.
for _,n in ipairs({'porter','rigger'}) do
 local source=n=='porter' and 'tea_sommelier' or 'bartender'
 local sp=app.open(root..'Assets/_Project/Art/v1/'..source..'/frames/'..source..'_idle_right_00.png');while #sp.frames>1 do sp:deleteFrame(sp.frames[#sp.frames]) end;sp.layers[1].name='standing source - palette adapted'
 local im=sp.cels[1].image;local offset=sp.cels[1].position
 for it in im:pixels() do local v=it();local x=it.x+offset.x;local y=it.y+offset.y;local r,g,b,a=pc.rgbaR(v),pc.rgbaG(v),pc.rgbaB(v),pc.rgbaA(v)
 if a>0 then
  if n=='porter' then
   -- Hair, including ponytail: exclude skin by brown/skin ratio and x/y mask.
   if y<116 and ((y<70) or x<113) and r>24 and r<166 and g<r*.76 and b<r*.8 then
    local t=(r+g+b)/3;local h=t<36 and '514054' or t<57 and '9b7c7e' or t<78 and 'c6a69a' or 'efd3b3';it(col(h))
   elseif y>=108 and y<156 and x>=106 and x<154 and r>25 and r<160 and math.abs(r-g)<30 and g>b then
    local t=(r+g+b)/3;it(col(t<65 and '48324d' or t<90 and '694663' or t<112 and '91647e' or 'b98799'))
   end
  else
   if y>=109 and y<163 and x>=106 and x<155 and b>r*1.15 and b<130 and r>20 then
    local t=(r+g+b)/3;it(col(t<45 and '293d35' or t<61 and '3d5949' or t<80 and '58785d' or '83a17c'))
   end
  end
 end end
 local prop=Image(256,256,ColorMode.RGB)
 if n=='porter' then
  -- Cloth carried diagonally between the existing lowered hands; no generated hand substituted.
  rolled(prop,99,163,77,18)
  -- Belt loops and brass tool clasp distinguish porter from player.
  rect(prop,111,155,39,4,O);rect(prop,114,156,32,2,col('87624e'));rect(prop,131,155,7,5,G);rect(prop,133,156,3,2,O)
  layer(sp,'rolled cloth - carrying reference pose',prop)
 else
  -- Shaped apron: flared hem and fold planes, not a flat garment rectangle.
  poly(prop,{{111,137},{145,138},{152,184},{141,192},{108,188},{103,181}},O)
  poly(prop,{{114,140},{142,141},{148,182},{139,188},{109,184},{108,178}},col('496b51'))
  poly(prop,{{114,140},{121,141},{117,180},{109,184},{108,178}},col('72916a'))
  poly(prop,{{136,142},{142,141},{148,182},{139,188},{135,182}},col('324b3e'))
  poly(prop,{{123,151},{140,151},{139,163},{123,164}},col('253c34'));rect(prop,125,152,13,2,col('a0b08a'));rect(prop,126,155,10,5,col('55765a'))
  -- Left-hand mallet; raised right arm fixes banner pin. Representative pose only.
  -- Remove only the original hanging right arm; preserve head, shoulder and torso.
  for yy=123,181 do for xx=154,181 do im:drawPixel(xx-offset.x,yy-offset.y,0) end end
  poly(prop,{{146,112},{155,111},{166,122},{173,104},{171,96},{176,88},{181,90},{184,101},{180,110},{174,133},{165,141},{154,133}},O)
  poly(prop,{{148,115},{154,114},{163,124},{169,128},{165,137},{156,129}},col('f1ddc6'))
  poly(prop,{{150,115},{154,116},{163,125},{167,127},{164,130},{156,125}},col('fff0dc'))
  poly(prop,{{154,126},{164,132},{167,130},{166,137},{158,132}},col('bca18b'))
  poly(prop,{{166,127},{171,113},{174,103},{174,97},{177,91},{180,93},{181,101},{177,109},{172,131},{168,136},{165,134}},col('e9a56c'))
  poly(prop,{{169,125},{174,108},{175,98},{178,94},{179,100},{175,112},{172,128}},col('ffcb8c'))
  poly(prop,{{175,104},{179,102},{177,110},{172,132},{168,136},{167,132}},col('bb754e'))
  rect(prop,175,95,3,5,col('fff0bc'));rect(prop,177,101,4,2,col('995b43'))
  -- Small wooden mallet in lowered left hand, shaft above the palm.
  poly(prop,{{96,148},{100,148},{103,168},{100,172}},O);poly(prop,{{97,150},{99,150},{101,166},{100,165}},col('b18a59'))
  rect(prop,87,141,21,10,O);rect(prop,89,143,17,6,col('a17c57'));rect(prop,89,143,17,2,col('d2b180'))
  layer(sp,'green apron, left mallet, raised fixing right hand',prop)
 end
 save(sp,'npc_'..n..'_representative')
end
-- Banner 1280x192: 2px cluster grid for folds; lettering intentionally absent.
for _,mode in ipairs({'rolled','unfolding','settled'}) do
 local sp=Sprite(1280,192,ColorMode.RGB);sp.layers[1].name='cloth';local im=sp.cels[1].image
 if mode=='rolled' then rolled(im,380,70,520,44)
 else
  local bottom=mode=='unfolding' and 108 or 174
  poly(im,{{29,12},{1250,12},{1261,39},{1247,bottom-7},{1205,bottom},{1120,bottom-8},{1000,bottom-3},{840,bottom-9},{650,bottom-2},{460,bottom-8},{270,bottom-2},{100,bottom-8},{32,bottom-15},{19,39}},O)
  poly(im,{{35,20},{1244,20},{1251,40},{1239,bottom-15},{1205,bottom-8},{1120,bottom-16},{1000,bottom-11},{840,bottom-17},{650,bottom-10},{460,bottom-16},{270,bottom-10},{100,bottom-16},{40,bottom-23},{29,40}},C)
  rect(im,42,22,1193,5,L);rect(im,43,29,1190,3,M)
  -- Fold planes primarily at the ends; central text area remains quiet.
  for _,x in ipairs({62,102,1178,1218}) do poly(im,{{x,31},{x+7,31},{x+15,bottom-26},{x+6,bottom-28}},M);poly(im,{{x+7,31},{x+11,31},{x+19,bottom-26},{x+15,bottom-26}},L) end
  poly(im,{{45,bottom-30},{270,bottom-17},{460,bottom-23},{650,bottom-17},{840,bottom-24},{1000,bottom-18},{1120,bottom-23},{1235,bottom-22},{1235,bottom-17},{1120,bottom-18},{1000,bottom-13},{840,bottom-19},{650,bottom-12},{460,bottom-18},{270,bottom-12},{45,bottom-25}},M)
  -- Twin end loops and dark pin cores.
  for _,x in ipairs({36,1230}) do rect(im,x,7,14,30,O);rect(im,x+3,10,8,22,G);rect(im,x+5,14,4,11,S);rect(im,x+5,7,4,4,L) end
 end
 save(sp,'banner_'..mode..'_representative')
end
local function star(im,x,y,size)
 poly(im,{{x,y-size},{x+3,y-3},{x+size,y},{x+3,y+3},{x,y+size},{x-3,y+3},{x-size,y},{x-3,y-3}},S)
 poly(im,{{x,y-size+2},{x+2,y-2},{x+size-2,y},{x+2,y+2},{x,y+size-2},{x-2,y+2},{x-size+2,y},{x-2,y-2}},G)
 poly(im,{{x,y-size+4},{x+1,y-1},{x+size-4,y},{x+1,y+1},{x,y+size-4},{x-1,y+1},{x-size+4,y},{x-1,y-1}},L)
end
for _,grade in ipairs({'normal','excellent','master'}) do
 local sp=Sprite(128,128,ColorMode.RGB);sp.layers[1].name='quality shape - peak pose';local im=sp.cels[1].image
 if grade=='normal' then star(im,64,63,10)
 elseif grade=='excellent' then
  star(im,64,35,13);star(im,38,68,10);star(im,90,68,10)
  poly(im,{{33,78},{38,84},{48,88},{64,91},{80,88},{90,84},{95,78},{94,83},{85,90},{66,95},{48,92},{37,87}},M)
  poly(im,{{37,80},{43,85},{57,89},{73,89},{87,84},{92,80},{87,87},{74,92},{55,92},{42,87}},L)
 else
  star(im,64,50,22);star(im,36,72,10);star(im,94,67,10)
  -- Two tapered upward curls, clear central gap; no opaque filled aura.
  poly(im,{{42,103},{32,96},{26,85},{24,73},{27,60},{32,48},{33,40},{32,33},{37,39},{37,49},{32,64},{30,76},{33,87},{39,94},{48,99}},col('a2afb0'))
  poly(im,{{42,101},{34,95},{28,84},{27,73},{30,59},{34,47},{34,40},{35,48},{31,63},{30,75},{33,86},{39,93},{48,98}},col('e8ebd8'))
  poly(im,{{82,105},{94,99},{102,90},{106,77},{104,66},{99,55},{99,45},{102,40},{103,48},{104,55},{109,67},{111,78},{107,92},{97,103},{87,109}},col('a2afb0'))
  poly(im,{{84,103},{95,97},{103,87},{107,76},{104,64},{101,54},{101,47},{102,54},{107,66},{109,78},{105,91},{96,101},{87,107}},col('e8ebd8'))
  rect(im,44,24,3,6,G);rect(im,88,29,3,5,G)
 end
 save(sp,'quality_'..grade..'_representative')
end
local f=io.open(out..'aseprite-batch-receipt.txt','w');f:write('Representative Aseprite Lua creation completed. Eight native files and eight PNGs. No PixelLab request, no GUI injection. NPC rigger raised right hand banner_hand(178,98); representative only, no locomotion clip yet.\n');f:close()
