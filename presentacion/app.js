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
 return `<div class="grid">${L.map(x=>`<article class="card scenario"><p class="eyebrow">${esc(x.name)}</p><div class="result ${x.afterTax>=0?'pos':'neg'}">${money(x.afterTax,true)}<small> por mes</small></div><dl class="facts"><div><dt>Jugadores por mes</dt><dd>${num(x.players)}</dd></div><div><dt>A la vez en la hora pico</dt><dd>${num(x.peak)}</dd></div><div><dt>Pagan</dt><dd>${num(x.payers)} · ${x.conv.toLocaleString('es-AR')} %</dd></div><div><dt>Gasto mensual por pagador</dt><dd>US$&nbsp;${dec(x.arppu)}</dd></div><div><dt>Ganancias en base al pago de los jugadores</dt><dd>${money(x.gross)}</dd></div><div><dt>Llega a esos jugadores</dt><dd>en ${x.ramp} meses</dd></div><div><dt>Recupero</dt><dd>${x.payback?'mes '+x.payback:'no se recupera'}</dd></div></dl></article>`).join('')}</div><p class="footnote">Los jugadores crecen mes a mes hasta ese número y después se mantienen: es plata de todos los meses, no de un lanzamiento. El promedio deja de perder plata desde ${thousands(mid.breakEven)} jugadores por mes.</p>`;
}
// Cuánto se invierte, en qué y qué vuelve.
function investment(){
 const E=D.economics,I=E.investment,L=E.scenarios,mid=L[1];
 return `<div class="grid two invest"><article class="card"><p class="eyebrow">La inversión</p><div class="metric-number">${money(I.total)}</div><div class="uses">${I.items.map(([n,v])=>`<div><span>${esc(n)}</span><b>${money(v)}</b><i style="width:${Math.max(1,v/I.total*100)}%"></i></div>`).join('')}</div><p class="footnote">Después, el juego necesita ${money(I.monthlyFixed)} fijos por mes (${money(I.salaries)} son los sueldos de los 7) más los servidores.</p></article><article class="card"><p class="eyebrow">Lo que vuelve</p><table class="returns"><tr><th></th><th>Recupero</th><th>A los 3 años</th></tr>${L.map(x=>`<tr><td>${esc(x.name)}</td><td>${x.payback?'mes '+x.payback:'no se recupera'}</td><td>${x.perDollar?`US$&nbsp;${dec(x.perDollar)} por cada dólar`:`pierde ${money(-x.result)} por mes`}</td></tr>`).join('')}</table></article></div><div class="watch"><div><b>${num(mid.peak)}</b><span>a la vez en la hora pico, de los ${num(mid.players)} jugadores por mes del promedio</span></div><div><b>${thousands(mid.breakEven)}</b><span>jugadores por mes para no perder plata</span></div><div><b>${mid.conv.toLocaleString('es-AR')} %</b><span>de los jugadores paga algo en el mes</span></div><div><b>US$&nbsp;${dec(mid.arppu)}</b><span>por mes cada uno que paga: US$&nbsp;${dec(mid.arppuLatam)} en Latinoamérica (${mid.latam}&nbsp;%) y US$&nbsp;${dec(mid.arppuRest)} en el resto</span></div></div>`;
}
// f(x): lo que queda por mes según los jugadores que pagan, con las zonas (pierde, se sostiene, va bien) y los escenarios.
function payers(){
 const F=D.economics.fx,W=900,H=390,L=84,R=24,T=40,B=56,xmax=4000,ymin=-10000,ymax=10000;
 const X=x=>L+(W-L-R)*x/xmax,Y=v=>T+(H-T-B)*(ymax-v)/(ymax-ymin);
 const plata=v=>(v<0?'−':v>0?'+':'')+'US$ '+num(Math.abs(Math.round(v)));
 const eje=v=>v===0?'US$ 0':(v<0?'−':'+')+num(Math.abs(v)/1000)+' mil';
 const zona=(a,b,c,t)=>`<rect class="fx-zona ${c}" x="${X(a)}" y="${T}" width="${X(b)-X(a)}" height="${H-T-B}"/><text class="fx-zona-txt ${c}" x="${(X(a)+X(b))/2}" y="${T+24}" text-anchor="middle">${t}</text>`;
 const ys=[-10000,-5000,0,5000,10000].map(v=>`<line class="grilla${v===0?' cero':''}" x1="${L}" x2="${W-R}" y1="${Y(v)}" y2="${Y(v)}"/><text class="eje" x="${L-12}" y="${Y(v)+5}" text-anchor="end">${eje(v)}</text>`).join('');
 const xs=[0,500,1000,1500,2000,2500,3000,3500,4000].map(v=>`<line class="tick" x1="${X(v)}" x2="${X(v)}" y1="${H-B}" y2="${H-B+6}"/><text class="eje" x="${X(v)}" y="${H-B+26}" text-anchor="middle">${num(v)}</text>`).join('');
 const umbral=(x,t)=>`<line class="fx-umbral" x1="${X(x)}" x2="${X(x)}" y1="${T}" y2="${H-B}"/><text class="fx-umbral-txt" x="${X(x)}" y="${T-10}" text-anchor="middle">${t}</text>`;
 const linea=`<polyline class="fx-linea" points="${F.points.map(([x,v])=>X(x).toFixed(1)+','+Y(v).toFixed(1)).join(' ')}"/>`;
 const punto=(x,v,c)=>`<circle class="fx-punto ${c}" cx="${X(x)}" cy="${Y(v)}" r="7"/>`;
 const e=F.example,m=F.average;
 const svg=`<svg class="fx-chart" id="fxChart" viewBox="0 0 ${W} ${H}" role="img" aria-label="Lo que queda por mes según los jugadores que pagan, de ${plata(F.points[0][1])} con 0 a ${plata(F.points[F.points.length-1][1])} con ${num(xmax)}: pierde hasta ${num(Math.round(F.breakEven))} pagadores, se sostiene hasta ${num(Math.round(F.full))} y va bien desde ahí">
  ${zona(0,F.breakEven,'mal','Pierde')}${zona(F.breakEven,F.full,'justo','Se sostiene')}${zona(F.full,xmax,'bien','Va bien')}${ys}${xs}
  ${umbral(F.breakEven,num(Math.round(F.breakEven)))}${umbral(F.full,num(Math.round(F.full)))}
  <text class="eje titulo" x="${(L+W-R)/2}" y="${H-6}" text-anchor="middle">Jugadores que pagan en el mes</text>
  ${linea}${punto(e.payers,e.result,'ej')}${punto(F.breakEven,0,'cero')}${punto(m.payers,m.result,'pos')}
  <text class="fx-txt" x="${X(e.payers)+14}" y="${Y(e.result)+28}">20.000 jugadores al 1 %: ${num(e.payers)} pagan · ${plata(e.result)}</text>
  <text class="fx-txt" x="${X(m.payers)}" y="${Y(m.result)-18}" text-anchor="middle">Promedio: ${num(m.payers)} pagan · ${plata(m.result)}</text>
  <text class="fx-nota" x="${X(2490)}" y="${Y(2400)}" text-anchor="middle">Unity Pro</text><g class="fx-hover" id="fxHover" visibility="hidden"><line id="fxHoverLine" y1="${T}" y2="${H-B}"/><circle id="fxHoverDot" r="6"/><rect id="fxHoverBox" rx="6" height="30"/><text id="fxHoverTxt"></text></g>
  <rect class="fx-hit" x="${L}" y="${T}" width="${W-L-R}" height="${H-T-B}" data-fx="${L},${W-R},${xmax}"/></svg>`;
 const fuera=F.outside.map(o=>`${o.name}: ${num(o.payers)} pagan, a US$&nbsp;${dec(o.arppu)} cada uno: ${plata(o.result)} por mes`).join('. ');
 return `<div class="fx">${svg}<div class="fx-zonas"><div class="mal"><b>Va mal</b><span>menos de ${num(Math.round(F.breakEven))} pagadores: pierde plata</span></div><div class="justo"><b>Se sostiene</b><span>de ${num(Math.round(F.breakEven))} a ${num(Math.round(F.full))}: no pierde con el equipo de hoy</span></div><div class="bien"><b>Va bien</b><span>${num(Math.round(F.full))} o más, 3 meses seguidos: también se paga el equipo completo</span></div><p class="footnote">Lo que queda después de Steam, la retención de EE.UU., los gastos y el impuesto, con el gasto por pagador del promedio (US$&nbsp;${dec(F.arppu)}). Desde unos 2.490 pagadores se pasan los US$&nbsp;200.000 por año y hay que pagar Unity Pro para los 7 (US$&nbsp;1.348 por mes): de ahí la bajada. Los escalones chicos son cambios de plan de Photon. ${fuera}.</p></div></div>`;
}
// Valor de la f(x) bajo el mouse: la línea vertical, el punto sobre la curva y el número.
document.addEventListener('pointermove',e=>{
 const hit=e.target.closest&&e.target.closest('.fx-hit'),g=document.getElementById('fxHover');
 if(!g)return;if(!hit){g.setAttribute('visibility','hidden');return}
 const svg=hit.ownerSVGElement,pt=svg.createSVGPoint();pt.x=e.clientX;pt.y=e.clientY;const p=pt.matrixTransform(svg.getScreenCTM().inverse());
 const [l,r,xmax]=hit.dataset.fx.split(',').map(Number),F=D.economics.fx,x=Math.max(0,Math.min(xmax,(p.x-l)/(r-l)*xmax));
 const [px,pv]=F.points.reduce((a,q)=>Math.abs(q[0]-x)<Math.abs(a[0]-x)?q:a);
 const W=900,H=390,L=84,R=24,T=40,B=56,X=v=>L+(W-L-R)*v/xmax,Y=v=>T+(H-T-B)*(10000-v)/20000;
 const t=`${num(px)} pagan · ${(pv<0?'−':pv>0?'+':'')}US$ ${num(Math.abs(pv))} por mes`;
 const txt=document.getElementById('fxHoverTxt'),box=document.getElementById('fxHoverBox');
 document.getElementById('fxHoverLine').setAttribute('x1',X(px));document.getElementById('fxHoverLine').setAttribute('x2',X(px));
 const dot=document.getElementById('fxHoverDot');dot.setAttribute('cx',X(px));dot.setAttribute('cy',Y(pv));
 txt.textContent=t;const w=t.length*9+20,bx=Math.min(Math.max(X(px)-w/2,L),W-R-w),by=Math.max(T+34,Y(pv)-48);
 box.setAttribute('x',bx);box.setAttribute('y',by);box.setAttribute('width',w);txt.setAttribute('x',bx+10);txt.setAttribute('y',by+21);
 g.setAttribute('visibility','visible');
});
// Si no funciona: los puntos de control (en jugadores que pagan) y lo máximo que pierde el inversor.
function guard(){
 const G=D.economics.guard;
 return `<div class="escala-linea guard"><div class="parada"><i></i><p class="eyebrow">Lanzamiento</p><b>${money(G.firstTranche)}</b><span>la mitad de la publicidad; la otra mitad, solo si en el mes 3 hay ${num(G.checkpoint3)} pagadores o más</span></div><div class="parada senal"><i></i><p class="eyebrow">Mes 3</p><b>&lt;&nbsp;${num(G.checkpoint3)}</b><span>pagadores: mantenimiento con ${G.keep} de los 7 y sin publicidad. Los gastos bajan a ${money(G.maintenance)} por mes</span></div><div class="parada corte"><i></i><p class="eyebrow">Mes 6</p><b>&lt;&nbsp;${num(G.checkpoint6)}</b><span>pagadores: se cierra</span></div></div><div class="grid two guard-fin"><article class="card scenario"><p class="eyebrow">Pesimista, con el plan</p><div class="result neg">${money(G.worst,true)}<small>lo máximo que pierde el inversor: la inversión y 6 meses</small></div></article><article class="card scenario"><p class="eyebrow">Pesimista, sin el plan</p><div class="result neg">${money(G.without,true)}<small>a los 3 años, siguiendo igual</small></div></article></div><p class="lead">En el promedio y el optimista no cambia nada. Y el inversor cobra primero: hasta recuperar lo que puso, la ganancia es suya.</p>`;
}
// Si funciona, los 7 a tiempo completo: tres paradas (hoy, la señal y tiempo completo) y lo que queda con el equipo completo.
function scale(){
 const S=D.economics.scale,pct=n=>Math.round(n*100)+' %',[a,b]=S.scenarios;
 return `<div class="escala-linea"><div class="parada"><i></i><p class="eyebrow">Hoy</p><b>${num(Math.round(S.hours))} h</b><span>por sprint entre los 7: el ${pct(S.hours/S.fullHours)} de un tiempo completo</span><strong>${money(S.salaries)} por mes en sueldos</strong></div><div class="parada senal"><i></i><p class="eyebrow">La señal</p><b>${thousands(S.breakEven)}</b><span>jugadores por mes, con la inversión ya recuperada</span><strong>${esc(a.name)}: mes ${a.payback} · ${esc(b.name)}: mes ${b.payback}</strong></div><div class="parada completo"><i></i><p class="eyebrow">Tiempo completo</p><b>${num(S.fullHours)} h</b><span>por sprint: los 7 a 80 h, ${pct(S.fullHours/S.hours-1)} más</span><strong>${money(S.fullSalaries)} por mes en sueldos</strong></div></div><div class="grid escala-fin">${S.scenarios.map(x=>`<article class="card scenario"><p class="eyebrow">${esc(x.name)}</p><div class="result pos">${money(x.grown,true)}<small>por mes con ${S.growth} % más jugadores · hoy ${money(x.now)}</small></div></article>`).join('')}<article class="card"><p class="eyebrow">Más contenido, más jugadores</p><p>${esc(S.service)} <b>Desde un ${S.payoff} % más, el inversor gana más que hoy.</b></p></article></div>`;
}
// A dónde va la inversión y, cada mes del promedio, lo que pagan los jugadores.
function costs(){
 const E=D.economics,I=E.investment,S=I.salary,M=E.month;
 // Lo que cuesta cada hora (con aguinaldo, contribuciones y ART) y el sueldo bruto de un mes a tiempo completo con esa hora
 // (80 h cada 14 días, como en la guía).
 const hora=Math.round(S.total/S.hours*10)/10,mes=Math.round(hora*80*30.4/14/S.factor/10)*10;
 return `<div class="grid two invest costs"><article class="card"><p class="eyebrow">Inversión inicial</p><div class="metric-number">${money(I.total)}</div><div class="uses">${I.detail.map(([n,v])=>`<div><span>${esc(n)}</span><b>${money(v)}</b><i style="width:${Math.max(1,v/I.total*100)}%"></i></div>`).join('')}</div><p class="footnote">Sueldos junior de Argentina (mediana de Sysarmy 2026.1) según lo que hace cada uno y sus horas: unos US$&nbsp;${dec(hora)} por hora, ${money(mes)} brutos por mes a tiempo completo. <b>En blanco:</b> el × ${S.factor.toLocaleString('es-AR')} son aguinaldo, contribuciones patronales y ART.</p></article><article class="card"><p class="eyebrow">Cada mes, en el promedio</p><table class="returns spend"><tr><th>Ganancias en base al pago de los jugadores</th><th>${money(M.gross)}</th></tr>${M.items.map(([n,v])=>`<tr><td>${esc(n)}</td><td>${money(v)}</td></tr>`).join('')}<tr class="left"><td>Queda de ganancia</td><td>${money(M.left)}</td></tr></table></article></div>`;
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
 return `<div class="grid avance">${D.progress.map(p=>`<article class="card"><p class="eyebrow">${esc(p.name)} · ${esc(p.dates)}</p>${p.title?`<h3>${esc(p.title)}</h3>`:''}<div class="avance-foto">${p.image?`<img src="${esc(p.image)}" alt="">`:''}</div>${p.items.length?`<ul>${p.items.map(it=>`<li>${esc(it.text)}${it.images?`<span class="avance-imgs${it.round?' redondas':''}${it.icons?' iconos':''}">${it.images.map(src=>`<img src="${esc(src)}" alt="">`).join('')}</span>`:''}</li>`).join('')}</ul>`:''}</article>`).join('')}</div>`;
}
// La historia en tres momentos, con cuadros del tráiler, y abajo los modos que la representan con su mapa.
function story(){
 const H=D.story;
 return `<div class="historia"><div class="relato">${H.chapters.map((c,i)=>`<figure class="capitulo"><img src="${esc(c.image)}" alt=""><figcaption><b>${String(i+1).padStart(2,'0')}</b><strong>${esc(c.title)}</strong></figcaption></figure>`).join('')}</div><p class="facetas">${esc(H.facets)}</p><div class="modos-historia">${H.modes.map(m=>`<article class="modo-historia${m.map?'':' corto'}"><img src="${esc(m.image)}" alt=""><div><strong>${esc(m.name)}</strong><span>${esc(m.facet)}</span>${m.map?`<small>Mapa: ${esc(m.map)}</small>`:''}</div></article>`).join('')}</div></div>`;
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
  return `<article class="card sprint"><p class="eyebrow">${esc(m.name)}</p><div class="metric-number">${pct(m.done,m.planned)}</div><p>${m.done} de ${m.planned} historias en Done</p>${bar([[m.done,'var(--green)'],[m.review,'var(--accent)'],[m.stuck||0,'var(--red)']],m.planned,`${m.done} en Done, ${m.review} en revisión, ${m.stuck||0} bloqueadas, ${m.todo} sin empezar`)}${legend([[m.review,'en revisión'],[m.stuck||0,m.stuck===1?'bloqueada':'bloqueadas'],[m.todo,'sin empezar']])}<div class="metric-number">${pct(m.passed,run)}</div><p>${m.passed} de ${run} pruebas pasan</p>${bar([[m.passed,'var(--green)'],[m.failed,'var(--red)'],[m.blocked,'var(--accent)']],m.tests,`${m.passed} pasan, ${m.failed} fallan, ${m.blocked} bloqueadas, ${m.unrun} sin ejecutar`)}${legend([[m.failed,m.failed===1?'falla':'fallan'],[m.blocked,m.blocked===1?'bloqueada':'bloqueadas'],[m.unrun,'sin ejecutar']])}</article>`;
 };
 return `<div class="grid metricas">${['s1','s2','s3','s4'].map(k=>sprint(D.metrics[k])).join('')}</div>`;
}
function arsenal(){
 const w=window.ARSENAL.find(x=>x.id===weaponId)||window.ARSENAL[0],t=w.tramos[0];
 // Contra 100 de vida y 50 de escudo, con el daño del primer tramo (la escopeta, con todos los perdigones).
 const tiros=Math.ceil(150/(t.cuerpo*(w.perdigones||1)));
 return `<div class="row" style="margin-bottom:20px"><label for="weaponSelect" class="sr">Elegir arma</label><select id="weaponSelect">${window.ARSENAL.map(x=>`<option value="${esc(x.id)}" ${x.id===w.id?'selected':''}>${esc(x.alias)} · ${esc(x.nombre)}</option>`).join('')}</select></div><div class="weapon"><div class="weapon-art"><img src="media/armas/${esc(w.id)}.png" alt="${esc(w.alias)}"></div><div><p class="eyebrow">${esc(w.modo)}</p><h3>${esc(w.alias)}</h3><div class="stats"><div><b>${w.precio.toLocaleString('es-AR')}</b><span>monedas de partida</span></div><div><b>${w.cargador}</b><span>balas / cargador</span></div><div><b>${w.recarga.toLocaleString('es-AR')} s</b><span>recarga</span></div><div><b>${t.cabeza}</b><span>daño · cabeza</span></div><div><b>${t.cuerpo}</b><span>daño · cuerpo</span></div><div><b>${tiros}</b><span>${tiros===1?'tiro':'tiros'} al cuerpo con escudo completo</span></div></div></div></div>`;
}
function content(s){
 switch(s.id){
 case 'inicio':return `<div class="hero"><p class="eyebrow">Laboratorio de Construcción de Software · Sprint 4</p><img class="hero-logo" src="media/logo.png" alt="Project Riftwalker"><h1>Del equipo<br>al juego.</h1><p class="lead">Decisiones, evidencia y una experiencia para jugar.</p></div>`;
 case 'trailer':{const t=D.trailer;return `<div class="head row trailer-head"><p class="eyebrow">${eyebrow(s)}</p>${t.src?'<button class="primary" data-trailer>▶ Pantalla completa</button>':''}</div><div class="trailer">${t.src?`<video id="trailerVideo" src="${esc(t.src)}"${t.poster?` poster="${esc(t.poster)}"`:''} controls controlslist="nofullscreen" playsinline preload="metadata"></video><button class="trailer-exit" data-exit-trailer>Volver al recorrido</button>`:'<strong>Tráiler</strong>'}</div>`}
 case 'historia':return head(s)+story();
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
 case 'fx':return head(s)+payers();
 case 'proteccion':return head(s)+guard();
 case 'avance':return head(s)+progress();
 case 'inversion':return head(s)+investment();
 case 'escala':return head(s)+scale();
 case 'cierre':return questionsScreen();
 }
}
function closeTrailer(){document.body.classList.remove('trailer-expanded');$('#trailerVideo')?.pause();$('#slide').focus({preventScroll:true})}
document.addEventListener('fullscreenchange',()=>{if(!document.fullscreenElement&&document.body.classList.contains('trailer-expanded'))closeTrailer()});
const questionTopics = [
  ['Nuevo mapa', 'La UNGS en Riftwalker', 'mapa'],
  ['Steam', 'Project Riftwalker en Steam', 'steam'],
  ['Idiomas', 'Idiomas', 'idiomas'],
  ['Logros', 'Logros', 'logros'],
  ['Miras', 'Elegí tu mira', 'miras'],
  ['Modo Invasión', 'Modo Invasión · Roguelike de oleadas', 'invasion'],
  ['Cuenta', 'Tu cuenta Riftwalker', 'cuenta']
];
const proofImages = '../pruebas/idiomas/2026-11-08/';
const invasionView = proofImages + 'US-348_CA1_Invasion_HUD_y_oleada.png';
let campusPhotos = Array.from({ length: 11 }, (_, index) => `media/campus/campus-${index + 1}.jpg`);
let campusPhoto = 0;
function mapPhoto() {
  const picture = $('#campusPicture');
  picture.src = campusPhotos[campusPhoto];
  picture.alt = 'Vista del mapa de la UNGS ' + (campusPhoto + 1);
  $('#photoCount').textContent = (campusPhoto + 1) + ' / ' + campusPhotos.length;
  for (const button of document.querySelectorAll('[data-photo]')) button.disabled = campusPhotos.length < 2;
}
function questionVisual(key) {
  switch (key) {
    case 'mapa': return `<img id="campusPicture" class="qa-picture" alt=""><div class="qa-actions"><button data-photo="-1" aria-label="Foto anterior">←</button><output id="photoCount"></output><button data-photo="1" aria-label="Foto siguiente">→</button><label class="photo-picker">Elegir fotos del mapa<input type="file" id="mapFiles" accept="image/*" multiple></label></div>`;
    case 'steam': return '<iframe class="steam-showcase" title="Ficha de Steam" src="../steam/"></iframe>';
    case 'logros': return `<div class="qa-run"><figure><img src="media/logros-progreso.png" alt="Pestaña Logros: 7 de 25 logros obtenidos, con el avance de cada uno y el porcentaje de jugadores que lo tiene"><figcaption>Tu progreso y el porcentaje de jugadores que tiene cada logro</figcaption></figure><figure><img src="media/logros-ranking.png" alt="Pestaña Ranking: ranking global del minijuego por cometas destruidos"><figcaption>Ranking global por modo y categoría</figcaption></figure></div><div class="qa-actions"><strong>25 logros</strong><span>9 de cometas</span><span>16 de combate</span></div>`;
    case 'idiomas': {
      const flag = (code,name) => `<figure><img alt="" src="https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/svg/${code}.svg"><figcaption>${name}</figcaption></figure>`;
      return `<div class="qa-languages"><h3>En el juego y el launcher</h3><div>${flag('1f1e6-1f1f7','Español')}${flag('1f1ec-1f1e7','English')}</div><h3>Previstos para el Sprint 5</h3><div>${flag('1f1e7-1f1f7','Português')}${flag('1f1eb-1f1f7','Français')}${flag('1f1ee-1f1f9','Italiano')}</div></div>`;
    }
    case 'miras': return '<img class="qa-picture" style="height:auto;max-height:70dvh;object-fit:contain" src="media/mira-opciones.png" alt="Opciones de mira con forma, tamaño, color y vista previa sobre fondo claro y oscuro">';
    case 'invasion': {
      const rar={'Común':'comun','Rara':'rara','Épica':'epica'};
      const cartas=D.cards.map(c=>`<article class="qa-carta ${rar[c.rarity]}"><span class="qa-rareza">${esc(c.rarity)}</span><strong>${esc(c.name)}</strong><p>${esc(c.text)}</p><small>${esc(c.with)} · ${c.max>1?'hasta 3 veces':'una sola vez'}</small></article>`).join('');
      return `<div class="qa-run qa-invasion"><figure><img src="${invasionView}" alt="Oleada en el campus"><figcaption>Sobreviví a la oleada</figcaption></figure><figure><img src="${proofImages}US-348_CA2_Invasion_eleccion_de_cartas.png" alt="Tres recompensas del puesto"><figcaption>Cargá el puesto y elegí 1 de 3</figcaption></figure></div><p class="qa-caption">Matando se carga el puesto de suministros y ahí se elige 1 de 3: un arma o una carta. Las cartas cambian cómo rinde cada arma; algunas se acumulan hasta 3 veces. Cuanto más avanzan las oleadas, más salen las raras y las épicas. Volver a tirar cuesta 150 puntos.</p><div class="qa-cartas">${cartas}</div>`;
    }
    case 'cuenta': return '<div class="qa-account"><div><strong>Crear cuenta</strong><span>Mail y contraseña</span></div><div><strong>Ingresar con Steam</strong><span>Acceso previsto</span></div></div><img class="qa-picture" style="display:block;width:100%;height:auto;max-height:38dvh;object-fit:contain" src="media/cuenta-confirmacion.png" alt="Correo de Project Riftwalker para confirmar el registro de una cuenta"><p class="qa-caption">Tu perfil, logros y monedas te acompañan al cambiar de PC.</p>';
  }
}
// Posiciones en porcentajes del tablero, compartidas por los trazos y sus botones.
const questionSketch = [
  [25, 13, -9, 'M37 32 C34 28 36 24 25 13'],
  [71, 13, 12, 'M64 32 Q64 31 62 28 T71 13'],
  [14, 39, 5, 'M32 44 C30 45 36 43 30 44 S19 40 14 39'],
  [84, 38, -6, 'M68 43 C69 43 65 41 72 42 Q78 42 84 38'],
  [18, 66, -13, 'M35 61 Q34 61 32 60 T18 66'],
  [81, 67, 8, 'M65 61 C70 60 69 66 81 67'],
  [49, 86, 11, 'M50 70 Q48 74 49 86']
];
function questionsScreen() {
  const buttons = questionTopics.map(([name], index) => {
    const [x, y, tilt] = questionSketch[index];
    return `<button class="question-node" data-question="${index}" style="left:${x}%;top:${y}%;--tilt:${tilt}deg">
      <svg viewBox="0 0 120 100" aria-hidden="true"><path class="ink-ring" d="M90 9 C67 1 35 5 19 18 C2 32 4 56 12 71 C23 91 50 97 78 91 C100 87 115 68 113 46 C112 28 103 14 83 9 C76 7 69 6 62 7"/>
      <path class="ink-echo" d="M8 49 C7 66 19 84 37 89 M76 94 Q95 91 104 79"/></svg>
      <span>${esc(name)}</span></button>`;
  }).join('');
  const lines = questionSketch.map(([, , , path]) => `<path d="${path}"/>`).join('');
  return `<section class="questions-board" aria-label="Preguntas del público">
    <svg aria-hidden="true" viewBox="0 0 100 100" preserveAspectRatio="none">${lines}</svg>
    <div class="question-center"><p>¡Gracias!</p><h1>¿Preguntas?</h1></div>
    ${buttons}
    <dialog id="questionDialog"><div class="dialog-head"><h2 id="questionTitle"></h2><button data-close="questionDialog">Cerrar ×</button></div><div id="questionBody"></div></dialog>
  </section>`;
}
function openQuestion(index) {
  const [, title, key] = questionTopics[index];
  $('#questionTitle').textContent = title;
  $('#questionDialog').dataset.topic = key;
  $('#questionBody').innerHTML = questionVisual(key);
  if (key === 'mapa') mapPhoto();
  $('#questionDialog').showModal();
  $('#questionDialog').onclose = () => { $('#questionBody').replaceChildren(); };
}

function render(focus=false){document.body.classList.remove('trailer-expanded');const s=D.slides[current];$('#slide').innerHTML=content(s);const questions=s.id==='cierre';if(questions&&running)$('#timerToggle').click();$('#timerToggle').disabled=questions;$('#position').textContent=`${current+1} / ${D.slides.length}`;$('#prev').disabled=current===0;if(current>=D.slides.length-2)prepararSecreto();$('#progress').style.width=`${(current+1)/D.slides.length*100}%`;$('#speaker').textContent=s.speaker||D.speaker;$('#announcement').textContent=`${current+1} de ${D.slides.length}: ${s.title}`;history.replaceState(null,'','#'+s.id);if(focus){$('#slide').focus({preventScroll:true});scrollTo(0,0);$('#slideScroll').scrollTop=0}}
// Diapositiva secreta: Siguiente en el cierre rompe la página de a poco (temblor, cortes, grietas, la interfaz se cae), se abre el
// portal y pasa el video del modo invasión. No cuenta en el índice, el contador ni el reloj; ← o Esc vuelve al cierre.
const secreto={abierto:false,timers:[],tic:0,capa:null,video:null};
function go(n){const ultima=D.slides.length-1;
 if(secreto.abierto){if(n<current)cerrarSecreto();return}
 if(n>ultima&&current===ultima){abrirSecreto();return}
 current=Math.max(0,Math.min(ultima,n));render(true)}
function prepararSecreto(){
 if(secreto.capa)return;const c=document.createElement('div');c.id='secreto';c.hidden=true;c.setAttribute('aria-hidden','true');
 c.innerHTML=`<div class="fondo"></div><div class="destellos"></div><div class="rebanadas"></div><div class="cortes">${'<i></i>'.repeat(14)}</div><svg class="grietas"></svg>${D.secret.video?`<video playsinline preload="auto" src="${esc(D.secret.video)}"></video>`:''}<div class="portal"></div>`;
 document.body.append(c);secreto.capa=c;const v=secreto.video=c.querySelector('video');
 // El recuadro del portal toma la forma del video (16:9 hasta que se sabe).
 v?.addEventListener('loadedmetadata',()=>c.style.setProperty('--aspecto',v.videoWidth/v.videoHeight));
}
// Grietas que salen de un golpe cerca del centro, en zigzag hasta el borde, en píxeles de la pantalla (así el largo con que
// se dibujan coincide con el real). Quedan sin dibujar hasta agrietar().
function grietas(svg){
 const W=innerWidth,H=innerHeight,r=(a,b)=>a+Math.random()*(b-a),p=(x,y)=>`${(x*W/1000).toFixed(1)} ${(y*H/1000).toFixed(1)}`;
 const camino=(x,y,ang,pasos)=>{let d='M'+p(x,y);for(let i=0;i<pasos&&x>0&&x<1000&&y>0&&y<1000;i++){ang+=r(-.45,.45);const l=r(35,110);x+=Math.cos(ang)*l;y+=Math.sin(ang)*l;d+=' L'+p(x,y)}return d};
 const x0=r(440,580),y0=r(380,500),d=[];
 // El portal se abre donde fue el golpe.
 svg.parentNode.style.setProperty('--golpe-x',x0/10+'%');svg.parentNode.style.setProperty('--golpe-y',y0/10+'%');
 for(let k=0;k<11;k++){const ang=k/11*Math.PI*2+r(-.25,.25);d.push(camino(x0,y0,ang,18));if(Math.random()<.6)d.push(camino(x0+Math.cos(ang)*r(80,200),y0+Math.sin(ang)*r(80,200),ang+r(-1,1),6))}
 svg.setAttribute('viewBox',`0 0 ${W} ${H}`);svg.innerHTML=d.map(x=>`<path d="${x}"/>`).join('');
 svg.querySelectorAll('path').forEach(x=>{const l=x.getTotalLength();x.style.strokeDasharray=l;x.style.strokeDashoffset=l});
}
// Las grietas se dibujan de a una: cada una arranca un poco después de la anterior.
function agrietar(svg){svg.querySelectorAll('path').forEach((p,i)=>{p.style.transitionDelay=(i*.13)+'s';p.style.strokeDashoffset=0})}
// Pedazos de la página corridos de lugar: copias de la diapositiva que cada tic muestran solo una franja, movida al costado.
function rebanadas(c){
 const m=$('#slide'),b=m.getBoundingClientRect(),cont=c.querySelector('.rebanadas');cont.innerHTML='';
 for(let k=0;k<5;k++){const r=m.cloneNode(true);r.removeAttribute('id');r.removeAttribute('tabindex');
  Object.assign(r.style,{position:'absolute',left:b.left+'px',top:b.top+'px',width:b.width+'px',height:b.height+'px',margin:0,background:'var(--bg)',display:'none'});cont.append(r)}
}
// Cada tic, franjas que invierten, tiñen o tapan la página, pedazos corridos y algún destello negro. k (de 0 a 1) es
// cuánto se rompió: al principio casi nada, cada vez más (también el temblor y la separación de colores).
function cortes(c,k){
 const hay=p=>Math.random()<p;
 c.querySelectorAll('.rebanadas>main').forEach(r=>{if(!hay(.08+.47*k)){r.style.display='none';return}
  const t=Math.random()*90,h=1+Math.random()*(3+10*k),dx=(Math.random()<.5?-1:1)*(4+Math.random()*(8+60*k));
  Object.assign(r.style,{display:'block',clipPath:`inset(${t}% 0 ${Math.max(0,100-t-h)}% 0)`,translate:`${dx}px 0`})});
 c.querySelectorAll('.cortes i').forEach(i=>{if(!hay(.04+.46*k)){i.style.display='none';return}
  i.className=['inv','inv','tinte','negro'][Math.floor(Math.random()*4)];
  Object.assign(i.style,{display:'block',top:Math.random()*100+'%',height:(.4+Math.random()*(1.5+6.5*k))+'vh',left:(Math.random()*40-20)+'%',width:(30+Math.random()*(30+60*k))+'%',transform:`translateX(${(Math.random()*2-1)*(8+52*k)}px)`})});
 c.querySelector('.destellos').style.opacity=hay(.015+.1*k)?.85:0;
 document.body.style.setProperty('--temblor',(.6+6.4*k).toFixed(1)+'px');document.body.style.setProperty('--rgb',(.8+4.7*k).toFixed(1)+'px');
}
// Unos 8,5 s: la página falla cada vez más (0 a 4,6 s), se agrieta (desde 2 s), la barra, los controles y la diapositiva se
// aflojan y se caen de a uno (4,3 a 7,5 s), se abre una grieta de luz (6,3 s), se vuelve el portal con la forma del video
// (7,6 s) y el video pasa adentro (8,5 s).
function abrirSecreto(){
 prepararSecreto();const c=secreto.capa,v=secreto.video,svg=c.querySelector('.grietas'),b=document.body,t=(ms,f)=>secreto.timers.push(setTimeout(f,ms));
 secreto.abierto=true;c.className=v?'':'sin-video';c.hidden=false;document.activeElement?.blur();
 const empezarVideo=()=>{if(!v)return;c.classList.add('video');v.currentTime=0;v.play().catch(()=>{v.muted=true;v.play().catch(()=>{})})};
 if(matchMedia('(prefers-reduced-motion: reduce)').matches){b.classList.add('rompiendo','cae-top','cae-nav','cae-main');c.classList.add('oscuro','abriendo','abierto');empezarVideo();return}
 rebanadas(c);grietas(svg);b.classList.add('rompiendo');
 const inicio=performance.now(),tic=()=>cortes(c,Math.min(1,(performance.now()-inicio)/4600));tic();secreto.tic=setInterval(tic,70);
 t(2000,()=>{c.classList.add('agrietada');agrietar(svg)});
 t(4300,()=>b.classList.add('flojo'));
 t(4800,()=>b.classList.add('cae-top'));
 t(5200,()=>b.classList.add('cae-nav'));
 t(5500,()=>{b.classList.add('cae-main');c.classList.add('oscuro')});
 t(6300,()=>c.classList.add('abriendo'));
 t(7600,()=>{clearInterval(secreto.tic);c.querySelector('.destellos').style.opacity=0;c.classList.add('abierto')});
 t(8500,empezarVideo);
}
function cerrarSecreto(){
 secreto.timers.forEach(clearTimeout);clearInterval(secreto.tic);secreto.timers=[];secreto.abierto=false;
 document.body.classList.remove('rompiendo','flojo','cae-top','cae-nav','cae-main');document.body.style.removeProperty('--temblor');document.body.style.removeProperty('--rgb');
 const c=secreto.capa;c.hidden=true;c.className='';
 if(secreto.video){secreto.video.pause();secreto.video.currentTime=0}
}
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

 if(d.photo){campusPhoto=(campusPhoto+Number(d.photo)+campusPhotos.length)%campusPhotos.length;mapPhoto()}
 if(d.question!==undefined)openQuestion(Number(d.question));
 if(d.demo)openDemo(d.demo);
 if(d.trailer!==undefined){
  document.body.classList.add('trailer-expanded');
  if(!document.fullscreenElement)document.documentElement.requestFullscreen?.().catch(()=>{});
  $('#trailerVideo').play().catch(()=>{});
 }
 if(d.exitTrailer!==undefined)closeTrailer();
});
document.addEventListener('input',event=>{
  if(event.target.id==='mapFiles'&&event.target.files.length){
    const files=Array.from(event.target.files).filter(file=>file.type.startsWith('image/'));
    if(!files.length)return;
    for(const url of campusPhotos)if(url.startsWith('blob:'))URL.revokeObjectURL(url);
    campusPhotos=files.map(file=>URL.createObjectURL(file));campusPhoto=0;mapPhoto();
  }
});
document.addEventListener('change',e=>{if(e.target.id==='weaponSelect'){weaponId=e.target.value;render()}});
document.addEventListener('keydown',e=>{if(document.body.classList.contains('trailer-expanded')){if(e.key==='Escape'){e.preventDefault();closeTrailer()}return}if(secreto.abierto){if(['Escape','ArrowLeft','PageUp'].includes(e.key)){e.preventDefault();cerrarSecreto()}return}if(document.querySelector('dialog[open]')||document.fullscreenElement?.tagName==='VIDEO'||e.target.closest('input,select,textarea,button,a,video'))return;
 if(e.key==='ArrowRight'||e.key==='PageDown'){e.preventDefault();go(current+1)}if(e.key==='ArrowLeft'||e.key==='PageUp'){e.preventDefault();go(current-1)}if(e.key.toLowerCase()==='f')$('#fullscreen').click();
});
// Child demos use their own keyboard; only an explicit return message closes them.
addEventListener('message',e=>{if(e.origin===location.origin&&e.source===$('#demoFrame').contentWindow&&e.data==='riftwalker:close-demo')$('#demoDialog').close()});
let running=false,elapsed=0,last=0;const duration = 20 * 60;
function clock(){const total=elapsed+(running?(performance.now()-last)/1000:0),remaining=duration-total,seconds=Math.ceil(Math.abs(remaining));$('#timer').textContent=`${remaining<0?'+':''}${String(Math.floor(seconds/60)).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;$('#timer').classList.toggle('over',remaining<0)}
$('#timerToggle').onclick=()=>{if(running)elapsed+=(performance.now()-last)/1000;else last=performance.now();running=!running;$('#timerToggle').textContent=running?'Ⅱ Pausar':'▶ Tiempo';clock()};
$('#timerReset').onclick=()=>{elapsed=0;last=performance.now();clock()};setInterval(clock,200);
const initial=D.slides.findIndex(s=>'#'+s.id===location.hash);current=initial<0?0:initial;render();clock();

addEventListener('hashchange',()=>{const i=D.slides.findIndex(s=>'#'+s.id===location.hash);if(i>=0&&i!==current)go(i)});
