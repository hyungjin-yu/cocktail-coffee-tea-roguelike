local root=app.params["out"]
local C={
 ink=Color{r=48,g=32,b=40}, red=Color{r=166,g=57,b=44}, redlight=Color{r=202,g=85,b=60},
 redshade=Color{r=110,g=41,b=38}, steel=Color{r=167,g=177,b=194}, shade=Color{r=83,g=84,b=111},
 cream=Color{r=244,g=241,b=231}, warm=Color{r=234,g=212,b=165}, coffee=Color{r=113,g=71,b=50},
 crema=Color{r=220,g=174,b=114}}
local s,im,cel
local function layer(n)
 if cel then cel.image=im end
 local l=s:newLayer(); l.name=n; im=Image(s.width,s.height,ColorMode.RGB)
 cel=s:newCel(l,1,im,Point(0,0))
end
local function rect(x,y,w,h,c)
 for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,C[c]) end end
end
local function ellipse(x,y,w,h,c,erase)
 for yy=y,y+h-1 do for xx=x,x+w-1 do
 if ((xx+0.5-x-w/2)/(w/2))^2+((yy+0.5-y-h/2)/(h/2))^2<=1 then im:drawPixel(xx,yy,erase and 0 or C[c]) end
 end end
end
local function begin(w,h) cel=nil; s=Sprite(w,h,ColorMode.RGB); s.layers[1].name="Reserved transparent" end
local function save(n)
 if cel then cel.image=im end
 s:saveAs(root.."/"..n..".aseprite")
 s:saveCopyAs(root.."/"..n..".png")
 s:close()
end
begin(128,80)
layer("Mounted warm red machine housing")
rect(5,2,118,30,"ink");rect(7,4,114,25,"redshade")
rect(8,4,112,17,"red");rect(10,4,108,3,"redlight")
rect(7,26,114,7,"ink");rect(9,27,110,3,"cream")
rect(12,32,104,8,"ink");rect(14,33,100,4,"shade");rect(16,33,95,2,"steel")
-- left mounting bolts and a restrained panel seam
rect(14,14,4,4,"ink");rect(15,14,2,2,"warm")
rect(109,14,4,4,"ink");rect(110,14,2,2,"warm")
layer("Steel group head fixed to housing")
rect(39,35,45,10,"ink");rect(42,36,39,7,"shade");rect(43,36,36,3,"steel")
ellipse(33,38,56,24,"ink");ellipse(35,39,52,19,"shade")
ellipse(36,39,50,13,"steel");ellipse(40,40,40,7,"cream")
rect(36,47,50,8,"shade");rect(37,47,47,3,"steel")
ellipse(36,48,50,13,"ink");ellipse(39,48,44,10,"steel")
ellipse(42,51,38,6,"shade")
layer("Locked portafilter handle")
rect(79,48,13,8,"ink");rect(80,49,12,4,"steel")
rect(90,47,29,11,"ink");rect(93,49,23,6,"redshade")
rect(94,49,21,2,"redlight");rect(116,49,4,6,"ink")
layer("Basket and twin outlet")
ellipse(43,52,36,14,"ink");ellipse(45,52,32,11,"shade")
rect(48,54,25,5,"steel");rect(48,54,21,2,"cream")
rect(55,61,12,8,"ink");rect(57,61,8,6,"steel")
rect(49,66,24,5,"ink");rect(50,66,22,3,"shade")
rect(49,68,6,7,"ink");rect(67,68,6,7,"ink")
rect(50,68,3,5,"steel");rect(68,68,3,5,"steel")
rect(50,74,4,2,"ink");rect(68,74,4,2,"ink")
save("dl_coffee_extraction_grouphead")
begin(64,48)
layer("Cream ceramic rim with transparent opening")
ellipse(2,5,60,38,"ink");ellipse(4,6,56,34,"warm")
ellipse(5,6,54,28,"cream");ellipse(8,9,48,25,"warm")
ellipse(9,11,46,22,"ink");ellipse(10,12,44,20,"coffee")
ellipse(11,13,42,18,"ink",true)
-- near rim shading; center stays fully open
save("dl_coffee_extraction_cup_rim")
begin(48,24)
layer("Neutral grayscale liquid mask for runtime tint")
-- grayscale preserves tint control, with a mild directional surface value
C.neutral=Color{r=220,g=220,b=220};C.light=Color{r=255,g=255,b=255}
ellipse(1,1,46,22,"neutral");ellipse(2,1,44,19,"light")
save("dl_coffee_extraction_liquid")
