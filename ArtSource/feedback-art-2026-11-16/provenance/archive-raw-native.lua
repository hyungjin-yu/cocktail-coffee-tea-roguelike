local root="C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/feedback-art-2026-11-16/raw/"
local count=0
for _,f in ipairs(app.fs.listFiles(root)) do
 if f:match("%.png$") then
  local path=root..f:gsub("%.png$",".aseprite")
  if not app.fs.isFile(path) then local s=app.open(root..f) s.layers[1].name="Original PixelLab output" s:saveAs(path) s:close() end
  count=count+1
 end
end
print("RAW_NATIVE_ARCHIVE_COUNT "..count)
