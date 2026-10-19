import pathlib,json,html
from PIL import Image,ImageDraw
R=pathlib.Path(__file__).resolve().parent
m=json.loads((R/'manifest.json').read_text(encoding='utf8'))
for bg,name in [((0,0,0),'black'),((255,255,255),'white'),((105,83,70),'cafe')]:
 out=Image.new('RGB',(800,((len(m['assets'])+3)//4)*190),bg);d=ImageDraw.Draw(out)
 for i,a in enumerate(m['assets']):
  p=R/a['frames'][-1]['path'];im=Image.open(p).convert('RGBA');im.thumbnail((184,154),Image.Resampling.NEAREST)
  x=(i%4)*200+8;y=(i//4)*190+24;out.paste(im,(x,y),im);d.text((x,y-18),a['assetId'],fill=(100,100,100) if name=='white' else 'white')
 out.save(R/f'qa/alpha-{name}.png')
clips=[a for a in m['assets'] if len(a['frames'])>1]
cards=''.join(f'<figure><img src="{a["frames"][0]["path"]}"><figcaption>{html.escape(a["assetId"])}</figcaption></figure>' for a in m['assets'] if len(a['frames'])==1)
s='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>Progression UI art review</title><style>
body{margin:24px;background:#302d35;color:#f4f1e7;font:18px/1.5 "Malgun Gothic",sans-serif}button,select{font:inherit;min-height:44px;padding:6px 16px}img,canvas{image-rendering:pixelated}.grid{display:grid;grid-template-columns:repeat(4,200px);gap:16px}figure{margin:0;padding:8px;background:#201d29}figure img{width:auto;height:auto;max-width:184px;max-height:154px}figcaption{font-size:14px;overflow-wrap:anywhere}.stage{position:relative;width:1280px;height:720px;background:#695346;overflow:hidden}.laptop{position:absolute;left:160px;top:40px;width:960px;height:640px}.ui{position:absolute;left:320px;top:130px;width:640px;height:360px;box-sizing:border-box;overflow:auto;color:#f4f1e7;font-size:18px}.tabs{display:flex;gap:4px;position:sticky;top:0;background:#315157}.tabs button{font-size:18px;min-height:44px;flex:1}.two{display:grid;grid-template-columns:1fr 1fr;gap:16px;padding:12px}.sample{padding:12px;border:2px solid #a7b1c2}.sample p{margin:6px 0}canvas{background:repeating-conic-gradient(#4a4050 0% 25%,#302d35 0% 50%) 50%/16px 16px;display:block}.note{max-width:900px}
</style><h1>종이·노트북 아트 검토</h1><p class="note">ready 아트 미리보기입니다. 아래 글자와 버튼은 HTML 실제 UI 예시이며 PNG에는 포함되지 않습니다. Unity integrated / verified는 아직 아닙니다.</p>
<h2>1280×720 배치 예시 · 본문 18px · 버튼 44px 이상</h2><div class="stage"><img class="laptop" src="assets/laptop_open/laptop_open.png"><div class="ui"><div class="tabs"><button>기존 강화</button><button>숙련 트리</button><button>직원 채용</button></div><div class="two"><section class="sample"><strong>칵테일 연구</strong><p>향미 분석가</p><p>스터링 전문</p><p>필요 연구 6 RP</p><button>상세 보기</button></section><section class="sample"><strong>차 연구</strong><p>향 평가사</p><p>맑은 우림</p><p>필요 연구 6 RP</p><button>상세 보기</button></section><section class="sample"><strong>배합 기술자</strong><p>조건과 비용은 실제 UI</p><button>상세 보기</button></section><section class="sample"><strong>블렌딩 연구원</strong><p>내용은 스크롤로 확인</p><button>상세 보기</button></section></div></div></div>
<h2>모션 · 1회 재생 후 마지막 프레임 유지</h2><select id="clip"></select><button id="play">다시 재생</button><button id="skip">끝 상태</button><span id="time"></span><canvas id="motion"></canvas><h2>정적 프레임과 상태</h2><div class="grid">CARDS</div><script>
const clips=CLIPS;const select=document.querySelector('#clip'),canvas=document.querySelector('#motion'),ctx=canvas.getContext('2d');let images=[],run=0;
for(const c of clips){let o=document.createElement('option');o.textContent=c.assetId;select.append(o)}
async function load(){run++;const c=clips[select.selectedIndex];images=await Promise.all(c.framePaths.map(p=>new Promise(resolve=>{let im=new Image();im.onload=()=>resolve(im);im.src=p})));canvas.width=c.nativeSize[0];canvas.height=c.nativeSize[1];canvas.style.width=canvas.width*2+'px';canvas.style.height=canvas.height*2+'px';draw(0)}
function draw(i){ctx.clearRect(0,0,canvas.width,canvas.height);ctx.imageSmoothingEnabled=false;ctx.drawImage(images[i],0,0);document.querySelector('#time').textContent=` ${i+1}/${images.length} · ${(i+1)*50}ms`}
async function play(){const token=++run;for(let i=0;i<images.length;i++){if(token!==run)return;draw(i);await new Promise(r=>setTimeout(r,50))}}
select.onchange=load;document.querySelector('#play').onclick=play;document.querySelector('#skip').onclick=()=>{run++;draw(images.length-1)};load();
</script></html>'''.replace('CARDS',cards).replace('CLIPS',json.dumps(clips,ensure_ascii=False))
(R/'preview.html').write_text(s,encoding='utf8')
print('offline preview and 3 alpha contact backgrounds saved')
