"""Standalone review UI embeds actual delivered PNGs; no art changes."""
import base64,json,pathlib
root=pathlib.Path(__file__).resolve().parent
manifest=json.loads((root/'manifest.json').read_text())
data={}
for a in manifest['assets']:
 if a['status']=='planned':continue
 item=dict(id=a['assetId'],size=a['size'],fps=a.get('fps',8),images=['data:image/png;base64,'+base64.b64encode(pathlib.Path(f['png']).read_bytes()).decode() for f in a['frames']])
 if a.get('mask'):item['mask']='data:image/png;base64,'+base64.b64encode(pathlib.Path(a['mask']['png']).read_bytes()).decode();item['bounds']=a['mask']['alphaBounds']
 data[a['assetId']]=item
html='''<!doctype html><meta charset="utf-8"><title>BeverageCraft actual sprite review</title>
<style>body{background:#201d29;color:#f4f1e7;font:16px system-ui;margin:24px}button,select,input{margin:8px}canvas{image-rendering:pixelated;background:repeating-conic-gradient(#3b3546 0% 25%,#302a3a 0% 50%) 0/16px 16px}#cards{display:flex;flex-wrap:wrap;gap:16px}figure{margin:0;width:200px}figcaption{font:12px monospace;overflow-wrap:anywhere}.row{display:flex;gap:24px;align-items:center}#fill{width:256px;height:320px;object-fit:contain}p{max-width:920px}</style>
<h1>BeverageCraft v1.1 — actual PNG review</h1><p>Art review only. Unity integration and game colour calculations belong to Claude. All sources and ordered animation frames are embedded below.</p>
<div class=row><canvas id=fill width=128 height=160></canvas><div><select id=vessels></select><br><label>Continuous amount <input id=amount type=range min=0 max=1000 value=500></label><output id=quantity></output><br><select id=color><option value="#dfe8eb">Gin / colourless</option><option value="#d5a45f">Rum</option><option value="#248edb">Blue curacao</option><option value="#ba284b">Grenadine</option><option value="#bd7847">Black tea</option><option value="#b0b65c">Green tea</option><option value="#ead889">Lemon juice</option><option value="#d6a23c">Honey</option></select><p>White mask is clipped from bottom to top; glass is a separate foreground PNG. Continuous values are not individual animation frames. These review colours are art swatches, not the game's optical simulation.</p></div></div><h2>Delivered sprites and animation snippets</h2><div id=cards></div><script>
const data=DATA; const load=s=>new Promise(r=>{const i=new Image;i.onload=()=>r(i);i.src=s});
(async()=>{for(const a of Object.values(data)){a.loaded=await Promise.all(a.images.map(load));if(a.mask)a.m=await load(a.mask)}
const vessels=document.querySelector('#vessels'); for(const a of Object.values(data).filter(x=>x.mask)){let o=new Option(a.id,a.id);vessels.add(o)}
const anim=[];for(const a of Object.values(data)){const fig=document.createElement('figure');const c=document.createElement('canvas');c.width=a.size[0];c.height=a.size[1];c.style.width='160px';c.style.height=Math.round(160*a.size[1]/a.size[0])+'px';if(parseInt(c.style.height)>240){c.style.height='240px';c.style.width=Math.round(240*a.size[0]/a.size[1])+'px'}fig.append(c);const t=document.createElement('figcaption');t.textContent=a.id+' '+a.size.join('×')+' / '+a.images.length+' frame(s)';fig.append(t);document.querySelector('#cards').append(fig);anim.push([c.getContext('2d'),a])}
function render(t){for(const [ctx,a] of anim){ctx.clearRect(0,0,...a.size);ctx.drawImage(a.loaded[Math.floor(t/1000*a.fps)%a.loaded.length],0,0)}
const a=data[vessels.value];if(a){const canvas=document.querySelector('#fill'),ctx=canvas.getContext('2d');canvas.width=a.size[0];canvas.height=a.size[1];ctx.imageSmoothingEnabled=false;const off=document.createElement('canvas');off.width=a.size[0];off.height=a.size[1];const o=off.getContext('2d');o.drawImage(a.m,0,0);o.globalCompositeOperation='source-in';o.fillStyle=document.querySelector('#color').value;o.fillRect(0,0,...a.size);const q=+document.querySelector('#amount').value/1000;const y=a.bounds[3]-(a.bounds[3]-a.bounds[1])*q;o.globalCompositeOperation='destination-in';o.fillRect(0,y,a.size[0],a.size[1]-y);o.clearRect(0,0,a.size[0],y);ctx.drawImage(off,0,0);ctx.drawImage(a.loaded[0],0,0);document.querySelector('#quantity').textContent=(q*100).toFixed(1)+'%'}requestAnimationFrame(render)}requestAnimationFrame(render)
})()</script>'''
(root/'preview.html').write_text(html.replace('DATA',json.dumps(data)),encoding='utf8')
print('Embedded',len(data),'actual sprite groups into preview.html')
