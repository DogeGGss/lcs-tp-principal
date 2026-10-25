'use strict';
const D=window.PRESENTATION, $=s=>document.querySelector(s);
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let current=0,weaponId='fusil';
const num=n=>n.toLocaleString('es-AR'),dec=n=>n.toLocaleString('es-AR',{minimumFractionDigits:2,maximumFractionDigits:2});
const money=(n,sign=false)=>(n<0?'−':sign&&n>0?'+':'')+'US$ '+num(Math.abs(Math.round(n)));
const thousands=n=>n>=1e6?n.toLocaleString('es-AR',{maximumFractionDigits:1})+' M':num(Math.round(n/1000)*1000);
// Números del simulador de la guía: los tres escenarios lado a lado.
function scenarios(){
 const L=D.economics.scenarios,mid=L[1];
 return `<div class="grid">${L.map(x=>`<article class="card scenario"><p class="eyebrow">${esc(x.name)}</p><div class="result ${x.result>=0?'pos':'neg'}">${money(x.result,true)}<small> por mes</small></div><dl class="facts"><div><dt>Jugadores por mes</dt><dd>${num(x.players)}</dd></div><div><dt>Pagan</dt><dd>${num(x.payers)} · ${x.conv.toLocaleString('es-AR')} %</dd></div><div><dt>Ganancias en base al pago de los jugadores</dt><dd>${money(x.gross)}</dd></div><div><dt>Cuesta</dt><dd>${money(x.costs)}</dd></div><div><dt>Recupero</dt><dd>${x.payback?'mes '+x.payback:'no se recupera'}</dd></div></dl></article>`).join('')}</div><p class="lead">El promedio deja de perder plata desde ${thousands(mid.breakEven)} jugadores por mes.</p>`;
}
// Cuánto se invierte, en qué y qué vuelve.
function investment(){
 const E=D.economics,I=E.investment,L=E.scenarios,mid=L[1];
 return `<div class="grid two invest"><article class="card"><p class="eyebrow">La inversión</p><div class="metric-number">${money(I.total)}</div><div class="uses">${I.items.map(([n,v])=>`<div><span>${esc(n)}</span><b>${money(v)}</b><i style="width:${Math.max(1,v/I.total*100)}%"></i></div>`).join('')}</div><p class="footnote">Después, el juego necesita ${money(I.monthlyFixed)} fijos por mes (${money(I.salaries)} son los sueldos de los 7) más los servidores.</p></article><article class="card"><p class="eyebrow">Lo que vuelve</p><table class="returns"><tr><th></th><th>Recupero</th><th>A los 3 años</th></tr>${L.map(x=>`<tr><td>${esc(x.name)}</td><td>${x.payback?'mes '+x.payback:'no se recupera'}</td><td>${x.perDollar?`US$&nbsp;${dec(x.perDollar)} por cada dólar`:`pierde ${money(-x.result)} por mes`}</td></tr>`).join('')}</table></article></div><div class="watch"><div><b>${num(mid.peak)}</b><span>a la vez en la hora pico, de los ${num(mid.players)} jugadores por mes del promedio</span></div><div><b>${thousands(mid.breakEven)}</b><span>jugadores por mes para no perder plata</span></div><div><b>${mid.conv.toLocaleString('es-AR')} %</b><span>de los jugadores paga algo en el mes</span></div><div><b>US$&nbsp;${dec(mid.arppu)}</b><span>por mes cada uno que paga: US$&nbsp;${dec(mid.arppuLatam)} en Latinoamérica (${mid.latam}&nbsp;%) y US$&nbsp;${dec(mid.arppuRest)} en el resto</span></div></div>`;
}
// A dónde va la inversión y, cada mes del promedio, lo que pagan los jugadores.
function costs(){
 const E=D.economics,I=E.investment,S=I.salary,M=E.month;
 return `<div class="grid two invest costs"><article class="card"><p class="eyebrow">Inversión inicial</p><div class="metric-number">${money(I.total)}</div><div class="uses">${I.detail.map(([n,v])=>`<div><span>${esc(n)}</span><b>${money(v)}</b><i style="width:${Math.max(1,v/I.total*100)}%"></i></div>`).join('')}</div><p class="footnote">Cada uno, el sueldo junior de lo que hace: ${money(S.gross)} brutos entre los 7 × ${dec(S.factor)} (aguinaldo, contribuciones y ART) = ${money(S.team)} por mes, durante los ${S.days} días de los 5 sprints.</p></article><article class="card"><p class="eyebrow">Cada mes, en el promedio</p><table class="returns spend"><tr><th>Ganancias en base al pago de los jugadores</th><th>${money(M.gross)}</th></tr>${M.items.map(([n,v])=>`<tr><td>${esc(n)}</td><td>${money(v)}</td></tr>`).join('')}<tr class="left"><td>Queda de ganancia</td><td>${money(M.left)}</td></tr></table></article></div>`;
}
// Los cuatro personajes bailando, a la misma escala y con los pies a la misma altura.
function dances(){
 return `<div class="bailes">${D.emotes.map(e=>`<figure class="baile"><div class="baile-escena"><img src="${esc(e.src)}" alt="${esc(e.name)} bailando ${esc(e.dance)}"></div><figcaption><strong>${esc(e.name)}</strong><span>${esc(e.dance)}</span></figcaption></figure>`).join('')}</div>`;
}
// Historias esperando prueba (LPR) al final de cada día, con los sprints de fondo, y la espera mediana de cada sprint.
function statusTime(){
 const T=D.statusTime,Q=T.queue,W=760,H=300,L=40,R=8,TOP=40,B=34,top=Math.ceil(Math.max(...Q.map(q=>q[1]))/30)*30;
 const bw=(W-L-R)/Q.length,y=v=>TOP+(H-TOP-B)*(1-v/top),at=d=>Q.findIndex(q=>q[0]===d),pico=Q.reduce((a,q,i)=>q[1]>Q[a][1]?i:a,0);
 const bandas=T.sprints.map((s,i)=>{const a=at(s.from),b=at(s.to);return `<rect class="banda${i%2?' par':''}" x="${L+a*bw}" y="${TOP-30}" width="${(b-a+1)*bw}" height="${H-B-TOP+30}"/><text class="banda-txt" x="${L+a*bw+8}" y="${TOP-12}">${esc(s.name.toUpperCase())}</text>`}).join('');
 const grilla=[0,top/3,2*top/3,top].map(v=>`<line class="grilla" x1="${L}" x2="${W-R}" y1="${y(v)}" y2="${y(v)}"/><text class="eje" x="${L-8}" y="${y(v)+5}" text-anchor="end">${v}</text>`).join('');
 const barras=Q.map((q,i)=>`<rect class="barra${i===pico?' pico':''}" x="${L+i*bw+2}" y="${y(q[1])}" width="${bw-4}" height="${y(0)-y(q[1])}" rx="2"><title>${q[0]}: ${q[1]} historias esperando prueba</title></rect>`).join('');
 const fechas=Q.map((q,i)=>i%7?'':`<text class="eje" x="${L+i*bw+bw/2}" y="${H-10}" text-anchor="middle">${q[0]}</text>`).join('');
 return `<div class="grid tiempo"><article class="card"><p class="eyebrow">Historias esperando prueba (LPR), cada día</p><svg viewBox="0 0 ${W} ${H}" role="img" aria-label="Historias esperando prueba por día: el pico es ${Q[pico][1]} el ${Q[pico][0]}">${bandas}${grilla}${barras}<text class="pico-txt" x="${L+pico*bw+bw/2}" y="${y(Q[pico][1])-8}" text-anchor="middle">${Q[pico][1]}</text>${fechas}</svg></article><article class="card"><p class="eyebrow">Espera mediana para probar</p><div class="esperas">${T.sprints.map(s=>`<div><span>${esc(s.name)}</span><b>${esc(s.wait)}</b>${s.note?`<small>${esc(s.note)}</small>`:''}</div>`).join('')}</div><p class="decision-tiempo">${esc(T.decision)}</p></article></div>`;
}
// Lo que sumó cada sprint, con una foto grande y lo nuevo en pocas palabras.
function progress(){
 return `<div class="grid avance">${D.progress.map(p=>`<article class="card"><p class="eyebrow">${esc(p.name)} · ${esc(p.dates)}</p>${p.title?`<h3>${esc(p.title)}</h3>`:''}<div class="avance-foto">${p.image?`<img src="${esc(p.image)}" alt="">`:''}</div>${p.items.length?`<ul>${p.items.map(it=>`<li>${esc(it.text)}${it.images?`<span class="avance-imgs${it.round?' redondas':''}">${it.images.map(src=>`<img src="${esc(src)}" alt="">`).join('')}</span>`:''}</li>`).join('')}</ul>`:''}</article>`).join('')}</div>`;
}
// Los conjuntos del Perfil (avatar, banner y marco del mismo estilo), uno abajo del otro como la cabecera del juego.
function sets(){
 const RZ=['Inicial','Común','Raro','Épico','Legendario'],COL=['#8E96A3','#9AA3B0','#5C7CFF','#B061FF','#FFC93C'];
 const PJ={astra:'Astra',pesado:'Pesado',guerrero:'Guerrero',sigiloso:'Sigiloso'},W=900,H=150,base='../menu/img/';
 return `<div class="conjuntos">${Marcos.conjuntos().map(({banner:b,avatar:a})=>{const d=Marcos.interior(b.id,H);return `<article class="conjunto"><div class="conjunto-banner" style="left:${d[0]}px;top:${d[1]}px;right:${d[2]}px;bottom:${d[3]}px"><img src="${base}banners/${b.id}.jpg" alt=""><i></i></div>${Marcos.html(b.id,0,0,W,H,false,base+'marcos/')}<span class="conjunto-avatar"><img src="${base}avatares/${a.id}.jpg" alt=""></span><strong>${esc(b.nombre)}</strong><small><b style="color:${COL[b.rareza]}">${RZ[b.rareza]}</b> · ${esc(b.estilo)}${PJ[b.personaje]?' · '+PJ[b.personaje]:''}</small></article>`}).join('')}</div>`;
}
// Los cuatro productos a la vista, cada uno con una imagen hecha con cosas del juego (avatares, banners y cápsulas).
function offer(){
 const R={1:'#9AA3B0',2:'#5C7CFF',3:'#B061FF',4:'#FFC93C'},av='../menu/img/avatares/';
 const visual={
  // Por rareza: avatar (común), banner (raro), baile (épico) y skin de arma (legendario).
  tienda:()=>`<div class="of-tienda">${[[av+'astra-celeste.jpg',1,'400',''],['../menu/img/banners/guerrero-synthwave.jpg',2,'800',''],['media/emotes/astra-shuffle.png',3,'1.600',' entero'],['media/armas/fusil.png',4,'3.200',' entero']].map(([src,r,p,c])=>`<span><img class="${c.trim()}" src="${src}" alt="" style="border-color:${R[r]}"><b style="color:${R[r]}">${p}</b></span>`).join('')}</div>`,
  pase:()=>`<div class="of-pase"><div class="of-premios">${[['astra-celeste',1],['guerrero-synthwave',2],['astra-acuarela',3],['astra-nouveau',4]].map(([id,r],i)=>`<img src="${av}${id}.jpg" alt="" style="left:${(i+1)*25}%;border-color:${R[r]}">`).join('')}</div><div class="of-niveles">${Array.from({length:40},(_,i)=>`<i${(i+1)%10===0?' class="hito"':''}></i>`).join('')}</div><small><span>Nivel 1</span><span>Nivel 40</span></small></div>`,
  capsulas:()=>`<div class="of-capsulas"><span class="of-capsula"><i></i></span><div><div class="of-barra">${[[55,1],[30,2],[12,3],[3,4]].map(([p,r])=>`<i style="width:${p}%;background:${R[r]}"></i>`).join('')}</div><small>${[[55,'Común',1],[30,'Raro',2],[12,'Épico',3],[3,'Legendario',4]].map(([p,n,r])=>`<span><b style="color:${R[r]}">${p} %</b> ${n}</span>`).join('')}</small></div></div>`,
  cristales:()=>`<div class="of-cristales">${[['500','4,99',.7],['1.100','9,99',.85],['2.300','19,99',1],['6.000','49,99',1.2]].map(([c,p,k])=>`<span><i style="--k:${k}"></i><b>${c}</b><small>US$ ${p}</small></span>`).join('')}</div>`
 };
 return `<div class="grid oferta">${D.business.map(b=>`<article class="card"><div class="of-visual">${visual[b.visual]()}</div><p class="eyebrow">${esc(b.name)}</p><h3>${esc(b.headline)}</h3><p>${esc(b.text)}</p><span class="of-precio">${esc(b.model)}</span><p class="footnote">${esc(b.note)}</p></article>`).join('')}</div><p class="lead">Jugar es gratis. Nada de lo que se vende da ventaja en la partida.</p>`;
}
const tag=(text,pending=false)=>`<span class="tag ${pending?'pending':''}">${esc(text)}</span>`;
const card=(title,text)=>`<article class="card"><h3>${esc(title)}</h3><p>${esc(text)}</p></article>`;
// La parte de la presentación (Juego, Equipo, Financiación) va antes del nombre del bloque.
const eyebrow=s=>`${s.section&&s.section!==s.label?esc(s.section)+' · ':''}${esc(s.label)}`;
const head=s=>`<div class="head"><p class="eyebrow">${eyebrow(s)}</p><h2>${esc(s.title)}</h2></div>`;
// Avance de los tres sprints: historias terminadas y pruebas que pasan. Un sprint todavía sin datos muestra rayas.
function metrics(){
 const pct=(a,b)=>(100*a/b).toLocaleString('es-AR',{maximumFractionDigits:1})+' %';
 const bar=(parts,total,label)=>`<div class="bars" role="img" aria-label="${label}">${parts.map(([v,c])=>`<span style="width:${v/Math.max(1,total)*100}%;background:${c}"></span>`).join('')}<span style="flex:1;background:var(--line)"></span></div>`;
 // Lo que queda fuera de Done o sin pasar; los ceros no se muestran.
 const legend=parts=>{const l=parts.filter(([v])=>v>0);return l.length?`<div class="legend">${l.map(([v,t])=>`<span>${v} ${t}</span>`).join('')}</div>`:''};
 const sprint=m=>{
  if(['planned','done','review','todo','tests','passed','failed','blocked','unrun'].some(k=>m[k]==null))
   return `<article class="card sprint"><p class="eyebrow">${esc(m.name)}</p><div class="metric-number">—</div><p>historias en Done</p>${bar([],1,'Sin datos')}<div class="metric-number">—</div><p>pruebas que pasan</p>${bar([],1,'Sin datos')}</article>`;
  const run=m.tests-m.unrun;
  return `<article class="card sprint"><p class="eyebrow">${esc(m.name)}</p><div class="metric-number">${pct(m.done,m.planned)}</div><p>${m.done} de ${m.planned} historias en Done</p>${bar([[m.done,'var(--green)'],[m.review,'var(--accent)']],m.planned,`${m.done} en Done, ${m.review} en revisión, ${m.todo} sin empezar`)}${legend([[m.review,'en revisión'],[m.todo,'sin empezar']])}<div class="metric-number">${pct(m.passed,run)}</div><p>${m.passed} de ${run} pruebas pasan</p>${bar([[m.passed,'var(--green)'],[m.failed,'var(--red)'],[m.blocked,'var(--accent)']],m.tests,`${m.passed} pasan, ${m.failed} fallan, ${m.blocked} bloqueadas, ${m.unrun} sin ejecutar`)}${legend([[m.failed,'fallan'],[m.blocked,m.blocked===1?'bloqueada':'bloqueadas'],[m.unrun,'sin ejecutar']])}</article>`;
 };
 return `<div class="grid">${['s1','s2','s3'].map(k=>sprint(D.metrics[k])).join('')}</div>`;
}
function arsenal(){
 const w=window.ARSENAL.find(x=>x.id===weaponId)||window.ARSENAL[0],t=w.tramos[0];
 // Contra 100 de vida y 50 de escudo, con el daño del primer tramo (la escopeta, con todos los perdigones).
 const tiros=Math.ceil(150/(t.cuerpo*(w.perdigones||1)));
 return `<div class="row" style="margin-bottom:20px"><label for="weaponSelect" class="sr">Elegir arma</label><select id="weaponSelect">${window.ARSENAL.map(x=>`<option value="${esc(x.id)}" ${x.id===w.id?'selected':''}>${esc(x.alias)} · ${esc(x.nombre)}</option>`).join('')}</select></div><div class="weapon"><div class="weapon-art"><img src="media/armas/${esc(w.id)}.png" alt="${esc(w.alias)}"></div><div><p class="eyebrow">${esc(w.modo)}</p><h3>${esc(w.alias)}</h3><div class="stats"><div><b>${w.precio.toLocaleString('es-AR')}</b><span>monedas de partida</span></div><div><b>${w.cargador}</b><span>balas / cargador</span></div><div><b>${w.recarga.toLocaleString('es-AR')} s</b><span>recarga</span></div><div><b>${t.cabeza}</b><span>daño · cabeza</span></div><div><b>${t.cuerpo}</b><span>daño · cuerpo</span></div><div><b>${tiros}</b><span>${tiros===1?'tiro':'tiros'} al cuerpo con escudo completo</span></div></div></div></div>`;
}
function content(s){
 switch(s.id){
 case 'inicio':return `<div class="hero"><p class="eyebrow">Laboratorio de Construcción de Software · Sprint 3</p><img class="hero-logo" src="media/logo.png" alt="Project Riftwalker"><h1>Del equipo<br>al juego.</h1><p class="lead">Decisiones, evidencia y una experiencia para jugar.</p></div>`;
 case 'trailer':{const t=D.trailer;return `<div class="head row trailer-head"><p class="eyebrow">${eyebrow(s)}</p>${t.src?'<button class="primary" data-trailer>▶ Pantalla completa</button>':''}</div><div class="trailer">${t.src?`<video id="trailerVideo" src="${esc(t.src)}"${t.poster?` poster="${esc(t.poster)}"`:''} controls playsinline preload="metadata"></video>`:'<strong>Tráiler</strong>'}</div>`}
 case 'metricas':return head(s)+metrics();
 case 'menus':return head(s)+`<div class="grid"><button class="demo-card" data-demo="tienda"><strong>01 / Tienda</strong><span>Elegir, comparar y comprar.</span><em>Abrir demo →</em></button><button class="demo-card" data-demo="modos"><strong>02 / Modos</strong><span>Tres andenes. Una elección.</span><em>Abrir demo →</em></button><button class="demo-card" data-demo="hud"><strong>03 / HUD</strong><span>Información durante el combate.</span><em>Abrir demo →</em></button></div>`;
 case 'conjuntos':return head(s)+sets();
 case 'emotes':return head(s)+dances();
 case 'tiempo':return head(s)+statusTime();
 case 'arsenal':return head(s)+arsenal();
 case 'juego':return head(s)+`<button class="menu-demo" data-demo="menu"><img src="../menu/img/fondos/fondo-01.jpg" alt=""><span class="menu-demo-texto"><strong>Abrir el menú</strong><span>Entrá al Perfil desde tu credencial y armá tu combinación: 19 avatares, 19 banners, 70 títulos y 19 marcos. Son más de 500.000 combinaciones. Y en la Estación del Rift, abrí cápsulas.</span><em>Abrir demo →</em></span></button>`;
 case 'negocio':return head(s)+offer();
 case 'escenarios':return head(s)+scenarios();
 case 'costos':return head(s)+costs();
 case 'avance':return head(s)+progress();
 case 'inversion':return head(s)+investment();
 case 'cierre':return `<div class="hero"><p class="eyebrow">Laboratorio de Construcción de Software · Sprint 3</p><img class="hero-logo" src="media/logo.png" alt="Project Riftwalker"><h1>¡Gracias!</h1><p class="lead">¿Preguntas?</p></div>`;
 }
}
function render(focus=false){const s=D.slides[current];$('#slide').innerHTML=content(s);$('#position').textContent=`${current+1} / ${D.slides.length}`;$('#prev').disabled=current===0;$('#next').disabled=current===D.slides.length-1;$('#progress').style.width=`${(current+1)/D.slides.length*100}%`;$('#speaker').textContent=s.speaker||D.speaker;$('#announcement').textContent=`${current+1} de ${D.slides.length}: ${s.title}`;history.replaceState(null,'','#'+s.id);if(focus){$('#slide').focus({preventScroll:true});scrollTo(0,0)}}
function go(n){current=Math.max(0,Math.min(D.slides.length-1,n));render(true)}
$('#edition').textContent=D.edition;
$('#indexGrid').innerHTML=D.slides.map((s,i)=>`${s.section!==D.slides[i-1]?.section?`<h3 class="index-section">${esc(s.section)}</h3>`:''}<button data-go="${i}">${String(i+1).padStart(2,'0')} / ${esc(s.label)}<small>${s.minutes} min · ${esc(s.title)}</small></button>`).join('');
$('#prev').onclick=()=>go(current-1);$('#next').onclick=()=>go(current+1);
$('#indexButton').onclick=()=>$('#indexDialog').showModal();
$('#contrast').onclick=()=>{const on=document.body.classList.toggle('projector');$('#contrast').setAttribute('aria-pressed',on)};
$('#fullscreen').onclick=async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen()}catch{$('#announcement').textContent='El navegador no permitió pantalla completa. Podés usar F11.'}};
// El menú, el perfil y la Estación del Rift son una página aparte (/menu); las otras demos son maquetas de la guía.
const demoSrc=id=>id==='menu'?'../menu/index.html':`../index.html?demo=${id}`;
let demo='';function openDemo(id){demo=id;$('#demoTitle').textContent={tienda:'Tienda de armas',modos:'Selección de modo',hud:'HUD de combate',menu:'Menú, perfil y cápsulas'}[id];$('#demoSub').textContent=id==='menu'?'El menú, el perfil y la Estación del Rift del juego, en la página':'Maqueta interactiva de la guía';$('#demoFrame').src=demoSrc(id);$('#demoDialog').showModal()}
$('#resetDemo').onclick=()=>{$('#demoFrame').src=demoSrc(demo)};
$('#demoDialog').addEventListener('close',()=>{$('#demoFrame').src='about:blank'});
$('#clipDialog').addEventListener('close',()=>{$('#video').pause();$('#video').removeAttribute('src');$('#video').load()});
document.addEventListener('click',e=>{const b=e.target.closest('button');if(!b)return;const d=b.dataset;
 if(d.close)$('#'+d.close).close();
 if(d.go!==undefined){$('#indexDialog').close();go(+d.go)}

 if(d.demo)openDemo(d.demo);
 if(d.trailer!==undefined){const v=$('#trailerVideo');if(v){v.requestFullscreen?.().catch(()=>{});v.play().catch(()=>{})}}
});
document.addEventListener('change',e=>{if(e.target.id==='weaponSelect'){weaponId=e.target.value;render()}});
document.addEventListener('keydown',e=>{if(document.querySelector('dialog[open]')||document.fullscreenElement?.tagName==='VIDEO'||e.target.closest('input,select,textarea,button,a,video'))return;
 if(e.key==='ArrowRight'||e.key==='PageDown'){e.preventDefault();go(current+1)}if(e.key==='ArrowLeft'||e.key==='PageUp'){e.preventDefault();go(current-1)}if(e.key.toLowerCase()==='f')$('#fullscreen').click();
});
// Child demos use their own keyboard; only an explicit return message closes them.
addEventListener('message',e=>{if(e.origin===location.origin&&e.source===$('#demoFrame').contentWindow&&e.data==='riftwalker:close-demo')$('#demoDialog').close()});
let running=false,elapsed=0,last=0;const duration=D.slides.reduce((sum,s)=>sum+s.minutes*60,0);
function clock(){const total=elapsed+(running?(performance.now()-last)/1000:0),remaining=duration-total,seconds=Math.ceil(Math.abs(remaining));$('#timer').textContent=`${remaining<0?'+':''}${String(Math.floor(seconds/60)).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;$('#timer').classList.toggle('over',remaining<0)}
$('#timerToggle').onclick=()=>{if(running)elapsed+=(performance.now()-last)/1000;else last=performance.now();running=!running;$('#timerToggle').textContent=running?'Ⅱ Pausar':'▶ Tiempo';clock()};
$('#timerReset').onclick=()=>{elapsed=0;last=performance.now();clock()};setInterval(clock,200);
const initial=D.slides.findIndex(s=>'#'+s.id===location.hash);current=initial<0?0:initial;render();clock();

addEventListener('hashchange',()=>{const i=D.slides.findIndex(s=>'#'+s.id===location.hash);if(i>=0&&i!==current)go(i)});
