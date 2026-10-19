local root=app.params.root
for _,k in ipairs({'gin','rum','blue_curacao','grenadine','lemon_juice','honey'}) do
 local id='dl_bottle_'..k;local s=app.open(root..'/'..id..'.aseprite')
 for _,l in ipairs(s.layers) do
  local suffix=nil
  if l.name=='Physical liquid interior mask - includes hidden label area' then suffix='mask'
  elseif l.name=='Visible interior only - NOT volume profile' then suffix='visible_mask'
  elseif l.name=='Original cap - hide for pouring' then suffix='cap'
  elseif l.name=='Empty bottle below cap' then suffix='open_empty' end
  if suffix then local cel=l:cel(1);local im=Image(24,56,ColorMode.RGB);im:drawImage(cel.image,cel.position);im:saveAs(root..'/qa/'..id..'_'..suffix..'_roundtrip.png') end
 end
 s:close()
end
print('24 native hidden/visible layer exports saved')
