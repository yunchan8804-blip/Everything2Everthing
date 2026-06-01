# -*- coding: utf-8 -*-
"""
Everything2Everything SSOT 제너레이터.

_data/*.json (마스터플랜 + 분석 + 리서치 + 설계안 + 심사 + 메타) 을 읽어
- index.html  : 단일 파일 다크테마 대시보드 (외부 의존성 0, 폰트만 CDN fallback)
- PLAN.md     : 사람이 읽고 다음 세션이 참조하는 마크다운 SSOT
를 생성한다.

다음 세션 사용법:
  1) _data/*.json 을 갱신 (로드맵 단계 status 등)
  2) python build.py
  3) index.html / PLAN.md 가 재생성됨
"""
import json
import os
import html

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "_data")


def load(name):
    with open(os.path.join(DATA, name), encoding="utf-8") as f:
        return json.load(f)


master = load("master.json")
analyses = load("analyses.json")
researches = load("researches.json")
designs = load("designs.json")
ranking = load("ranking.json")
meta = load("meta.json")

# 로드맵 진행 상태(다음 세션이 갱신). 없으면 모두 'planned'.
status_path = os.path.join(DATA, "status.json")
if os.path.exists(status_path):
    with open(status_path, encoding="utf-8") as f:
        STATUS = json.load(f)
else:
    STATUS = {}


def e(s):
    return html.escape(str(s if s is not None else ""))


def nl2br(s):
    return e(s).replace("\\n", "<br>").replace("\n", "<br>")


# ------------------------------------------------------------------ CSS
CSS = r"""
:root{
  --bg:#080b11; --bg1:#0d121b; --card:#121a26; --card2:#16202e;
  --line:#22304333; --line2:#2a3a50; --tx:#e7eef6; --dim:#9fb2c8; --faint:#65788f;
  --cy:#22d3ee; --em:#34d399; --vi:#a78bfa; --am:#fbbf24; --rs:#fb7185; --bl:#60a5fa;
  --r:16px; --rs2:10px;
  --mono:"JetBrains Mono",ui-monospace,"SFMono-Regular",Consolas,"Cascadia Code",monospace;
  --sans:"Pretendard Variable",Pretendard,-apple-system,"Segoe UI","Malgun Gothic",sans-serif;
}
*{box-sizing:border-box}
html{scroll-behavior:smooth}
body{margin:0;background:var(--bg);color:var(--tx);font-family:var(--sans);
  font-size:15.5px;line-height:1.72;-webkit-font-smoothing:antialiased}
body::before{content:"";position:fixed;inset:0;z-index:-2;
  background:
    radial-gradient(60% 50% at 78% -8%, #1b3a4a55, transparent 70%),
    radial-gradient(55% 45% at 12% 4%, #2c224a55, transparent 70%),
    var(--bg);}
body::after{content:"";position:fixed;inset:0;z-index:-1;opacity:.4;
  background-image:linear-gradient(#ffffff05 1px,transparent 1px),linear-gradient(90deg,#ffffff05 1px,transparent 1px);
  background-size:54px 54px;mask-image:radial-gradient(circle at 50% 0,#000,transparent 80%)}
a{color:var(--cy);text-decoration:none}
a:hover{text-decoration:underline}
.wrap{display:grid;grid-template-columns:248px minmax(0,1fr);gap:0;max-width:1320px;margin:0 auto}
/* nav */
nav{position:sticky;top:0;align-self:start;height:100vh;overflow-y:auto;padding:30px 18px 40px;
  border-right:1px solid var(--line2)}
nav .brand{font-family:var(--mono);font-weight:700;font-size:15px;letter-spacing:-.3px;line-height:1.35;
  background:linear-gradient(92deg,var(--cy),var(--vi));-webkit-background-clip:text;background-clip:text;color:transparent}
nav .brsub{color:var(--faint);font-size:11.5px;margin:6px 0 22px;font-family:var(--mono)}
nav a{display:block;color:var(--dim);font-size:13.5px;padding:6px 11px;border-radius:8px;margin:1px 0;
  border-left:2px solid transparent;transition:.15s}
nav a:hover{background:#ffffff08;color:var(--tx);text-decoration:none}
nav a.on{color:var(--tx);background:#22d3ee14;border-left-color:var(--cy)}
nav .nsec{color:var(--faint);font-size:10.5px;text-transform:uppercase;letter-spacing:.12em;margin:18px 0 6px 11px}
main{padding:0 clamp(20px,4vw,60px) 120px;min-width:0}
/* hero */
.hero{padding:64px 0 40px}
.kick{display:inline-flex;gap:9px;align-items:center;font-family:var(--mono);font-size:12px;color:var(--cy);
  border:1px solid var(--cy);border-color:#22d3ee44;background:#22d3ee0e;padding:5px 13px;border-radius:999px}
.kick b{color:var(--em)}
h1{font-size:clamp(30px,5vw,50px);line-height:1.08;letter-spacing:-1.4px;margin:22px 0 0;font-weight:800}
h1 .g{background:linear-gradient(96deg,var(--cy) 10%,var(--em) 50%,var(--vi));-webkit-background-clip:text;background-clip:text;color:transparent}
.pitch{font-size:18.5px;color:var(--dim);max-width:62ch;margin:20px 0 0;line-height:1.6}
.metarow{display:flex;flex-wrap:wrap;gap:10px;margin-top:30px}
.chip{font-family:var(--mono);font-size:12px;color:var(--dim);background:var(--card);border:1px solid var(--line2);
  padding:7px 13px;border-radius:9px}
.chip b{color:var(--tx)}
.chip .k{color:var(--faint)}
/* sections */
section{padding:50px 0;border-top:1px solid var(--line);scroll-margin-top:18px}
.sh{display:flex;align-items:baseline;gap:14px;margin:0 0 8px}
.sh .no{font-family:var(--mono);font-size:13px;color:var(--cy);font-weight:700}
h2{font-size:27px;letter-spacing:-.7px;margin:0;font-weight:750}
.sub{color:var(--dim);font-size:15px;margin:0 0 26px;max-width:78ch}
.lead{font-size:17px;color:var(--tx);max-width:80ch;line-height:1.7}
.dim{color:var(--dim)}
/* grid + cards */
.grid{display:grid;gap:14px}
.g2{grid-template-columns:repeat(2,1fr)}
.g3{grid-template-columns:repeat(3,1fr)}
.g4{grid-template-columns:repeat(4,1fr)}
@media(max-width:900px){.g2,.g3,.g4{grid-template-columns:1fr}}
.card{background:linear-gradient(180deg,var(--card),var(--bg1));border:1px solid var(--line2);border-radius:var(--r);
  padding:20px 22px;position:relative;transition:.18s}
.card:hover{border-color:#3a5170;transform:translateY(-2px);box-shadow:0 14px 40px -22px #000}
.card h3{margin:0 0 8px;font-size:16.5px;letter-spacing:-.2px}
.card p{margin:0;color:var(--dim);font-size:14px}
.num{font-family:var(--mono);color:var(--vi);font-size:13px;font-weight:700}
code,.mono{font-family:var(--mono);font-size:.88em}
:not(pre)>code{background:#ffffff0d;border:1px solid var(--line);padding:1.5px 6px;border-radius:6px;color:#bfe6ef}
/* badges */
.b{display:inline-flex;align-items:center;gap:5px;font-family:var(--mono);font-size:11px;font-weight:700;
  padding:3px 9px;border-radius:7px;letter-spacing:.02em;white-space:nowrap}
.b-xl{background:#fb71851f;color:var(--rs);border:1px solid #fb718544}
.b-l{background:#fbbf241f;color:var(--am);border:1px solid #fbbf2444}
.b-m{background:#22d3ee1f;color:var(--cy);border:1px solid #22d3ee44}
.b-s{background:#34d3991f;color:var(--em);border:1px solid #34d39944}
.b-high{background:#fb71851f;color:var(--rs);border:1px solid #fb718544}
.b-medium{background:#fbbf241f;color:var(--am);border:1px solid #fbbf2444}
.b-low{background:#34d3991f;color:var(--em);border:1px solid #34d39944}
.b-gh{background:#a78bfa1f;color:var(--vi);border:1px solid #a78bfa44}
.b-done{background:#34d39926;color:var(--em);border:1px solid #34d39955}
.b-prog{background:#60a5fa22;color:var(--bl);border:1px solid #60a5fa55}
.b-plan{background:#ffffff0a;color:var(--faint);border:1px solid var(--line2)}
/* roadmap timeline */
.tl{position:relative;margin-top:8px;padding-left:0}
.ph{position:relative;border:1px solid var(--line2);border-radius:var(--r);margin:0 0 14px;overflow:hidden;
  background:linear-gradient(180deg,var(--card),var(--bg1))}
.ph>summary{list-style:none;cursor:pointer;padding:18px 22px;display:flex;align-items:center;gap:16px}
.ph>summary::-webkit-details-marker{display:none}
.ph .pno{font-family:var(--mono);font-weight:800;font-size:15px;min-width:42px;height:42px;display:grid;place-items:center;
  border-radius:11px;background:#22d3ee14;color:var(--cy);border:1px solid #22d3ee3a}
.ph[open] .pno{background:linear-gradient(135deg,var(--cy),var(--vi));color:#04121a;border-color:transparent}
.ph .pti{flex:1;min-width:0}
.ph .pti b{font-size:16.5px;letter-spacing:-.2px}
.ph .pti .pg{color:var(--dim);font-size:13.5px;margin-top:3px}
.ph .pbadges{display:flex;gap:7px;flex-wrap:wrap;align-items:center}
.ph .body{padding:4px 22px 22px 80px;border-top:1px solid var(--line)}
.ph .body h4{margin:18px 0 9px;font-size:12px;text-transform:uppercase;letter-spacing:.11em;color:var(--faint);font-family:var(--mono)}
.lst{list-style:none;padding:0;margin:0;display:grid;gap:7px}
.lst li{position:relative;padding-left:20px;color:var(--dim);font-size:14px}
.lst li::before{content:"";position:absolute;left:2px;top:9px;width:7px;height:7px;border-radius:2px;
  background:linear-gradient(135deg,var(--cy),var(--em))}
.kc{display:grid;gap:6px}
.kc .row{display:grid;grid-template-columns:minmax(120px,210px) 1fr;gap:12px;font-size:13.5px;
  padding:8px 0;border-bottom:1px dashed var(--line)}
.kc .row .a{font-family:var(--mono);font-size:12.5px;color:var(--am)}
.kc .row .c{color:var(--dim)}
.exit{margin-top:16px;background:#34d3990d;border:1px solid #34d39933;border-radius:var(--rs2);padding:12px 15px;
  font-size:13.5px;color:var(--tx)}
.exit b{color:var(--em);font-family:var(--mono);font-size:11px;text-transform:uppercase;letter-spacing:.08em}
.provtag{font-family:var(--mono);font-size:11.5px;color:var(--vi);background:#a78bfa12;border:1px solid #a78bfa33;
  padding:3px 9px;border-radius:7px;display:inline-block;margin:3px 5px 0 0}
/* ADR */
.adr{border:1px solid var(--line2);border-radius:var(--r);overflow:hidden;background:linear-gradient(180deg,var(--card),var(--bg1))}
.adr>summary{cursor:pointer;list-style:none;padding:16px 20px;display:flex;gap:14px;align-items:center}
.adr>summary::-webkit-details-marker{display:none}
.adr .aid{font-family:var(--mono);font-weight:800;color:var(--vi);font-size:13px;min-width:52px}
.adr .atit{flex:1;font-weight:650;font-size:15.5px}
.adr .arrow{color:var(--faint);transition:.2s}
.adr[open] .arrow{transform:rotate(90deg);color:var(--cy)}
.adr .abody{padding:2px 20px 20px 20px;border-top:1px solid var(--line);display:grid;gap:12px}
.adr .field>span{font-family:var(--mono);font-size:10.5px;text-transform:uppercase;letter-spacing:.1em;color:var(--faint);display:block;margin-bottom:3px}
.adr .field.dec p{color:var(--tx)}
.adr .field p{margin:0;color:var(--dim);font-size:14px}
.adr .two{display:grid;grid-template-columns:1fr 1fr;gap:14px}
@media(max-width:760px){.adr .two{grid-template-columns:1fr}}
/* arch layers */
.layer{display:grid;grid-template-columns:max-content 1fr;gap:18px;align-items:start;
  border:1px solid var(--line2);border-left:3px solid var(--cy);border-radius:12px;padding:16px 20px;margin:0 0 12px;
  background:linear-gradient(180deg,var(--card),var(--bg1))}
.layer:nth-child(2){border-left-color:var(--em)}
.layer:nth-child(3){border-left-color:var(--vi)}
.layer:nth-child(4){border-left-color:var(--am)}
.layer .ln{font-weight:700;font-size:15px;max-width:210px}
.layer .lr{color:var(--dim);font-size:13.5px;margin-top:4px}
.layer .comp{display:flex;flex-wrap:wrap;gap:6px;margin-top:4px}
.layer .comp span{font-family:var(--mono);font-size:11.5px;color:var(--dim);background:#ffffff08;border:1px solid var(--line2);padding:3px 9px;border-radius:7px}
.flow{margin-top:14px;background:var(--card2);border:1px solid var(--line2);border-radius:var(--r);padding:18px 20px}
.flow b{color:var(--cy);font-family:var(--mono);font-size:11px;letter-spacing:.08em;text-transform:uppercase}
.flow p{margin:8px 0 0;color:var(--dim);font-size:14px}
/* matrix */
.mtx{display:grid;grid-template-columns:1fr 1fr;gap:14px}
@media(max-width:760px){.mtx{grid-template-columns:1fr}}
.mbox{border:1px solid var(--line2);border-radius:var(--r);padding:18px 20px;background:linear-gradient(180deg,var(--card),var(--bg1))}
.mbox.cur{border-color:#fb718533}.mbox.tgt{border-color:#34d39933}
.mbox .t{font-family:var(--mono);font-size:11px;text-transform:uppercase;letter-spacing:.1em;margin-bottom:8px}
.mbox.cur .t{color:var(--rs)}.mbox.tgt .t{color:var(--em)}
.mbox p{margin:0;color:var(--dim);font-size:14px}
.gaps{display:grid;gap:8px;margin-top:14px}
.gaps li{list-style:none;display:flex;gap:11px;align-items:flex-start;font-size:14px;color:var(--dim);
  border:1px solid var(--line2);border-radius:10px;padding:11px 14px;background:var(--card)}
.gaps li::before{content:"GAP";font-family:var(--mono);font-size:10px;font-weight:700;color:var(--rs);
  background:#fb71851a;border:1px solid #fb718540;padding:2px 7px;border-radius:6px;margin-top:1px;flex-shrink:0}
.plan-box{margin-top:14px;border:1px solid #22d3ee33;background:#22d3ee08;border-radius:var(--r);padding:18px 20px}
.plan-box .t{font-family:var(--mono);font-size:11px;color:var(--cy);text-transform:uppercase;letter-spacing:.09em;margin-bottom:8px}
.plan-box p{margin:0;color:var(--dim);font-size:14px;line-height:1.7}
/* use case pills */
.uc{display:grid;gap:8px}
.uc li{list-style:none;font-size:14px;color:var(--dim);padding-left:26px;position:relative}
.uc li::before{content:"AI";position:absolute;left:0;top:1px;font-family:var(--mono);font-size:9.5px;font-weight:800;
  color:var(--vi);background:#a78bfa18;border:1px solid #a78bfa44;border-radius:6px;padding:2px 5px}
/* table */
.tbl{width:100%;border-collapse:collapse;font-size:13.8px;margin-top:6px}
.tbl th{text-align:left;font-family:var(--mono);font-size:11px;text-transform:uppercase;letter-spacing:.07em;
  color:var(--faint);padding:9px 12px;border-bottom:1px solid var(--line2)}
.tbl td{padding:11px 12px;border-bottom:1px solid var(--line);color:var(--dim);vertical-align:top}
.tbl tr:hover td{background:#ffffff04}
.tbl td b{color:var(--tx)}
.arrow-c{color:var(--em);font-family:var(--mono)}
/* metrics */
.met{display:grid;grid-template-columns:1.3fr auto 1.3fr;gap:0;align-items:center;
  border:1px solid var(--line2);border-radius:12px;padding:14px 18px;margin-bottom:10px;background:var(--card)}
@media(max-width:760px){.met{grid-template-columns:1fr}}
.met .mn{font-weight:700;font-size:14.5px;grid-column:1/-1;margin-bottom:6px}
.met .from{font-size:13px;color:var(--rs)}
.met .to{font-size:13px;color:var(--em);text-align:right}
.met .ar{color:var(--faint);font-family:var(--mono);padding:0 16px}
@media(max-width:760px){.met .to{text-align:left}.met .ar{padding:4px 0}}
/* research / analysis */
.rcard{border:1px solid var(--line2);border-radius:var(--r);overflow:hidden;margin-bottom:12px;background:linear-gradient(180deg,var(--card),var(--bg1))}
.rcard>summary{cursor:pointer;list-style:none;padding:16px 20px;display:flex;gap:13px;align-items:center}
.rcard>summary::-webkit-details-marker{display:none}
.rcard .rt{flex:1;font-weight:650;font-size:15px}
.rcard .rdot{width:9px;height:9px;border-radius:50%;background:linear-gradient(135deg,var(--cy),var(--vi));flex-shrink:0}
.rcard .rbody{padding:4px 20px 20px;border-top:1px solid var(--line);display:grid;gap:14px}
.rcard h4{margin:14px 0 8px;font-size:11.5px;text-transform:uppercase;letter-spacing:.1em;color:var(--faint);font-family:var(--mono)}
.srcs{display:flex;flex-wrap:wrap;gap:7px}
.srcs a{font-family:var(--mono);font-size:11.5px;background:#ffffff08;border:1px solid var(--line2);padding:4px 9px;border-radius:7px;color:var(--cy)}
.weak li::before{background:var(--rs)!important}
.blk li::before{background:var(--am)!important}
.opp li::before{background:var(--em)!important}
/* score bars */
.score{display:grid;grid-template-columns:1fr auto;gap:10px;align-items:center;margin:4px 0 14px}
.score .bar{height:9px;border-radius:6px;background:#ffffff0d;overflow:hidden}
.score .fill{height:100%;border-radius:6px;background:linear-gradient(90deg,var(--cy),var(--em))}
.score .v{font-family:var(--mono);font-weight:800;font-size:18px}
.win{border-color:#34d39955!important;box-shadow:0 0 0 1px #34d39933, 0 18px 50px -30px #34d39966}
.winbadge{font-family:var(--mono);font-size:10px;font-weight:800;color:#04121a;background:var(--em);padding:3px 8px;border-radius:6px}
/* handoff */
.handoff{border:1px solid #fbbf2440;background:linear-gradient(180deg,#fbbf240a,var(--bg1));border-radius:var(--r);padding:24px 26px}
.handoff .t{font-family:var(--mono);color:var(--am);font-size:12px;letter-spacing:.08em;text-transform:uppercase}
.handoff p{color:var(--tx);font-size:15px;line-height:1.78;margin:12px 0 0}
.next{margin-top:18px;display:grid;gap:9px}
.next li{list-style:none;display:flex;gap:12px;align-items:flex-start;font-size:14.5px;color:var(--dim)}
.next li .n{font-family:var(--mono);font-weight:800;color:var(--am);background:#fbbf2418;border:1px solid #fbbf2440;
  min-width:26px;height:26px;border-radius:8px;display:grid;place-items:center;font-size:12px;flex-shrink:0}
footer{border-top:1px solid var(--line2);margin-top:60px;padding:30px 0;color:var(--faint);font-size:12.5px;font-family:var(--mono)}
.toggle-all{font-family:var(--mono);font-size:12px;color:var(--cy);background:transparent;border:1px solid #22d3ee44;
  padding:6px 13px;border-radius:8px;cursor:pointer;margin-bottom:14px}
.toggle-all:hover{background:#22d3ee12}
@media(max-width:980px){.wrap{grid-template-columns:1fr}nav{display:none}}
"""

# ------------------------------------------------------------------ JS
JS = r"""
const secs=[...document.querySelectorAll('section[id]')];
const links=[...document.querySelectorAll('nav a')];
const byId={};links.forEach(a=>byId[a.getAttribute('href').slice(1)]=a);
const io=new IntersectionObserver((es)=>{es.forEach(en=>{if(en.isIntersecting){
  links.forEach(l=>l.classList.remove('on'));const a=byId[en.target.id];if(a)a.classList.add('on');}});},
  {rootMargin:'-12% 0px -78% 0px'});
secs.forEach(s=>io.observe(s));
document.querySelectorAll('[data-toggle]').forEach(btn=>{
  btn.addEventListener('click',()=>{
    const sel=btn.getAttribute('data-toggle');
    const ds=document.querySelectorAll(sel);
    const anyClosed=[...ds].some(d=>!d.open);
    ds.forEach(d=>d.open=anyClosed);
    btn.textContent=anyClosed?btn.dataset.close:btn.dataset.open;
  });
});
"""


# ------------------------------------------------------------------ builders
def badge_effort(x):
    return f'<span class="b b-{e(x).lower()}">{e(x)}</span>'


def badge_risk(x):
    return f'<span class="b b-{e(x).lower()}">RISK {e(x)}</span>'


def status_badge(phase):
    st = STATUS.get(phase, "planned")
    cls = {"done": "b-done", "in_progress": "b-prog", "planned": "b-plan"}.get(st, "b-plan")
    lab = {"done": "완료", "in_progress": "진행중", "planned": "예정"}.get(st, "예정")
    return f'<span class="b {cls}">{lab}</span>'


def principles():
    cards = ""
    for i, p in enumerate(master["designPrinciples"], 1):
        cards += f"""<div class="card"><div class="num">P{i:02d}</div>
        <h3>{e(p['name'])}</h3><p>{e(p['description'])}</p></div>"""
    return f'<div class="grid g2">{cards}</div>'


def architecture():
    layers = ""
    for L in master["targetArchitecture"]["layers"]:
        comps = "".join(f"<span>{e(c)}</span>" for c in L["components"])
        layers += f"""<div class="layer"><div><div class="ln">{e(L['name'])}</div></div>
        <div><div class="lr">{e(L['responsibility'])}</div><div class="comp">{comps}</div></div></div>"""
    flow = master["targetArchitecture"]["dataFlow"]
    return f"""<p class="lead">{e(master['targetArchitecture']['overview'])}</p>
    <div style="margin-top:22px">{layers}</div>
    <div class="flow"><b>데이터 흐름</b><p>{e(flow)}</p></div>"""


def adrs():
    out = ""
    for a in master["coreDecisions"]:
        out += f"""<details class="adr"><summary>
        <span class="aid">{e(a['id'])}</span><span class="atit">{e(a['title'])}</span>
        <span class="arrow">&#9656;</span></summary>
        <div class="abody">
          <div class="field dec"><span>결정</span><p>{e(a['decision'])}</p></div>
          <div class="field"><span>근거</span><p>{e(a['rationale'])}</p></div>
          <div class="two">
            <div class="field"><span>대안</span><p>{e(a.get('alternatives',''))}</p></div>
            <div class="field"><span>트레이드오프</span><p>{e(a['tradeoffs'])}</p></div>
          </div>
        </div></details>"""
    return out


def roadmap():
    out = ""
    for i, r in enumerate(master["roadmap"]):
        deliv = "".join(f"<li>{e(d)}</li>" for d in r.get("deliverables", []))
        kc = "".join(
            f'<div class="row"><div class="a">{e(c["area"])}</div><div class="c">{e(c["change"])}</div></div>'
            for c in r.get("keyChanges", [])
        )
        provs = "".join(f'<span class="provtag">+ {e(p)}</span>' for p in r.get("newProviders", []))
        provs_html = f'<h4>신규 Provider</h4><div>{provs}</div>' if provs else ""
        dep = r.get("dependsOn")
        dep_html = f'<span class="b b-plan">depends · {e(dep)}</span>' if dep else ""
        open_attr = " open" if i == 0 else ""
        out += f"""<details class="ph"{open_attr}><summary>
          <div class="pno">{e(r['phase'])}</div>
          <div class="pti"><b>{e(r['title'])}</b><div class="pg">{e(r['goal'])}</div></div>
          <div class="pbadges">{status_badge(r['phase'])}{badge_effort(r['effort'])}{badge_risk(r['risk'])}{dep_html}</div>
        </summary>
        <div class="body">
          <h4>산출물</h4><ul class="lst">{deliv}</ul>
          {('<h4>핵심 코드 변경</h4><div class="kc">'+kc+'</div>') if kc else ''}
          {provs_html}
          <div class="exit"><b>Exit Criteria</b><br>{e(r['exitCriteria'])}</div>
        </div></details>"""
    return f'<div class="tl">{out}</div>'


def matrix():
    m = master["conversionMatrix"]
    gaps = "".join(f"<li>{e(g)}</li>" for g in m["gaps"])
    return f"""<div class="mtx">
      <div class="mbox cur"><div class="t">현재 상태</div><p>{e(m['currentState'])}</p></div>
      <div class="mbox tgt"><div class="t">목표 상태</div><p>{e(m['targetState'])}</p></div>
    </div>
    <h4 style="font-family:var(--mono);color:var(--faint);font-size:11.5px;letter-spacing:.1em;text-transform:uppercase;margin:24px 0 10px">구조적 공백</h4>
    <ul class="gaps">{gaps}</ul>
    <div class="plan-box"><div class="t">그래프 라우팅 설계 — ProviderRegistry → ConversionGraph</div><p>{e(m['graphRoutingPlan'])}</p></div>"""


def ai_section():
    ai = master["aiIntegration"]
    uc = "".join(f"<li>{e(u)}</li>" for u in ai["useCases"])
    return f"""<div class="grid g2">
      <div class="card"><h3>Codex non-interactive OAuth</h3><p>{e(ai['codexOAuth'])}</p></div>
      <div class="card"><h3>API 키 모드 (기본 경로)</h3><p>{e(ai['apiMode'])}</p></div>
    </div>
    <div class="card" style="margin-top:14px"><h3>활용 사례 (AI 전용 신규 엣지)</h3><ul class="uc" style="margin-top:12px">{uc}</ul></div>
    <div class="card" style="margin-top:14px"><h3>아키텍처 — AI는 끄면 사라지는 부가 엣지</h3><p>{e(ai['architecture'])}</p></div>"""


def media_section():
    m = master["mediaLayer"]
    rows = [
        ("영상 (Video)", m["video"], "--cy"),
        ("오디오 (Audio)", m["audio"], "--em"),
        ("PDF 압축", m["pdfCompression"], "--vi"),
        ("이미지 최적화", m["imageOptim"], "--am"),
    ]
    cards = "".join(
        f'<div class="card"><h3 style="color:var({c})">{e(t)}</h3><p>{e(d)}</p></div>'
        for t, d, c in rows
    )
    return f"""<div class="grid g2">{cards}</div>
    <div class="plan-box" style="margin-top:14px"><div class="t">외부 바이너리 · 라이선스 게이트 전략</div><p>{e(m['approach'])}</p></div>"""


def risks():
    rows = ""
    for r in master["riskRegister"]:
        rows += f"""<tr><td><b>{e(r['risk'])}</b></td>
        <td>{badge_risk(r['likelihood'])}</td><td>{badge_risk(r['impact'])}</td>
        <td>{e(r['mitigation'])}</td></tr>"""
    return f"""<table class="tbl"><thead><tr><th style="width:34%">리스크</th><th>발생가능</th><th>영향</th><th>완화책</th></tr></thead>
    <tbody>{rows}</tbody></table>"""


def metrics():
    out = ""
    for m in master["successMetrics"]:
        out += f"""<div class="met"><div class="mn">{e(m['metric'])}</div>
        <div class="from">{e(m['current'])}</div><div class="ar">&#8594;</div><div class="to">{e(m['target'])}</div></div>"""
    return out


def research_section():
    out = ""
    for r in researches:
        libs = ""
        for L in r.get("libraries", []):
            libs += f"""<tr><td><b>{e(L.get('name'))}</b></td><td>{e(L.get('purpose'))}</td>
            <td><code>{e(L.get('license'))}</code></td><td>{e(L.get('maturity'))}</td></tr>"""
        libtbl = (f'<h4>라이브러리 · 도구</h4><table class="tbl"><thead><tr><th>이름</th><th>용도</th><th>라이선스</th><th>성숙도</th></tr></thead><tbody>{libs}</tbody></table>') if libs else ""
        finds = "".join(f"<li>{e(k)}</li>" for k in r.get("keyFindings", []))
        srcs = "".join(
            f'<a href="{e(s)}" target="_blank" rel="noopener">{e(s.split("//")[-1][:46])}</a>'
            for s in r.get("sources", []) if str(s).startswith("http")
        )
        out += f"""<details class="rcard"><summary><span class="rdot"></span><span class="rt">{e(r['topic'])}</span><span class="arrow" style="color:var(--faint)">&#9656;</span></summary>
        <div class="rbody">
          <div><h4>핵심 발견</h4><ul class="lst">{finds}</ul></div>
          <div><h4>권장 접근 (.NET 9 / 단일 EXE)</h4><p class="dim" style="font-size:14px;margin:0">{e(r.get('recommendedApproach'))}</p></div>
          {libtbl}
          <div><h4>통합 노트</h4><p class="dim" style="font-size:14px;margin:0">{e(r.get('integrationNotes'))}</p></div>
          {('<div><h4>출처</h4><div class="srcs">'+srcs+'</div></div>') if srcs else ''}
        </div></details>"""
    return out


def analysis_section():
    out = ""
    for a in analyses:
        weak = "".join(f"<li>{e(w)}</li>" for w in a.get("weaknesses", []))
        blk = "".join(f"<li>{e(b)}</li>" for b in a.get("extensibilityBlockers", []))
        opp = "".join(
            f'<li>{e(o["title"])} <span class="b b-{e(o["impact"]).lower()}">impact {e(o["impact"])}</span> <span class="b b-{e(o["effort"]).lower()}">effort {e(o["effort"])}</span></li>'
            for o in a.get("improvementOpportunities", [])
        )
        out += f"""<details class="rcard"><summary><span class="rdot" style="background:linear-gradient(135deg,var(--am),var(--rs))"></span><span class="rt">{e(a['subsystem'])}</span><span class="arrow" style="color:var(--faint)">&#9656;</span></summary>
        <div class="rbody">
          <p class="dim" style="font-size:14px;margin:0">{e(a.get('summary'))}</p>
          <div><h4 style="color:var(--rs)">설계 약점</h4><ul class="lst weak">{weak}</ul></div>
          <div><h4 style="color:var(--am)">확장성 차단 요소 (file:line)</h4><ul class="lst blk">{blk}</ul></div>
          <div><h4 style="color:var(--em)">개선 기회</h4><ul class="lst opp">{opp}</ul></div>
        </div></details>"""
    return out


def designs_section():
    rk = {r["angle"].split("(")[0].strip(): r for r in ranking["rankings"]}
    # map by index hint in angle text
    cards = ""
    for idx, d in enumerate(designs):
        # find matching ranking by 'idx N'
        rinfo = next((r for r in ranking["rankings"] if f"idx {idx}" in r["angle"]), None)
        score = rinfo["score"] if rinfo else "-"
        is_win = (rinfo and rinfo["score"] == max(rr["score"] for rr in ranking["rankings"]))
        pct = (score if isinstance(score, (int, float)) else 0)
        moves = "".join(f"<li>{e(mv)}</li>" for mv in d.get("keyMoves", [])[:6])
        win_html = '<span class="winbadge">SELECTED BASE</span>' if is_win else ""
        cards += f"""<div class="card {'win' if is_win else ''}">
        <div style="display:flex;justify-content:space-between;align-items:center;gap:10px">
          <h3 style="margin:0">{e(d['angle'])}</h3>{win_html}</div>
        <div class="score"><div class="bar"><div class="fill" style="width:{pct}%"></div></div><div class="v">{e(score)}</div></div>
        <p style="color:var(--dim);font-size:13.5px">{e(d['vision'])[:340]}…</p>
        <h4 style="font-family:var(--mono);color:var(--faint);font-size:11px;letter-spacing:.09em;text-transform:uppercase;margin:14px 0 8px">핵심 변경</h4>
        <ul class="lst" style="font-size:13px">{moves}</ul>
        <p style="margin-top:12px;font-size:12.5px;color:var(--rs)"><b style="font-family:var(--mono)">최대 리스크</b> · {e(d.get('biggestRisk',''))[:170]}</p>
        </div>"""
    graft = "".join(f"<li>{e(g)}</li>" for g in ranking["bestIdeasToGraft"])
    return f"""<div class="grid g3">{cards}</div>
    <div class="plan-box" style="margin-top:20px"><div class="t">심사 종합 권고</div><p>{nl2br(ranking['recommendation'])}</p></div>
    <h4 style="font-family:var(--mono);color:var(--faint);font-size:11.5px;letter-spacing:.1em;text-transform:uppercase;margin:26px 0 10px">마스터플랜에 흡수한 최고의 아이디어</h4>
    <ul class="lst opp" style="gap:9px">{graft}</ul>"""


# nav
NAV_ITEMS = [
    ("개요", [("vision", "비전"), ("principles", "설계 원칙"), ("architecture", "타깃 아키텍처")]),
    ("설계", [("adr", "핵심 결정 (ADR)"), ("matrix", "변환 매트릭스"), ("ai", "AI 통합"), ("media", "미디어 레이어")]),
    ("실행", [("roadmap", "로드맵 P1–P8"), ("metrics", "성공 지표"), ("risks", "리스크 레지스터"), ("handoff", "다음 세션 인계")]),
    ("근거", [("designs", "설계안 · 심사"), ("research", "인터넷 리서치"), ("analysis", "코드 분석")]),
]


def nav():
    out = ""
    for grp, items in NAV_ITEMS:
        out += f'<div class="nsec">{grp}</div>'
        for hid, lab in items:
            out += f'<a href="#{hid}">{lab}</a>'
    return out


u = meta["usage"]
metarow = f"""<div class="metarow">
  <div class="chip"><span class="k">생성</span> <b>{e(meta['generatedDate'])}</b></div>
  <div class="chip"><span class="k">방법</span> <b>멀티에이전트 Workflow</b></div>
  <div class="chip"><span class="k">에이전트</span> <b>{u['agents']}</b></div>
  <div class="chip"><span class="k">서브에이전트 토큰</span> <b>{u['subagentTokens']:,}</b></div>
  <div class="chip"><span class="k">도구 호출</span> <b>{u['toolUses']}</b></div>
  <div class="chip"><span class="k">종합</span> <b>{e(meta['winningSynthesis'])}</b></div>
</div>"""

HTML = f"""<!doctype html><html lang="ko"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>{e(meta['title'])}</title>
<link rel="preconnect" href="https://cdn.jsdelivr.net">
<link rel="stylesheet" href="https://cdn.jsdelivr.net/gh/orioncactus/pretendard@v1.3.9/dist/web/variable/pretendardvariable.min.css">
<link rel="stylesheet" href="https://cdn.jsdelivr.net/gh/projectnoonnu/[email protected]/JetBrainsMono.css">
<style>{CSS}</style></head>
<body><div class="wrap">
<nav>
  <div class="brand">Everything2<br>Everything</div>
  <div class="brsub">▶ 변환 그래프 OS · SSOT</div>
  {nav()}
</nav>
<main>
  <header class="hero">
    <div class="kick">MASTER PLAN · <b>Single Source of Truth</b></div>
    <h1>모든 변환을 엣지로, <span class="g">엔진이 경로를 합성</span>하는 만능 변환기</h1>
    <p class="pitch">{e(master['elevatorPitch'])}</p>
    {metarow}
  </header>

  <section id="vision"><div class="sh"><span class="no">00</span><h2>북극성 비전</h2></div>
    <p class="lead">{e(master['vision'])}</p>
  </section>

  <section id="principles"><div class="sh"><span class="no">01</span><h2>설계 원칙</h2></div>
    <p class="sub">아키텍처를 관통하는 불변식. 모든 코드 변경은 이 원칙을 위배하지 않아야 한다.</p>
    {principles()}
  </section>

  <section id="architecture"><div class="sh"><span class="no">02</span><h2>타깃 아키텍처</h2></div>
    <p class="sub">4계층 변환 그래프 아키텍처. 핵심은 ProviderRegistry를 단일 홉 딕셔너리에서 ConversionGraph로 승격하는 것.</p>
    {architecture()}
  </section>

  <section id="adr"><div class="sh"><span class="no">03</span><h2>핵심 아키텍처 결정 (ADR)</h2></div>
    <p class="sub">현재 코드의 구체적 한계를 직접 겨냥한 결정. 클릭하면 근거·대안·트레이드오프가 펼쳐진다.</p>
    <button class="toggle-all" data-toggle=".adr" data-open="모두 펼치기" data-close="모두 접기">모두 펼치기</button>
    <div class="grid" style="gap:10px">{adrs()}</div>
  </section>

  <section id="matrix"><div class="sh"><span class="no">04</span><h2>변환 매트릭스 · 그래프 라우팅</h2></div>
    <p class="sub">단일 홉 → 멀티홉 자동 합성. transitive closure로 "이 파일로 만들 수 있는 모든 포맷"이 폭발한다.</p>
    {matrix()}
  </section>

  <section id="ai"><div class="sh"><span class="no">05</span><h2>AI 통합 — Codex OAuth + API</h2></div>
    <p class="sub">키가 없어도 모든 기존 변환은 100% 동작. AI는 ✨ 배지로만 opt-in 노출되는 부가가치 엣지.</p>
    {ai_section()}
  </section>

  <section id="media"><div class="sh"><span class="no">06</span><h2>미디어 레이어 — 영상·오디오·PDF 압축</h2></div>
    <p class="sub">FFmpeg(LGPL 분리 호출)·Ghostscript(AGPL 감지만)로 카테고리를 미디어 변환기로 점프.</p>
    {media_section()}
  </section>

  <section id="roadmap"><div class="sh"><span class="no">07</span><h2>실행 로드맵 · P1 → P8</h2></div>
    <p class="sub">각 단계가 독립적으로 가치를 전달하고 이전 단계에 의존한다. P1은 그래프 코어 + 즉시 체감(PDF 압축)을 함께 심는다.</p>
    <button class="toggle-all" data-toggle=".ph" data-open="모두 펼치기" data-close="모두 접기">모두 펼치기</button>
    {roadmap()}
  </section>

  <section id="metrics"><div class="sh"><span class="no">08</span><h2>성공 지표</h2></div>
    <p class="sub">현재 → 목표. 각 지표가 로드맵 완료를 객관적으로 측정한다.</p>
    {metrics()}
  </section>

  <section id="risks"><div class="sh"><span class="no">09</span><h2>리스크 레지스터</h2></div>
    <p class="sub">가장 큰 위협은 "보이지 않는 리팩터링의 함정"과 GPL/AGPL 라이선스 오염.</p>
    {risks()}
  </section>

  <section id="handoff"><div class="sh"><span class="no">10</span><h2>다음 세션 인계 노트</h2></div>
    <p class="sub">이 문서가 SSOT다. 다음 세션은 아래 순서대로 시작한다.</p>
    <div class="handoff"><div class="t">▶ Handoff — 어디서부터 시작하고 무엇을 먼저 검증할지</div>
      <p>{nl2br(master['ssotNotes'])}</p>
    </div>
  </section>

  <section id="designs"><div class="sh"><span class="no">11</span><h2>설계안 비교 · 심사</h2></div>
    <p class="sub">3개 독립 아키텍트가 서로 다른 각도에서 제안했고, 심사가 점수화·종합했다.</p>
    {designs_section()}
  </section>

  <section id="research"><div class="sh"><span class="no">12</span><h2>인터넷 리서치 (7개 토픽)</h2></div>
    <p class="sub">2026년 6월 기준 WebSearch로 조사한 라이브러리·방법론·라이선스. 클릭하면 출처까지 펼쳐진다.</p>
    <button class="toggle-all" data-toggle=".rcard" data-open="모두 펼치기" data-close="모두 접기">모두 펼치기</button>
    {research_section()}
  </section>

  <section id="analysis"><div class="sh"><span class="no">13</span><h2>코드 심층 분석 (5개 서브시스템)</h2></div>
    <p class="sub">현재 코드를 직접 읽고 file:line으로 인용한 약점·확장 차단 요소·개선 기회.</p>
    {analysis_section()}
  </section>

  <footer>
    Everything2Everything SSOT · 생성 {e(meta['generatedDate'])} · {u['agents']} agents · {u['subagentTokens']:,} tokens<br>
    이 페이지는 docs/ssot/_data/*.json 에서 python build.py 로 재생성됩니다. · {e(meta['repo'])}
  </footer>
</main>
</div>
<script>{JS}</script>
</body></html>"""

with open(os.path.join(HERE, "index.html"), "w", encoding="utf-8") as f:
    f.write(HTML)


# ------------------------------------------------------------------ Markdown SSOT
def md():
    L = []
    L.append(f"# {meta['title']}\n")
    L.append(f"> {meta['subtitle']}\n")
    L.append(f"**생성** {meta['generatedDate']} · **방법** {meta['method']}  ")
    L.append(f"**규모** {u['agents']} agents · {u['subagentTokens']:,} subagent tokens · {u['toolUses']} tool calls  ")
    L.append(f"**종합** {meta['winningSynthesis']}\n")
    L.append("> 이 문서는 SSOT다. `docs/ssot/_data/*.json` 을 갱신하고 `python docs/ssot/build.py` 로 재생성한다. 웹 버전은 `docs/ssot/index.html`.\n")
    L.append("---\n## 엘리베이터 피치\n")
    L.append(master["elevatorPitch"] + "\n")
    L.append("## 북극성 비전\n")
    L.append(master["vision"] + "\n")
    L.append("## 설계 원칙\n")
    for i, p in enumerate(master["designPrinciples"], 1):
        L.append(f"{i}. **{p['name']}** — {p['description']}")
    L.append("\n## 타깃 아키텍처\n")
    L.append(master["targetArchitecture"]["overview"] + "\n")
    for Lr in master["targetArchitecture"]["layers"]:
        L.append(f"- **{Lr['name']}** — {Lr['responsibility']}  \n  `{'`, `'.join(Lr['components'])}`")
    L.append(f"\n**데이터 흐름:** {master['targetArchitecture']['dataFlow']}\n")
    L.append("## 핵심 아키텍처 결정 (ADR)\n")
    for a in master["coreDecisions"]:
        L.append(f"### {a['id']} · {a['title']}")
        L.append(f"- **결정:** {a['decision']}")
        L.append(f"- **근거:** {a['rationale']}")
        if a.get("alternatives"):
            L.append(f"- **대안:** {a['alternatives']}")
        L.append(f"- **트레이드오프:** {a['tradeoffs']}\n")
    L.append("## 실행 로드맵\n")
    for r in master["roadmap"]:
        dep = f" · depends: {r['dependsOn']}" if r.get("dependsOn") else ""
        st = STATUS.get(r["phase"], "planned")
        L.append(f"### {r['phase']} · {r['title']}  `effort:{r['effort']}` `risk:{r['risk']}` `status:{st}`{dep}")
        L.append(f"**목표:** {r['goal']}\n")
        L.append("**산출물:**")
        for d in r.get("deliverables", []):
            L.append(f"- {d}")
        if r.get("keyChanges"):
            L.append("\n**핵심 코드 변경:**")
            for c in r["keyChanges"]:
                L.append(f"- `{c['area']}` — {c['change']}")
        if r.get("newProviders"):
            L.append(f"\n**신규 Provider:** {', '.join(r['newProviders'])}")
        L.append(f"\n**Exit Criteria:** {r['exitCriteria']}\n")
    L.append("## 변환 매트릭스 · 그래프 라우팅\n")
    m = master["conversionMatrix"]
    L.append(f"- **현재:** {m['currentState']}")
    L.append(f"- **목표:** {m['targetState']}\n")
    L.append("**구조적 공백:**")
    for g in m["gaps"]:
        L.append(f"- {g}")
    L.append(f"\n**그래프 라우팅 설계:** {m['graphRoutingPlan']}\n")
    L.append("## AI 통합\n")
    ai = master["aiIntegration"]
    L.append(f"- **Codex OAuth:** {ai['codexOAuth']}")
    L.append(f"- **API 모드:** {ai['apiMode']}")
    L.append(f"- **아키텍처:** {ai['architecture']}")
    L.append("- **활용 사례:**")
    for x in ai["useCases"]:
        L.append(f"  - {x}")
    L.append("\n## 미디어 레이어\n")
    ml = master["mediaLayer"]
    for k, lab in [("video", "영상"), ("audio", "오디오"), ("pdfCompression", "PDF 압축"), ("imageOptim", "이미지 최적화"), ("approach", "외부 바이너리·라이선스 전략")]:
        L.append(f"- **{lab}:** {ml[k]}")
    L.append("\n## 리스크 레지스터\n")
    L.append("| 리스크 | 발생 | 영향 | 완화책 |\n|---|---|---|---|")
    for r in master["riskRegister"]:
        L.append(f"| {r['risk']} | {r['likelihood']} | {r['impact']} | {r['mitigation']} |")
    L.append("\n## 성공 지표\n")
    L.append("| 지표 | 현재 | 목표 |\n|---|---|---|")
    for x in master["successMetrics"]:
        L.append(f"| {x['metric']} | {x['current']} | {x['target']} |")
    L.append("\n## 다음 세션 인계 노트 (Handoff)\n")
    L.append(master["ssotNotes"].replace("\\n", "\n") + "\n")
    L.append("---\n## 심사 종합 권고\n")
    L.append(ranking["recommendation"].replace("\\n", "\n") + "\n")
    L.append("### 마스터플랜에 흡수한 최고의 아이디어\n")
    for g in ranking["bestIdeasToGraft"]:
        L.append(f"- {g}")
    return "\n".join(L)


with open(os.path.join(HERE, "PLAN.md"), "w", encoding="utf-8") as f:
    f.write(md())

print("OK index.html", os.path.getsize(os.path.join(HERE, "index.html")), "bytes")
print("OK PLAN.md", os.path.getsize(os.path.join(HERE, "PLAN.md")), "bytes")
