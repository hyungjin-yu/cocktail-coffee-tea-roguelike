local root=app.params.root
for _,d in ipairs({{'ingredient_card',256,112,16,16},{'receipt',256,384,12,20},{'button',240,56,24,10}}) do
 local src=Image{fromFile=root..'/assets/'..d[1]..'/'..d[1]..'.png'}
 local im=Image(d[2],d[3],ColorMode.RGB)
 for y=0,d[3]-1 do for x=0,d[2]-1 do
 local u=x<d[4] and x or x>=d[2]-d[4] and src.width-(d[2]-x) or d[4]+math.floor((x-d[4])*(src.width-2*d[4])/(d[2]-2*d[4]))
 local v=y<d[5] and y or y>=d[3]-d[5] and src.height-(d[3]-y) or d[5]+math.floor((y-d[5])*(src.height-2*d[5])/(d[3]-2*d[5]))
 im:drawPixel(x,y,src:getPixel(u,v)) end end
 im:saveAs(root..'/qa/9slice-'..d[1]..'.png')
end
