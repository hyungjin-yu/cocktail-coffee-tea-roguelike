local root=app.params.root
for _,id in ipairs({'dl_teapot_open_body','dl_teapot_lid'}) do
 local s=app.open(root..'/'..id..'.aseprite')
 for _,l in ipairs(s.layers) do if l.name=='Existing physical interior mask - hidden, unchanged' then
  local cel=l:cel(1);local im=Image(64,56,ColorMode.RGB);im:drawImage(cel.image,cel.position);im:saveAs(root..'/qa/'..id..'_mask_roundtrip.png')
 end end
 s:close()
end
