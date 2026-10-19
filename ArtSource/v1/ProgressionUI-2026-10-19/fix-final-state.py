import pathlib
R=pathlib.Path(__file__).resolve().parent
p=R/'finish.lua';s=p.read_text(encoding='utf8').replace(' local stableOpen=lidFrame(1,true)', ' local stableOpen=lidFrame(1,false);fill(stableOpen,56,28,268,160,color(16))')
p.write_text(s,encoding='utf8')
