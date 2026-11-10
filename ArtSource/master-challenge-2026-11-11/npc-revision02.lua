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
  -- Rolled cloth sits across both flexed wrists; fingertips occlude its front.
  rect(prop,111,152,39,5,O);rect(prop,114,153,32,2,col('87624e'));rect(prop,131,152,7,5,G);rect(prop,133,153,3,2,O)
  rolled(prop,96,157,80,18)
  -- Left palm curves under the roll, thumb over top rim, four curled fingers in front.
  poly(prop,{{94,150},{102,149},{108,155},{108,161},{114,161},{116,166},{112,174},{104,178},{97,173},{94,165}},col('53362d'))
  poly(prop,{{96,151},{101,151},{105,156},{104,164},{111,164},{112,167},{109,172},{104,175},{99,171},{96,164}},col('e4a06b'))
  poly(prop,{{97,152},{100,152},{103,156},{102,164},{99,166},{97,163}},col('ffd09b'))
  poly(prop,{{100,167},{110,166},{110,170},{105,173},{101,172}},col('f6bb82'))
  rect(prop,104,166,2,5,col('b57450'));rect(prop,108,166,2,4,col('b57450'));rect(prop,99,173,6,2,col('b57450'))
  -- Right grip sits around opposite cloth end; darker palm below, thumb on top.
  poly(prop,{{160,148},{166,148},{170,153},{169,160},{173,163},{172,169},{166,172},{160,169},{158,163}},col('53362d'))
  poly(prop,{{162,150},{165,150},{168,154},{166,160},{170,163},{169,167},{166,169},{162,167},{161,162}},col('e9a66d'))
  poly(prop,{{162,150},{165,151},{166,155},{164,160},{162,158}},col('ffd09b'))
  rect(prop,164,163,6,2,col('ffca8e'));rect(prop,164,167,5,2,col('b57450'));rect(prop,166,164,2,3,col('b57450'))
  layer(sp,'cloth cradled by flexed wrists with front fingers',prop)
 else
  -- Tailored apron follows torso and gathers at tied waist, separate from vest.
  poly(prop,{{110,150},{144,150},{148,160},{151,181},{146,189},{137,194},{119,191},{105,185},{104,178},{109,163}},O)
  poly(prop,{{112,154},{141,154},{145,164},{148,180},{144,186},{137,190},{120,187},{109,182},{108,178}},col('496b51'))
  poly(prop,{{112,156},{120,157},{116,173},{116,182},{121,187},{110,182},{108,177}},col('72916a'))
  poly(prop,{{137,155},{141,155},{145,164},{148,180},{143,186},{136,190},{134,182}},col('324b3e'))
  poly(prop,{{125,159},{130,159},{127,178},{129,188},{124,187},{121,179}},col('587b59'))
  poly(prop,{{133,163},{136,161},{134,181},{137,189},{133,189},{131,180}},col('253c34'))
  -- Curved waist seam and small bow at left, broad planes rather than noise.
  poly(prop,{{109,149},{118,151},{132,153},{143,150},{145,156},{132,159},{118,157},{108,155}},col('243b31'))
  poly(prop,{{111,150},{120,153},{132,154},{142,152},{142,154},{132,156},{119,155},{111,153}},col('96a781'))
  poly(prop,{{109,151},{101,148},{98,152},{102,157},{108,155},{114,159},{119,157},{115,152}},col('334b3b'))
  poly(prop,{{104,151},{108,152},{105,154},{102,153}},col('93a27a'))
  poly(prop,{{110,155},{113,157},{107,170},{104,168}},col('6f8b62'))
  poly(prop,{{120,166},{137,166},{136,175},{122,176}},col('2c4838'));rect(prop,122,166,13,2,col('96a781'))
  -- Remove original hanging right forearm, preserving torso and shoulder root.
  for yy=121,181 do for xx=154,182 do im:drawPixel(xx-offset.x,yy-offset.y,0) end end
  -- Rope is BEHIND the gripping bare hand and terminates below thumb.
  rect(prop,193,67,4,45,col('66533f'));rect(prop,194,67,2,45,col('dfc58c'))
  -- Shoulder -> folded sleeve -> elbow; comparable width to original left sleeve.
  poly(prop,{{146,111},{155,110},{162,116},{169,125},{176,128},{179,135},{173,143},{164,143},{153,134},{147,126}},O)
  poly(prop,{{148,114},{154,113},{160,119},{166,128},{174,130},{176,135},{171,140},{165,139},{156,131},{150,124}},col('d6bea7'))
  poly(prop,{{149,114},{153,114},{159,120},{162,126},{157,127},{151,122}},col('fff0dc'))
  poly(prop,{{158,127},{165,130},{174,133},{173,136},{165,135},{158,132}},col('f8e7d0'))
  poly(prop,{{157,130},{164,135},{172,138},{169,141},{164,138},{155,133}},col('a78e7d'))
  -- Rolled cuff wraps diagonally around elbow, double fold line.
  poly(prop,{{166,125},{179,131},{176,139},{163,133}},col('6c5651'))
  poly(prop,{{168,127},{177,132},{174,136},{166,132}},col('fff0dc'))
  poly(prop,{{166,132},{174,136},{173,138},{164,134}},col('c8ad97'))
  -- Bent forearm: broad elbow narrows at wrist then widens into closed grip.
  poly(prop,{{172,126},{179,117},{183,105},{185,100},{183,94},{184,86},{189,82},{196,84},{201,90},{200,99},{194,104},{190,116},{184,132},{178,138},{172,135}},col('53362d'))
  poly(prop,{{174,127},{181,116},{185,104},{188,99},{186,94},{187,88},{190,85},{195,86},{198,91},{197,97},{191,102},{187,115},{181,130},{177,134},{174,133}},col('e9a56c'))
  poly(prop,{{175,127},{182,115},{186,103},{189,99},{189,95},{187,93},{189,88},{192,87},{193,91},{191,97},{190,103},{186,115},{180,129},{177,131}},col('ffcc90'))
  poly(prop,{{191,103},{195,101},{190,117},{183,132},{177,136},{175,133},{181,129},{187,115}},col('b87550'))
  -- Finger pads occlude rope; three separations and thumb on opposing side.
  rect(prop,193,87,5,3,col('ffd09b'));rect(prop,193,91,6,3,col('f4b780'));rect(prop,192,95,6,3,col('edaa71'))
  rect(prop,194,90,5,1,col('9e5d43'));rect(prop,194,94,5,1,col('9e5d43'));rect(prop,193,98,4,1,col('9e5d43'))
  poly(prop,{{186,92},{189,90},{193,94},{193,97},{190,99},{187,97}},col('b87550'))
  poly(prop,{{187,92},{189,92},{192,95},{191,97},{189,96}},col('ffce93'))
  layer(sp,'gathered tied apron and articulated rope gripping arm',prop)
 end
 save(sp,'npc_'..n..'_revision02')
end
