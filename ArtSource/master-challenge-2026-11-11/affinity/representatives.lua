local root='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/'
local out=root..'ArtSource/master-challenge-2026-11-11/affinity/'
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

-- Shape representatives only, effects point upward-right from hit centre.
local function bubble(im,x,y,r)
 for yy=-r,r do for xx=-r,r do local ds=xx*xx+yy*yy;if ds<=r*r and ds>=(r-2)*(r-2) then im:drawPixel(x+xx,y+yy,col(yy<0 and xx<2 and 'fff0db' or 'a8d4ce')) end end end
 rect(im,x-r+2,y-r+3,2,3,col('fff9e5'))
end
local function bean(im,x,y,k)
 local pts={{-9,-8},{-4,-13},{3,-14},{9,-9},{11,-2},{8,7},{2,13},{-5,13},{-11,8},{-13,1}}
 local function shape(t,c) local q={};for _,v in ipairs(t) do q[#q+1]={math.floor(x+v[1]*k),math.floor(y+v[2]*k)} end;poly(im,q,col(c)) end
 shape(pts,'42302c');shape({{-8,-6},{-3,-11},{2,-11},{7,-7},{8,-1},{5,6},{0,10},{-5,10},{-9,6},{-10,0}},'98664b')
 shape({{-7,-5},{-2,-9},{2,-9},{4,-7},{0,-5},{-3,-1},{-4,4},{-7,6},{-8,1}},'c59165')
 shape({{2,-10},{4,-9},{2,-3},{-2,2},{-2,7},{-5,10},{-4,4},{-5,0},{0,-5}},'4e332d')
 shape({{4,-8},{5,-6},{2,-1},{-1,3},{-1,7},{-3,9},{-2,2},{1,-3}},'e5b77c')
end
local function leaf(im,x,y,k,flip)
 local function sh(t,c) local q={};for _,v in ipairs(t) do q[#q+1]={math.floor(x+v[1]*k*(flip and -1 or 1)),math.floor(y+v[2]*k)} end;poly(im,q,col(c)) end
 sh({{-17,10},{-10,-4},{-1,-10},{17,-13},{10,3},{1,12},{-8,14}},'354438')
 sh({{-13,9},{-8,-2},{0,-7},{13,-10},{6,2},{0,9},{-7,11}},'6e9564')
 sh({{-13,8},{-7,-1},{1,-6},{11,-8},{2,0},{-4,5}},'b1cb83')
 sh({{-13,9},{-2,2},{12,-9},{9,-3},{0,5},{-9,11}},'456540')
end
for _,n in ipairs({'cocktail_foam','coffee_bean_steam','tea_leaf_wind'}) do
 local sp=Sprite(128,128,ColorMode.RGB);sp.layers[1].name=n..' representative peak';local im=sp.cels[1].image
 if n=='cocktail_foam' then
  bubble(im,54,72,10);bubble(im,73,57,8);bubble(im,88,39,6);bubble(im,88,71,4)
  poly(im,{{39,85},{44,82},{49,84},{52,88},{48,91},{41,89}},col('dbe9d1'))
  rect(im,94,28,2,4,col('ffe0ba'));rect(im,99,34,4,2,col('ffe0ba'))
 elseif n=='coffee_bean_steam' then
  bean(im,61,72,1);bean(im,83,54,.7)
  poly(im,{{48,60},{42,53},{43,46},{50,40},{54,34},{52,27},{56,31},{57,37},{51,44},{47,48},{47,53},{53,57}},col('b6a58b'))
  poly(im,{{48,58},{45,52},{46,47},{53,40},{55,34},{55,38},{49,45},{47,50},{49,55},{53,58}},col('f4e4bf'))
  rect(im,92,34,3,3,col('c59165'))
 else
  leaf(im,60,68,.85,false);leaf(im,89,47,.55,true)
  poly(im,{{34,83},{44,89},{57,91},{71,87},{84,78},{94,65},{99,51},{98,63},{92,77},{80,89},{65,96},{52,97},{40,92}},col('718f78'))
  poly(im,{{36,83},{45,87},{58,89},{71,85},{84,77},{93,65},{92,71},{82,84},{68,92},{54,94},{44,90}},col('d0dcb1'))
 end
 save(sp,n..'_representative')
end
