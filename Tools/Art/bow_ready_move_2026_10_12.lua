-- Six anatomical contact/load/passing keyposes. No warp, interpolation or duplicate quota.
-- Retain canonical upper-body pixels and draw two articulated legs behind apron.
local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/bow-ready-2026-10-12'
local reference=Image{fromFile='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/v1/tea_sommelier/frames/tea_sommelier_ranged_right_01.png'}
local pc=app.pixelColor
local C={outline=pc.rgba(32,29,41,255),pants=pc.rgba(96,54,45,255),light=pc.rgba(119,65,50,255),far=pc.rgba(67,36,31,255),shoe=pc.rgba(102,54,40,255),shine=pc.rgba(143,80,53,255),sole=pc.rgba(50,27,22,255)}
local function block(im,x,y,c)
 x=math.floor(x/2+.5)*2;y=math.floor(y/2+.5)*2
 for dy=0,1 do for dx=0,1 do if x+dx>=0 and y+dy>=0 and x+dx<256 and y+dy<256 then im:drawPixel(x+dx,y+dy,c) end end end
end
local function poly(im,points,c)
 local ymin,ymax=256,0
 for _,p in ipairs(points) do ymin=math.min(ymin,p[2]);ymax=math.max(ymax,p[2]) end
 for y=math.floor(ymin/2)*2,math.ceil(ymax/2)*2,2 do
  local xs={}
  for i,a in ipairs(points) do local b=points[i%#points+1]
   if (a[2]<=y+1 and b[2]>y+1) or (b[2]<=y+1 and a[2]>y+1) then xs[#xs+1]=a[1]+(y+1-a[2])*(b[1]-a[1])/(b[2]-a[2]) end
  end
  table.sort(xs)
  for i=1,#xs-1,2 do for x=math.floor(xs[i]/2)*2,math.floor((xs[i+1]-1)/2)*2,2 do block(im,x,y,c) end end
 end
end
local function segment(im,a,b,wa,wb,c)
 local dx,dy=b[1]-a[1],b[2]-a[2];local len=math.sqrt(dx*dx+dy*dy);local nx,ny=-dy/len,dx/len
 poly(im,{{a[1]+nx*wa/2,a[2]+ny*wa/2},{b[1]+nx*wb/2,b[2]+ny*wb/2},{b[1]-nx*wb/2,b[2]-ny*wb/2},{a[1]-nx*wa/2,a[2]-ny*wa/2}},c)
end
local function joint(im,x,y,r,c)
 for yy=y-r,y+r,2 do for xx=x-r,x+r,2 do if (xx-x)^2+(yy-y)^2<=r*r then block(im,xx,yy,c) end end end
end
local function leg(im,hip,knee,ankle,far)
 local base=far and C.far or C.pants
 segment(im,hip,knee,24,20,C.outline);joint(im,knee[1],knee[2],10,C.outline);segment(im,knee,ankle,20,16,C.outline)
 segment(im,hip,knee,20,16,base);joint(im,knee[1],knee[2],7,base);segment(im,knee,ankle,16,12,base)
 if not far then
  segment(im,{hip[1]+2,hip[2]+2},{knee[1]+2,knee[2]-2},4,4,C.light)
  segment(im,{knee[1]+2,knee[2]+2},{ankle[1]+2,ankle[2]-4},4,2,C.light)
  poly(im,{{knee[1]-6,knee[2]-4},{knee[1]+6,knee[2]-2},{knee[1]+4,knee[2]},{knee[1]-6,knee[2]-2}},C.far)
 end
 local x,y=ankle[1],ankle[2]
 poly(im,{{x-10,y-12},{x+6,y-12},{x+8,y-2},{x+18,y},{x+20,y+6},{x-10,y+6}},C.outline)
 poly(im,{{x-8,y-10},{x+4,y-10},{x+6,y},{x+16,y+2},{x+16,y+4},{x-8,y+4}},far and C.far or C.shoe)
 poly(im,{{x-8,y+2},{x+16,y+2},{x+16,y+4},{x-8,y+4}},C.sole)
 if not far then poly(im,{{x-6,y-10},{x+2,y-10},{x+2,y-8},{x-6,y-8}},C.shine);poly(im,{{x+2,y-2},{x+8,y},{x+14,y+2},{x+4,y+2}},C.shine) end
end
local poses={
 {name='near_contact_far_back',bob=0,near={{136,170},{144,194},{154,218}},far={{118,170},{108,194},{104,218}},swing=-4,support='near'},
 {name='near_load_far_lift',bob=2,near={{136,172},{136,196},{138,218}},far={{118,172},{106,190},{104,200}},swing=-2,support='near'},
 {name='near_support_far_pass',bob=-2,near={{136,168},{128,192},{122,218}},far={{118,168},{142,186},{148,202}},swing=2,support='near'},
 {name='far_contact_near_back',bob=0,near={{136,170},{116,194},{104,218}},far={{118,170},{142,194},{154,218}},swing=4,support='far'},
 {name='far_load_near_lift',bob=2,near={{136,172},{116,188},{108,200}},far={{118,172},{134,196},{138,218}},swing=2,support='far'},
 {name='far_support_near_pass',bob=-2,near={{136,168},{146,186},{150,202}},far={{118,168},{120,192},{122,218}},swing=-2,support='far'},
}
local preview=Image(1536,256,ColorMode.RGB)
for i,p in ipairs(poses) do
 local im=Image(256,256,ColorMode.RGB)
 leg(im,p.far[1],p.far[2],p.far[3],true);leg(im,p.near[1],p.near[2],p.near[3],false)
 for y=0,179,2 do for x=0,255,2 do
  local c=reference:getPixel(x,y)
  local arm=y>=120 and y<176 and (x<110 or x>=158)
  if pc.rgbaA(c)>0 and not arm then block(im,x,y+p.bob,c) end
 end end
 for y=120,175,2 do for x=0,255,2 do
  local c=reference:getPixel(x,y)
  if pc.rgbaA(c)>0 and (x<110 or x>=158) then
   local dx=math.floor(p.swing*(y-120)/56/2+.5)*2;if x>=158 then dx=-dx end
   block(im,x+dx,y+p.bob,c)
  end
 end end
 im:saveAs(root..'/frames/tea_sommelier_move_right_'..string.format('%02d',i-1)..'.png');preview:drawImage(im,Point((i-1)*256,0))
end
preview:saveAs(root..'/authored-move-preview.png')
local f=assert(io.open(root..'/authored-move-plan.json','w'));f:write(json.encode(poses));f:close()
