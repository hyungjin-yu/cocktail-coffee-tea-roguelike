local root=app.params.root;local pc=app.pixelColor
local function C(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local function rect(im,x,y,w,h,c) for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,c) end end end
local function load(id) local s=assert(app.open(root..'/'..id..'/'..id..'.aseprite'));local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,1);s:close();return im end
local function save(id,borders,images,states)
 local dir=root..'/'..id;app.fs.makeAllDirectories(dir..'/frames');app.fs.makeAllDirectories(dir..'/reference')
 local s=Sprite(images[1].width,images[1].height,ColorMode.RGB);s.layers[1].name='Cafe pixel skin';local clips={};local files={};local hashes={}
 for n,im in ipairs(images) do
  if n>1 then s:newFrame() end
  s.cels[n].image=im;s.frames[n].duration=.1
  local f='frames/'..id..'_'..states[n]..'_right_00.png';im:saveAs(dir..'/'..f);files[#files+1]=f;clips[states[n]]={fps=0,loop=false,files={f}}
 end
 for n,state in ipairs(states) do local tag=s:newTag(n,n);tag.name=state end
 local sl=s:newSlice(Rectangle(0,0,s.width,s.height));sl.name='NineSlice';sl.center=Rectangle(borders[1],borders[4],s.width-borders[1]-borders[3],s.height-borders[2]-borders[4])
 s:saveAs(dir..'/'..id..'.aseprite');s:close()
 local m={assetId=id,specVersion='1.0',kind='ui',scope='ui_polish_2026_10_08',frameSizePx={images[1].width,images[1].height},pixelsPerUnit=128,pivotNormalized={.5,.5},spriteBorderPx=borders,clips=clips,files=files,sourceReferences={root..'/build_canonical.lua',root..'/pixellab-raw.aseprite'},status='needs_fix',acceptance={sourceReady=false,unityVerified=false},checks={structural='pending',nativeRoundTrip='pending',visual='pending',unity='not_run'},provenance={tool='Aseprite 1.3.18.6 Lua',concept='PixelLab actual generation1 edit0; source pixels Aseprite authored'},deviations={}}
 local f=assert(io.open(dir..'/manifest.json','w'));f:write(json.encode(m):sub(1,-2)..',"anchorsByFrame":{}}');f:close()
end
save('ui_cafe_panel',{12,12,12,12},{load('ui_panel')},{'effect'})
local buttons={};for _,state in ipairs({'normal','hover','pressed','disabled'}) do buttons[#buttons+1]=load('ui_button_'..state) end
save('ui_cafe_button',{10,10,10,10},buttons,{'normal','hover','pressed','disabled'})
save('ui_cafe_card',{12,12,12,12},{load('ui_card_normal')},{'effect'})
local normal=load('ui_tab_neutral_normal');local hover=load('ui_tab_neutral_selected');local pressed=normal:clone();rect(pressed,8,24,64,2,C('EAD4A5'));rect(pressed,8,6,64,2,C('201D29'))
local disabled=normal:clone();rect(disabled,8,8,64,16,C('3B3035'));for _,x in ipairs({2,74}) do rect(disabled,x,8,4,2,C('79624E'));rect(disabled,x,12,4,2,C('302028'));rect(disabled,x,16,4,2,C('79624E'));rect(disabled,x,20,4,2,C('302028')) end
save('ui_cafe_tab',{8,8,8,8},{normal,hover,pressed,disabled},{'normal','hover','pressed','disabled'})
save('ui_cafe_focus',{12,12,12,12},{load('ui_selection')},{'effect'})
local fill=Image(32,8,ColorMode.RGB);rect(fill,0,0,32,8,C('F4F1E7'));rect(fill,0,0,32,2,C('FFFFFF'));rect(fill,0,6,32,2,C('D8D2C7'))
save('ui_cafe_bar_fill',{3,2,3,2},{fill},{'effect'})
if app.params.extra=='confirmed' then
 save('ui_cafe_bar_frame',{6,6,6,6},{load('ui_bar_frame')},{'effect'})
 save('ui_cafe_header',{8,8,8,8},{load('ui_card_header')},{'effect'})
end
print('Canonical source export complete')
