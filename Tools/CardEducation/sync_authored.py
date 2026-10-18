"""Synchronize reviewed education JSON to defaults and assets; mechanics untouched."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[2]
rows=json.loads((root/'Tools/CardEducation/authored-data.json').read_text(encoding='utf-8'))
assert len(rows)==35 and len({row['id'] for row in rows})==35
q=lambda value:json.dumps(value,ensure_ascii=False)
lines=['using System;','namespace CCT','{','    // Authored defaults only. Resource loading never rewrites serialized cards.','    public static class CardEducationData','    {','        public static void Author(CardData card)','        {','            switch (card.id)','            {']
for row in rows:
    fields={'realWorldFact':row['fact'],'gameplayConnection':row['connection'],'sourceName':row['sourceName'],'sourceUrl':row['sourceUrl'],'additionalSources':row.get('additionalSources',''),'educationArtId':row['art']}
    lines+=['                case '+q(row['id'])+':']
    lines+=['                    card.'+key+' = '+q(value)+';' for key,value in fields.items()]
    lines+=['                    card.flavorText = card.realWorldFact;','                    break;']
    path=root/'Assets/_Project/Resources/Cards'/('Card_'+row['id']+'.asset')
    text=path.read_text(encoding='utf-8-sig')
    fields.update(displayName=row['name'],flavorText=row['fact'])
    for key,value in fields.items():
        line='  '+key+': '+q(value)+'\n'
        if re.search(r'^  '+key+':',text,re.M):text=re.sub(r'^  '+key+r':.*?(?=^  \w|\Z)',lambda match:line,text,flags=re.M|re.S)
        else:text+=line
    path.write_text(text,encoding='utf-8')
lines+=['                default: throw new ArgumentException("Unknown educational card: " + card.id);','            }','        }',
'        public const string SymbolNotice = "효과 수치는 게임의 상징적 표현이며 실제 섭취의 건강 효능이나 술의 도수·우열을 뜻하지 않습니다.";',
'        public static string MaxEffect(CardData c) { var form = AttackFinalForms.ForCard(c.id); return form.HasValue ? "\\n" + AttackFinalForms.Description(form.Value) : ""; }',
'        public static string Detail(CardData c) => "게임 효과\\n" + c.description + "\\n최대 중첩 " + c.maxStacks + MaxEffect(c) + "\\n\\n실제 지식\\n" + c.realWorldFact + "\\n\\n게임 해석\\n" + c.gameplayConnection + "\\n\\n" + SymbolNotice + "\\n\\n출처: " + c.sourceName + "\\n" + c.sourceUrl + (string.IsNullOrEmpty(c.additionalSources) ? "" : "\\n\\n" + c.additionalSources);','    }','}']
(root/'Assets/_Project/Scripts/Data/CardEducationData.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Synchronized 35 authored cards; mechanics untouched.')
