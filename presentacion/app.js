'use strict';
const D=window.PRESENTATION, $=s=>document.querySelector(s);
// Los tres planes de Financiación están guardados fuera del recorrido; con ?planes en la dirección vuelven después de Costos.
if(D.savedSlides&&new URLSearchParams(location.search).has('planes'))D.slides.splice(D.slides.findIndex(x=>x.id==='costos')+1,0,...D.savedSlides);
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let current=0,weaponId='fusil';
const num=n=>n.toLocaleString('es-AR'),dec=n=>n.toLocaleString('es-AR',{minimumFractionDigits:2,maximumFractionDigits:2});
const money=(n,sign=false)=>(n<0?'−':sign&&n>0?'+':'')+'US$ '+num(Math.abs(Math.round(n)));
const thousands=n=>n>=1e6?n.toLocaleString('es-AR',{maximumFractionDigits:1})+' M':num(Math.round(n/1000)*1000);
// Números del simulador de la guía: los tres escenarios lado a lado.
function scenarios(){
 // Una tabla: los rótulos una sola vez a la izquierda y los tres escenarios en columnas, así la letra entra grande.
 const L=D.economics.scenarios;
 const fila=(t,f)=>`<tr><th scope="row">${t}</th>${L.map(x=>`<td>${f(x)}</td>`).join('')}</tr>`;
 return `<table class="esc-tabla"><thead><tr><th></th>${L.map(x=>`<th scope="col"><span>${esc(x.name)}</span><b class="${x.afterTax>=0?'pos':'neg'}">${money(x.afterTax,true)}</b><small>por mes</small></th>`).join('')}</tr></thead><tbody>${fila('Jugadores por mes',x=>num(x.players))}${fila('Pagan',x=>`${num(x.payers)} · ${x.conv.toLocaleString('es-AR')} %`)}${fila('Gasto mensual por pagador',x=>'US$&nbsp;'+dec(x.arppu))}${fila('Llega a esos jugadores',x=>`en ${x.ramp} meses`)}${fila('Recupero',x=>x.payback?'mes '+x.payback:'no se recupera')}</tbody></table>`;
}
// Cuánto se invierte, en qué y qué vuelve.
function investment(){
 const E=D.economics,I=E.investment,L=E.scenarios,mid=L[1];
 return `<div class="grid two invest"><article class="card"><p class="eyebrow">La inversión</p><div class="metric-number">${money(I.total)}</div><div class="uses">${I.items.map(([n,v])=>`<div><span>${esc(n)}</span><b>${money(v)}</b><i style="width:${Math.max(1,v/I.total*100)}%"></i></div>`).join('')}</div><p class="footnote">Después, el juego necesita ${money(I.monthlyFixed)} fijos por mes (${money(I.salaries)} son los sueldos de los 7) más los servidores.</p></article><article class="card"><p class="eyebrow">Lo que vuelve</p><table class="returns"><tr><th></th><th>Recupero</th><th>A los 3 años</th></tr>${L.map(x=>`<tr><td>${esc(x.name)}</td><td>${x.payback?'mes '+x.payback:'no se recupera'}</td><td>${x.perDollar?`US$&nbsp;${dec(x.perDollar)} por cada dólar`:`pierde ${money(-x.result)} por mes`}</td></tr>`).join('')}</table></article></div><div class="watch"><div><b>${num(mid.peak)}</b><span>a la vez en la hora pico, de los ${num(mid.players)} jugadores por mes del promedio</span></div><div><b>${thousands(mid.breakEven)}</b><span>jugadores por mes para no perder plata</span></div><div><b>${mid.conv.toLocaleString('es-AR')} %</b><span>de los jugadores paga algo en el mes</span></div><div><b>US$&nbsp;${dec(mid.arppu)}</b><span>por mes cada uno que paga: US$&nbsp;${dec(mid.arppuLatam)} en Latinoamérica (${mid.latam}&nbsp;%) y US$&nbsp;${dec(mid.arppuRest)} en el resto</span></div></div>`;
}
// f(x): lo que queda por mes según los jugadores que pagan, con las zonas (pierde, se sostiene, va bien) dentro del
// gráfico. Desde el 25/11/2026 va solo el gráfico, a todo el ancho; lo de cada zona se dice con el guion.
function payers(){
 const F=D.economics.fx,W=900,H=580,L=92,R=40,T=40,B=56,xmax=4000,ymin=-10000,ymax=10000;
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
 return `<div class="fx">${svg}</div>`;
}
// Valor de la f(x) bajo el mouse: la línea vertical, el punto sobre la curva y el número.
document.addEventListener('pointermove',e=>{
 const hit=e.target.closest&&e.target.closest('.fx-hit'),g=document.getElementById('fxHover');
 if(!g)return;if(!hit){g.setAttribute('visibility','hidden');return}
 const svg=hit.ownerSVGElement,pt=svg.createSVGPoint();pt.x=e.clientX;pt.y=e.clientY;const p=pt.matrixTransform(svg.getScreenCTM().inverse());
 const [l,r,xmax]=hit.dataset.fx.split(',').map(Number),F=D.economics.fx,x=Math.max(0,Math.min(xmax,(p.x-l)/(r-l)*xmax));
 const [px,pv]=F.points.reduce((a,q)=>Math.abs(q[0]-x)<Math.abs(a[0]-x)?q:a);
 const W=900,H=580,L=92,R=40,T=40,B=56,X=v=>L+(W-L-R)*v/xmax,Y=v=>T+(H-T-B)*(10000-v)/20000;
 const t=`${num(px)} pagan · ${(pv<0?'−':pv>0?'+':'')}US$ ${num(Math.abs(pv))} por mes`;
 const txt=document.getElementById('fxHoverTxt'),box=document.getElementById('fxHoverBox');
 document.getElementById('fxHoverLine').setAttribute('x1',X(px));document.getElementById('fxHoverLine').setAttribute('x2',X(px));
 const dot=document.getElementById('fxHoverDot');dot.setAttribute('cx',X(px));dot.setAttribute('cy',Y(pv));
 txt.textContent=t;const w=t.length*9+20,bx=Math.min(Math.max(X(px)-w/2,L),W-R-w),by=Math.max(T+34,Y(pv)-48);
 box.setAttribute('x',bx);box.setAttribute('y',by);box.setAttribute('width',w);txt.setAttribute('x',bx+10);txt.setAttribute('y',by+21);
 g.setAttribute('visibility','visible');
});
// Si no despega: los puntos de control (en jugadores que pagan) y lo máximo que pierde el inversor. Nadie se va antes de
// que termine la rampa: en el mes 3 se corrige, en el 6 se ajustan las horas de los 7 y en el 9 se decide.
// Si despega fuerte (25/11/2026): el plan del optimista, con las mismas paradas que Escala y Protección: la señal, el
// equipo de 12 y la publicidad con torneos; abajo, lo que se reinvierte y lo que le queda al inversor.
function boom(){
 const B=D.economics.boom;
 return `<div class="escala-linea despegue"><div class="parada senal"><i></i><p class="eyebrow">La señal</p><b>${num(B.signal)}</b><span>pagadores por mes</span><strong>El promedio llega a ${num(B.averagePayers)}</strong></div><div class="parada completo"><i></i><p class="eyebrow">Equipo de ${B.team}</p><b>+${B.hires}</b><span>${esc(B.hiresText)}</span><strong>${money(B.hiresCost)} por mes</strong></div><div class="parada completo"><i></i><p class="eyebrow">Más gente</p><b>× ${B.ads/B.adsNow}</b><span>en publicidad, más torneos</span><strong>${money(B.reach)} más por mes</strong></div></div><div class="grid escala-fin despegue-fin"><article class="card scenario"><p class="eyebrow">Se reinvierte</p><div class="result">${money(B.reinvest)}<small>por mes, en el juego</small></div></article><article class="card scenario"><p class="eyebrow">Con ${B.growth} % más jugadores</p><div class="result pos">${money(B.grown,true)}<small>por mes al inversor (sin reinvertir, ${money(B.withFull)})</small></div></article><article class="card"><p class="eyebrow">Más contenido, más jugadores</p><p><b>Desde un ${B.payoff} % más de jugadores, el inversor gana más que sin reinvertir.</b></p></article></div>`;
}
function guard(){
 const G=D.economics.guard;
 return `<div class="escala-linea guard"><div class="parada"><i></i><p class="eyebrow">Mes 3 · Corregir</p><b>&lt;&nbsp;${num(G.checkpoint3)}</b><span>pagadores: nadie se va, se corrige</span></div><div class="parada senal"><i></i><p class="eyebrow">Mes 6 · Ajustar</p><b>&lt;&nbsp;${num(G.checkpoint6)}</b><span>pagadores: los 7 siguen, con menos horas</span></div><div class="parada corte"><i></i><p class="eyebrow">Mes 9 · Decidir</p><b>&lt;&nbsp;${num(G.checkpoint9)}</b><span>pagadores: se cierra, con la indemnización reservada</span></div></div><div class="grid two guard-fin"><article class="card scenario"><p class="eyebrow">Pesimista, con el plan</p><div class="result neg">${money(G.worst,true)}<small>lo máximo que pierde el inversor</small></div></article><article class="card scenario"><p class="eyebrow">Pesimista, sin el plan</p><div class="result neg">${money(G.without,true)}<small>a los 3 años, siguiendo igual</small></div></article></div>`;
}
// Si funciona, los 7 a tiempo completo: tres paradas (hoy, la señal y tiempo completo) y lo que queda con el equipo completo.
function scale(){
 const S=D.economics.scale,pct=n=>Math.round(n*100)+' %',[a,b]=S.scenarios;
 return `<div class="escala-linea"><div class="parada"><i></i><p class="eyebrow">Hoy</p><b>${num(Math.round(S.hours))} h</b><span>por sprint, entre los 7</span><strong>${money(S.salaries)} por mes en sueldos</strong></div><div class="parada senal"><i></i><p class="eyebrow">La señal</p><b>${thousands(S.breakEven)}</b><span>jugadores por mes</span><strong>${esc(a.name)}: mes ${a.payback} · ${esc(b.name)}: mes ${b.payback}</strong></div><div class="parada completo"><i></i><p class="eyebrow">Tiempo completo</p><b>${num(S.fullHours)} h</b><span>por sprint, los 7 a 80 h</span><strong>${money(S.fullSalaries)} por mes en sueldos</strong></div></div><div class="grid escala-fin">${S.scenarios.map(x=>`<article class="card scenario"><p class="eyebrow">${esc(x.name)}</p><div class="result pos">${money(x.grown,true)}<small>por mes con ${S.growth} % más jugadores</small></div></article>`).join('')}<article class="card"><p class="eyebrow">Más contenido, más jugadores</p><p><b>Desde un ${S.payoff} % más de jugadores, el inversor gana más que hoy.</b></p></article></div>`;
}
// A dónde va la inversión y, cada mes del promedio, lo que pagan los jugadores.
function costs(){
 // Rehecha el 25/11/2026 para la PC del aula: la inversión con sus cuatro partes y, cada mes del promedio, lo que pagan los
 // jugadores repartido en cinco grupos de gastos con una barra cada uno. El detalle (Sysarmy, cada servicio) va en el guion.
 const E=D.economics,I=E.investment,M=E.month;
 const fila=(n,v,base,cls='')=>`<div class="ct-fila ${cls}"><span>${esc(n)}</span><b>${money(v)}</b><i style="width:${Math.max(1,v/base*100)}%"></i></div>`;
 const grupos=M.groups.map(([n,partes])=>[n,partes.reduce((t,p)=>t+M.items.find(([x])=>x===p)[1],0)]);
 return `<div class="costos2"><article class="card"><p class="eyebrow">Inversión inicial</p><div class="ct-total">${money(I.total)}</div>${I.detail.map(([n,v])=>fila(n,v,I.total)).join('')}</article><article class="card"><p class="eyebrow">Cada mes, en el promedio</p>${fila('Lo que pagan los jugadores',M.gross,M.gross,'ingreso')}${grupos.map(([n,v])=>fila('− '+n,v,M.gross,'gasto')).join('')}${fila('Queda de ganancia',M.left,M.gross,'queda')}</article></div>`;
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
// Avance de a un sprint (25/11/2026): arriba la línea de los 5 sprints con el actual marcado; abajo la foto grande y lo
// que sumó, con sus imágenes. Para la PC del aula (1024 × 768) la letra base ya es grande y el zoom la agranda más.
function sprintSlide(s){
 const p=D.progress[s.sprint];
 const linea=`<ol class="sprint-linea">${D.progress.map((q,i)=>`<li class="${i<s.sprint?'hecho':i===s.sprint?'actual':''}">${esc(q.name)}</li>`).join('')}</ol>`;
 const items=p.items.map(it=>`<li><span>${esc(it.text)}</span>${it.images?`<span class="avance-imgs${it.round?' redondas':''}${it.icons?' iconos':''}">${it.images.map(src=>`<img src="${esc(src)}" alt="">`).join('')}</span>`:''}</li>`).join('');
 return `<div class="head"><p class="eyebrow">${esc(s.section)} · ${esc(p.name)} · ${esc(p.dates)}</p><h2>${esc(p.title)}</h2></div>${linea}<div class="sprint-uno"><div class="avance-foto">${p.image?`<img src="${esc(p.image)}" alt="">`:''}</div><ul>${items}</ul></div>`;
}
// La historia en tres momentos, con cuadros del tráiler, y abajo los modos que la representan con su mapa.
// Identidad: los personajes (ciudades), los mapas (lugares del país), las armas (trenes) y el easter egg del TUM.
function edge(){
 const E=D.edge;
 return `<div class="diferencial">${E.pillars.map(p=>`<article class="dif-col${p.next?' dif-sigue':''}"><figure><img src="${esc(p.image)}" alt=""></figure><div><p class="eyebrow">${esc(p.tag)}</p><h3>${esc(p.title)}</h3><em>${esc(p.compare)}</em></div></article>`).join('')}</div>`;
}
function identity(){
 // Rehecha el 25/11/2026 para la PC del aula (1024 × 768): solo imágenes y títulos grandes; lo demás va en el guion.
 const I=D.identity;
 const item=(img,nombre)=>`<figure><div class="ident-img"><img src="${esc(img)}" alt=""></div><figcaption>${esc(nombre)}</figcaption></figure>`;
 const grupo=(cls,titulo,items)=>`<section class="ident-grupo ${cls}"><h3>${esc(titulo)}</h3><div class="ident-items">${items.join('')}</div></section>`;
 return `<div class="ident"><div class="ident-izq">${grupo('ident-personajes','Personajes: ciudades',I.characters.map(c=>item(c.image,c.name)))}${grupo('ident-mapas','Mapas: lugares del país',I.maps.map(m=>item(m.image,m.name)))}${grupo('ident-armas','Armas: trenes',I.weapons.map(w=>item(w.image,w.name)))}</div><div class="ident-der">${grupo('ident-escolta','Carta legendaria',[item(I.escort.image,I.escort.title)])}${grupo('ident-huevo','Easter egg',[item(I.egg.image,'Plaga Gris')])}</div></div>`;
}
function story(){
 const H=D.story;
 return `<div class="historia"><div class="relato">${H.chapters.map((c,i)=>`<figure class="capitulo"><img src="${esc(c.image)}" alt=""><figcaption><b>${String(i+1).padStart(2,'0')}</b><strong>${esc(c.title)}</strong></figcaption></figure>`).join('')}</div><p class="facetas">${esc(H.facets)}</p><div class="modos-historia">${H.modes.map(m=>`<article class="modo-historia${m.map?'':' corto'}"><img src="${esc(m.image)}" alt=""><div><strong>${esc(m.name)}</strong><span>${esc(m.facet)}</span>${m.map?`<small>Mapa: ${esc(m.map)}</small>`:''}</div></article>`).join('')}</div></div>`;
}
// Los conjuntos del Perfil (avatar, banner y marco del mismo estilo), uno abajo del otro como la cabecera del juego.
function sets(){
 const RZ=['Inicial','Común','Raro','Épico','Legendario'],COL=['#8E96A3','#9AA3B0','#5C7CFF','#B061FF','#FFC93C'];
 const PJ=Object.fromEntries(CATALOGO.personajes.map(p=>[p.id,p.nombre])),W=900,H=150,base='../menu/img/';
 return `<div class="conjuntos">${Marcos.conjuntos().map(({banner:b,avatar:a})=>{const d=Marcos.interior(b.id,H);return `<article class="conjunto"><div class="conjunto-banner" style="left:${d[0]}px;top:${d[1]}px;right:${d[2]}px;bottom:${d[3]}px"><img src="${base}banners/${b.id}.jpg" alt=""><i></i></div>${Marcos.html(b.id,0,0,W,H,false,base+'marcos/')}<span class="conjunto-avatar"><img src="${base}avatares/${a.id}.jpg" alt=""></span><strong>${esc(b.nombre)}</strong><small><b style="color:${COL[b.rareza]}">${RZ[b.rareza]}</b> · ${esc(b.estilo)}${PJ[b.personaje]?' · '+PJ[b.personaje]:''}</small></article>`}).join('')}</div>`;
}
// Los cuatro productos a la vista, cada uno con una imagen hecha con cosas del juego (avatares, banners y cápsulas).
function offer(){
 // Rehecha el 25/11/2026 para la PC del aula: imagen grande, título, precio y un solo dato por producto; el resto va en el guion.
 const R={1:'#9AA3B0',2:'#5C7CFF',3:'#B061FF',4:'#FFC93C'},m='../menu/img/';
 const visual={
  // Un cosmético de cada rareza con su precio: avatar, banner, baile y la skin legendaria del Mitre.
  tienda:()=>`<div class="vt-tienda">${[[m+'avatares/astra-celeste.jpg',1,'400'],[m+'banners/guerrero-synthwave.jpg',2,'800'],['media/emotes/astra-shuffle.png',3,'1.600'],[m+'skins/mitre-dragon-abisal.webp',4,'3.200']].map(([src,r,p])=>`<span><img src="${src}" alt="" style="border-color:${R[r]}"><b style="color:${R[r]}">${p}</b></span>`).join('')}</div>`,
  pase:()=>`<div class="vt-pase"><div>${[['astra-celeste',1],['guerrero-synthwave',2],['astra-acuarela',3],['astra-nouveau',4]].map(([id,r])=>`<img src="${m}avatares/${id}.jpg" alt="" style="border-color:${R[r]}">`).join('')}</div><i>${Array.from({length:40},(_,k)=>`<u${(k+1)%10===0?' class="hito"':''}></u>`).join('')}</i><small><span>Nivel 1</span><span>Nivel 40</span></small></div>`,
  capsulas:()=>`<div class="vt-capsulas"><span class="vt-capsula"><i></i></span><ul>${[[55,'Común',1],[30,'Raro',2],[12,'Épico',3],[3,'Legendario',4]].map(([p,n,r])=>`<li><b style="color:${R[r]}">${p} %</b> ${n}</li>`).join('')}</ul></div>`,
  cristales:()=>`<div class="vt-cristales">${[['500','4,99',.75],['1.100','9,99',.9],['2.300','19,99',1.05],['6.000','49,99',1.2]].map(([c,p,k])=>`<span><i style="--k:${k}"></i><b>${c}</b><small>US$ ${p}</small></span>`).join('')}</div>`
 };
 return `<div class="venta">${D.business.map(b=>`<article class="venta-card"><div class="venta-img">${visual[b.visual]()}</div><div class="venta-txt"><h3>${esc(b.name)}</h3><p>${esc(b.fact)}</p></div><b class="venta-precio">${esc(b.model)}</b></article>`).join('')}</div><p class="lead venta-lema">Jugar es gratis. Nada de lo que se vende da ventaja en la partida.</p>`;
}
const tag=(text,pending=false)=>`<span class="tag ${pending?'pending':''}">${esc(text)}</span>`;
const card=(title,text)=>`<article class="card"><h3>${esc(title)}</h3><p>${esc(text)}</p></article>`;
// La parte de la presentación (Juego, Equipo, Financiación) va antes del nombre del bloque.
const eyebrow=s=>`${s.section&&s.section!==s.label?esc(s.section)+' · ':''}${esc(s.label)}`;
const head=s=>`<div class="head"><p class="eyebrow">${eyebrow(s)}</p><h2>${esc(s.title)}</h2></div>`;
// Avance de los tres sprints: historias terminadas y pruebas que pasan. Un sprint todavía sin datos muestra rayas.
function metrics(){
 // Una tabla: un sprint por fila, con las historias en Done y las pruebas que pasan en dos columnas anchas. Lo que falta de cada uno va en el guion.
 const pct=(a,b)=>(100*a/b).toLocaleString('es-AR',{maximumFractionDigits:1})+' %';
 const bar=(parts,total,label)=>`<div class="bars" role="img" aria-label="${label}">${parts.map(([v,c])=>`<span style="width:${v/Math.max(1,total)*100}%;background:${c}"></span>`).join('')}<span style="flex:1;background:var(--line)"></span></div>`;
 const S=['s1','s2','s3','s4','s5'].map(k=>D.metrics[k]);
 const ok=m=>!['planned','done','review','todo','tests','passed','failed','blocked','unrun'].some(k=>m[k]==null);
 const historias=m=>ok(m)?`<b>${pct(m.done,m.planned)}</b><span>${m.done} de ${m.planned}</span>${bar([[m.done,'var(--green)'],[m.review,'var(--accent)'],[m.stuck||0,'var(--red)']],m.planned,`${m.done} en Done, ${m.review} en revisión, ${m.stuck||0} bloqueadas, ${m.todo} sin empezar`)}`:'<b>—</b>';
 const pruebas=m=>{if(!ok(m))return '<b>—</b>';const run=m.tests-m.unrun;return `<b>${pct(m.passed,run)}</b><span>${m.passed} de ${run}</span>${bar([[m.passed,'var(--green)'],[m.failed,'var(--red)'],[m.blocked,'var(--accent)']],m.tests,`${m.passed} pasan, ${m.failed} fallan, ${m.blocked} bloqueadas, ${m.unrun} sin ejecutar`)}`};
 return `<div class="met-tabla"><div></div><div class="met-rotulo">Historias en Done</div><div class="met-rotulo">Pruebas que pasan</div>${S.map(m=>`<div class="met-sprint">${esc(m.name.split(' · ')[0])}</div><div class="met-celda">${historias(m)}</div><div class="met-celda">${pruebas(m)}</div>`).join('')}</div>`;
}
function arsenal(){
 const w=window.ARSENAL.find(x=>x.id===weaponId)||window.ARSENAL[0],t=w.tramos[0];
 // Contra 100 de vida y 50 de escudo, con el daño del primer tramo (la escopeta, con todos los perdigones).
 const tiros=Math.ceil(150/(t.cuerpo*(w.perdigones||1)));
 return `<div class="row" style="margin-bottom:20px"><label for="weaponSelect" class="sr">Elegir arma</label><select id="weaponSelect">${window.ARSENAL.map(x=>`<option value="${esc(x.id)}" ${x.id===w.id?'selected':''}>${esc(x.alias)} · ${esc(x.nombre)}</option>`).join('')}</select></div><div class="weapon"><div class="weapon-art"><img src="media/armas/${esc(w.id)}.png" alt="${esc(w.alias)}"></div><div><p class="eyebrow">${esc(w.modo)}</p><h3>${esc(w.alias)}</h3><div class="stats"><div><b>${w.precio.toLocaleString('es-AR')}</b><span>monedas de partida</span></div><div><b>${w.cargador}</b><span>balas / cargador</span></div><div><b>${w.recarga.toLocaleString('es-AR')} s</b><span>recarga</span></div><div><b>${t.cabeza}</b><span>daño · cabeza</span></div><div><b>${t.cuerpo}</b><span>daño · cuerpo</span></div><div><b>${tiros}</b><span>${tiros===1?'tiro':'tiros'} al cuerpo con escudo completo</span></div></div></div></div>`;
}
function content(s){
 switch(s.id){
 case 'inicio':return `<div class="hero"><p class="eyebrow">Laboratorio de Construcción de Software · Sprint 5</p><img class="hero-logo" src="media/logo.png" alt="Project Riftwalker"><h1>Del equipo<br>al juego.</h1><p class="lead">Decisiones, evidencia y una experiencia para jugar.</p></div>`;
 case 'trailer':{const t=D.trailer;return `<div class="head row trailer-head"><p class="eyebrow">${eyebrow(s)}</p>${t.src?'<button class="primary" data-trailer>▶ Pantalla completa</button>':''}</div><div class="trailer">${t.src?`<video id="trailerVideo" src="${esc(t.src)}"${t.poster?` poster="${esc(t.poster)}"`:''} controls controlslist="nofullscreen" playsinline preload="metadata"></video><button class="trailer-exit" data-exit-trailer>Volver al recorrido</button>`:'<strong>Tráiler</strong>'}</div>`}
 case 'historia':return head(s)+story();
 case 'metricas':return head(s)+metrics();
 // Modos de juego: abre la selección de modo del juego; en cada andén, «Buscar partida» muestra cómo se juega (briefing.js).
 case 'menus':return head(s)+`<button class="menu-demo modos-demo" data-demo="modos"><span class="modos-tiras"><img src="../img/tactico-vista.jpg" alt=""><img src="media/historia/colonia.jpg" alt=""><img src="media/campus/campus-5.jpg" alt=""></span><span class="menu-demo-texto"><strong>Elegí el modo</strong><span>Táctico, Deathmatch e Invasión, como en el juego. En cada andén, «Buscar partida» muestra cómo se juega.</span><em>Abrir la selección de modo →</em></span></button>`;
 case 'diferencial':return head(s)+edge();
 case 'identidad':return head(s)+identity();
 case 'conjuntos':return head(s)+sets();
 case 'emotes':return head(s)+dances();
 case 'tiempo':return head(s)+statusTime();
 case 'arsenal':return head(s)+arsenal();
 case 'juego':return head(s)+`<button class="menu-demo" data-demo="menu"><img src="../menu/img/fondos/fondo-01.jpg" alt=""><span class="menu-demo-texto"><strong>Abrir el menú</strong><em>Abrir demo →</em></span></button>`;
 case 'negocio':return head(s)+offer();
 case 'escenarios':return head(s)+scenarios();
 case 'costos':return head(s)+costs();
 case 'fx':return head(s)+payers();
 case 'proteccion':return head(s)+guard();
 case 'sprint-1':case 'sprint-2':case 'sprint-3':case 'sprint-4':case 'sprint-5':return sprintSlide(s);
 case 'inversion':return head(s)+investment();
 case 'escala':return head(s)+scale();
 case 'despegue':return head(s)+boom();
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
  ['Modo Invasión', 'Invasión · 4 de las 27 cartas', 'invasion'],
  ['Cuenta', 'Tu cuenta Riftwalker', 'cuenta'],
  ['Launcher', 'El launcher de Riftwalker', 'launcher'],
  ['Arsenal', 'Cada arma cambia una decisión', 'arsenal'],
  ['Financiación', 'Planes de financiación', 'planes']
];
const proofImages = '../pruebas/idiomas/2026-11-08/';
// Los tres planes de Financiación (25/11/2026): salieron del recorrido y se ven en la burbuja «Financiación» del cierre,
// del peor caso al mejor. Cada uno es la diapositiva guardada (savedSlides) con su título y su dibujo.
const PLANES = [['Si no despega', 'proteccion', () => guard()], ['Si funciona', 'escala', () => scale()], ['Si despega fuerte', 'despegue', () => boom()]];
function planHTML(k) {
  const [, id, dibujo] = PLANES[k], s = (D.savedSlides || []).find(x => x.id === id);
  return `<h3>${esc(s ? s.title : PLANES[k][0])}</h3>${dibujo()}`;
}
// Agranda o achica el plan para que llene la ventana sin pasarse (como ajustarLetra en las diapositivas, pero también achica).
function ajustarPlan() {
  const caja = document.querySelector('#questionBody .qa-plan'), c = caja && caja.firstElementChild;
  if (!c) return;
  const cabe = z => { c.style.zoom = z; return c.getBoundingClientRect().height <= caja.clientHeight && c.scrollWidth <= c.clientWidth + 1; };
  let lo = 0.5, hi = 1.8;
  if (cabe(hi)) return;
  for (let i = 0; i < 9; i++) { const m = (lo + hi) / 2; if (cabe(m)) lo = m; else hi = m; }
  c.style.zoom = lo;
}
addEventListener('resize', () => requestAnimationFrame(ajustarPlan));
// Las dos capturas de la pregunta Logros: archivo, texto alternativo y nombre de la pestaña.
const LOGROS = [
  ['media/logros-progreso.png', 'Pestaña Logros: 7 de 25 logros obtenidos, con el avance de cada uno y el porcentaje de jugadores que lo tiene', 'Tu progreso en cada logro'],
  ['media/logros-ranking.png', 'Pestaña Ranking: ranking global del minijuego por cometas destruidos', 'Ranking global por modo']
];
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
    // Logros (25/11/2026): una captura por vez, a todo el ancho, con dos pestañas grandes para pasar de una a la otra.
    case 'logros': return `<div class="qa-actions qa-pestanas">${LOGROS.map(([,, nombre],k)=>`<button data-logro="${k}" aria-pressed="${!k}">${nombre}</button>`).join('')}</div><img id="logroPicture" class="qa-picture" src="${LOGROS[0][0]}" alt="${LOGROS[0][1]}"><div class="qa-actions"><strong>25 logros</strong><span>9 de cometas</span><span>16 de combate</span></div>`;
    case 'idiomas': {
      const flag = (code,name) => `<figure><img alt="" src="https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/svg/${code}.svg"><figcaption>${name}</figcaption></figure>`;
      return `<div class="qa-languages"><h3>En el juego y el launcher</h3><div>${flag('1f1e6-1f1f7','Español')}${flag('1f1ec-1f1e7','English')}${flag('1f1e7-1f1f7','Português')}${flag('1f1eb-1f1f7','Français')}${flag('1f1ee-1f1f9','Italiano')}</div><p class="qa-caption">Se elige en Opciones o en el launcher, y los dos quedan en el mismo idioma.</p></div>`;
    }
    case 'miras': return '<img class="qa-picture" src="media/mira-opciones.png" alt="Opciones de mira con forma, tamaño, color y vista previa sobre fondo claro y oscuro">';
    case 'invasion': {
      // Desde el 25/11/2026, solo cuatro cartas en grande (2 × 2): rareza, nombre, qué hace y con qué rinde.
      const rar={'Común':'comun','Rara':'rara','Épica':'epica','Legendaria':'legendaria'};
      return `<div class="qa-cartas">${D.cards.map(c=>`<article class="qa-carta ${rar[c.rarity]}"><span class="qa-rareza">${esc(c.rarity)}</span><strong>${esc(c.name)}</strong><p>${esc(c.text)}</p><small>${esc(c.with)} · ${c.max>1?'se acumula hasta 3 veces':'una sola vez'}</small></article>`).join('')}</div>`;
    }
    case 'planes': return `<div class="qa-actions qa-pestanas">${PLANES.map(([nombre], k) => `<button data-plan="${k}" aria-pressed="${!k}">${nombre}</button>`).join('')}</div><div class="qa-plan"><div>${planHTML(0)}</div></div>`;
    // La ficha de cada arma (antes, la diapositiva Arsenal): se elige con el desplegable y se vuelve a dibujar adentro del popup.
    case 'arsenal': return `<div class="qa-arsenal">${arsenal()}</div>`;
    case 'launcher': return '<img class="qa-picture" src="media/launcher.png" alt="Launcher de Project Riftwalker con las novedades de la versión 0.4.0 y el botón Jugar"><div class="qa-actions"><strong>Instala y actualiza</strong><span>Novedades de cada versión</span><span>Entrás con tu cuenta</span><span>Canal estable y experimental</span></div>';
    case 'cuenta': return '<div class="qa-account"><div><strong>Crear cuenta</strong><span>Mail y contraseña</span></div><div><strong>Ingresar con Steam</strong><span>Acceso previsto</span></div></div><img class="qa-picture" src="media/cuenta-confirmacion.png" alt="Correo de Project Riftwalker para confirmar el registro de una cuenta"><p class="qa-caption">Tu perfil, logros y monedas te acompañan al cambiar de PC.</p>';
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
  [49, 86, 11, 'M50 70 Q48 74 49 86'],
  [48, 11, -4, 'M50 34 C51 28 47 22 48 11'],
  [67, 88, 6, 'M57 69 C59 76 62 83 67 88'],
  [31, 88, -7, 'M43 69 C42 75 37 83 31 88']
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
  if (key === 'planes') requestAnimationFrame(ajustarPlan);
  $('#questionDialog').onclose = () => { $('#questionBody').replaceChildren(); };
}

// Letra más grande: en las diapositivas con lugar libre, todo lo que va debajo del título se agranda parejo (zoom)
// hasta llenar la pantalla, hasta 2,2 veces (25/11: para usar todo el alto en cualquier pantalla). Las que ya ocupan toda la altura (o son video o portada) quedan como están.
// Con mouse, las barras se esconden (ver barrasDesplegables): la letra se ajusta a la pantalla entera, así no salta al mostrarlas.
const BARRAS_OCULTAS=matchMedia('(hover:hover) and (pointer:fine)');
// Sin barras (con mouse), Diferencia e Identidad dejan su alto fijo y también se agrandan hasta llenar la pantalla.
const AJUSTAN_SIN_BARRAS=['diferencial'];
const SIN_AJUSTE=['inicio','trailer','diferencial','cierre'];
function envolverCuerpo(s){const m=$('#slide');if((SIN_AJUSTE.includes(s.id)&&!(BARRAS_OCULTAS.matches&&AJUSTAN_SIN_BARRAS.includes(s.id)))||!m.querySelector(':scope>.head'))return;const c=document.createElement('div');c.className='cuerpo';[...m.children].filter(e=>!e.classList.contains('head')).forEach(e=>c.append(e));m.append(c);ajustarLetra()}
function ajustarLetra(){const c=$('#slide>.cuerpo');if(!c)return;c.style.zoom='';const nav=document.querySelector('.navigation');const piso=BARRAS_OCULTAS.matches||!nav?innerHeight:nav.getBoundingClientRect().top;const limite=Math.min(piso-18,innerHeight-parseFloat(getComputedStyle($('#slide')).paddingBottom));const desborde=()=>[...c.querySelectorAll('*')].filter(e=>e.scrollWidth>e.clientWidth+2&&getComputedStyle(e).overflowX!=='visible').length;const base=desborde();const cabe=z=>{c.style.zoom=z;return c.getBoundingClientRect().bottom<=limite&&c.scrollWidth<=c.clientWidth+1&&desborde()<=base};if(!cabe(1)){c.style.zoom='';return}let lo=1,hi=2.2;if(cabe(hi))return;for(let i=0;i<8;i++){const m=(lo+hi)/2;if(cabe(m))lo=m;else hi=m}c.style.zoom=lo}
addEventListener('resize',()=>requestAnimationFrame(ajustarLetra));
new MutationObserver(()=>requestAnimationFrame(ajustarLetra)).observe(document.body,{attributes:true,attributeFilter:['style','class']});
document.fonts&&document.fonts.ready.then(ajustarLetra);
// Las imágenes que cargan después (armas, retratos) cambian el alto: se vuelve a ajustar cuando llega cada una.
$('#slide').addEventListener('load',e=>{if(e.target.tagName==='IMG')requestAnimationFrame(ajustarLetra)},true);
function render(focus=false){document.body.classList.remove('trailer-expanded');const s=D.slides[current];$('#slide').innerHTML=content(s);envolverCuerpo(s);const questions=s.id==='cierre';if(questions&&running)$('#timerToggle').click();$('#timerToggle').disabled=questions;$('#position').textContent=`${current+1} / ${D.slides.length}`;$('#prev').disabled=current===0;$('#next').disabled=current===D.slides.length-1;$('#progress').style.width=`${(current+1)/D.slides.length*100}%`;$('#speaker').textContent=s.speaker||D.speaker;$('#announcement').textContent=`${current+1} de ${D.slides.length}: ${s.title}`;history.replaceState(null,'','#'+s.id);if(focus){$('#slide').focus({preventScroll:true});scrollTo(0,0);$('#slideScroll').scrollTop=0}}
function go(n){current=Math.max(0,Math.min(D.slides.length-1,n));render(true)}
$('#edition').textContent=D.edition;
$('#indexGrid').innerHTML=D.slides.map((s,i)=>`${s.section!==D.slides[i-1]?.section?`<h3 class="index-section">${esc(s.section)}</h3>`:''}<button data-go="${i}">${String(i+1).padStart(2,'0')} / ${esc(s.label)}<small>${s.seconds?s.seconds+' s':s.minutes+' min'} · ${esc(s.title)}</small></button>`).join('');
$('#prev').onclick=()=>go(current-1);$('#next').onclick=()=>go(current+1);
$('#indexButton').onclick=()=>$('#indexDialog').showModal();
$('#contrast').onclick=()=>{const on=document.body.classList.toggle('projector');$('#contrast').setAttribute('aria-pressed',on)};
$('#fullscreen').onclick=async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen()}catch{$('#announcement').textContent='El navegador no permitió pantalla completa. Podés usar F11.'}};
// El menú, el perfil y la tienda son una página aparte (/menu); las otras demos son maquetas de la guía.
const demoSrc=id=>id==='menu'?'../menu/index.html?v=2026112411':`../index.html?demo=${id}${id==='modos'?'&briefing=1&v=2026112503':''}`;
let demo='';function openDemo(id){demo=id;$('#demoTitle').textContent={tienda:'Tienda de armas',modos:'Selección de modo',hud:'HUD de combate',menu:'Menú, perfil y tienda'}[id];$('#demoSub').textContent=id==='menu'?'El menú, el perfil y la tienda del juego, en la página':'Maqueta interactiva de la guía';$('#demoFrame').src=demoSrc(id);$('#demoDialog').showModal()}
$('#resetDemo').onclick=()=>{$('#demoFrame').src=demoSrc(demo)};
$('#demoDialog').addEventListener('close',()=>{$('#demoFrame').src='about:blank'});
$('#clipDialog').addEventListener('close',()=>{$('#video').pause();$('#video').removeAttribute('src');$('#video').load()});
document.addEventListener('click',e=>{const b=e.target.closest('button');if(!b)return;const d=b.dataset;
 if(d.close)$('#'+d.close).close();
 if(d.go!==undefined){$('#indexDialog').close();go(+d.go)}

 if(d.photo){campusPhoto=(campusPhoto+Number(d.photo)+campusPhotos.length)%campusPhotos.length;mapPhoto()}
 if(d.logro!==undefined){const [src,alt]=LOGROS[+d.logro];$('#logroPicture').src=src;$('#logroPicture').alt=alt;for(const x of document.querySelectorAll('[data-logro]'))x.setAttribute('aria-pressed',x===b)}
 if(d.plan!==undefined){$('#questionBody .qa-plan>div').innerHTML=planHTML(+d.plan);for(const x of document.querySelectorAll('[data-plan]'))x.setAttribute('aria-pressed',x===b);requestAnimationFrame(ajustarPlan)}
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
document.addEventListener('change',e=>{if(e.target.id==='weaponSelect'){weaponId=e.target.value;if(e.target.closest('#questionDialog')){$('#questionBody').innerHTML=questionVisual('arsenal');$('#weaponSelect').focus()}else render()}});
document.addEventListener('keydown',e=>{if(document.body.classList.contains('trailer-expanded')){if(e.key==='Escape'){e.preventDefault();closeTrailer()}return}if(document.querySelector('dialog[open]')||document.fullscreenElement?.tagName==='VIDEO'||e.target.closest('input,select,textarea,button,a,video'))return;
 if(e.key==='ArrowRight'||e.key==='PageDown'){e.preventDefault();go(current+1)}if(e.key==='ArrowLeft'||e.key==='PageUp'){e.preventDefault();go(current-1)}if(e.key.toLowerCase()==='f')$('#fullscreen').click();
});
// Child demos use their own keyboard; only an explicit return message closes them.
addEventListener('message',e=>{if(e.origin===location.origin&&e.source===$('#demoFrame').contentWindow&&e.data==='riftwalker:close-demo')$('#demoDialog').close()});
let running=false,elapsed=0,last=0;const duration = 20 * 60;
function clock(){const total=elapsed+(running?(performance.now()-last)/1000:0),remaining=duration-total,seconds=Math.ceil(Math.abs(remaining));$('#timer').textContent=`${remaining<0?'+':''}${String(Math.floor(seconds/60)).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;$('#timer').classList.toggle('over',remaining<0)}
$('#timerToggle').onclick=()=>{if(running)elapsed+=(performance.now()-last)/1000;else last=performance.now();running=!running;$('#timerToggle').textContent=running?'Ⅱ':'▶';$('#timerToggle').title=running?'Pausar el tiempo':'Iniciar el tiempo';$('.clock').classList.toggle('corriendo',running);clock()};
$('#timerReset').onclick=()=>{elapsed=0;last=performance.now();clock()};setInterval(clock,200);
const initial=D.slides.findIndex(s=>'#'+s.id===location.hash);current=initial<0?0:initial;render();clock();

addEventListener('hashchange',()=>{const i=D.slides.findIndex(s=>'#'+s.id===location.hash);if(i>=0&&i!==current)go(i)});

// Barras desplegables (25/11/2026): el aula proyecta a 1024 × 768 por VGA, así que la diapositiva usa toda la pantalla.
// La barra de arriba y la de abajo aparecen al llevar el mouse a su borde y se esconden al alejarlo; con Tab se ven
// por el foco (CSS). El reloj queda siempre arriba a la derecha y baja cuando aparece la barra de arriba.
(function barrasDesplegables(){
 const arriba=$('.top'),abajo=$('.navigation'),reloj=$('.clock');let verArriba=false,verAbajo=false;
 function poner(a,b){
  if(a!==verArriba){verArriba=a;arriba.classList.toggle('visible',a);reloj.classList.toggle('bajo',a)}
  if(b!==verAbajo){verAbajo=b;abajo.classList.toggle('visible',b)}
 }
 // Con «Ajustar al proyector» la página entera se achica y se mueve (body.fitted): las zonas se miden sobre la imagen
 // ajustada, no sobre el borde de la pantalla, así la barra no se esconde antes de llegar a ella (25/11/2026).
 function bordes(){
  if(!document.body.classList.contains('fitted'))return {sup:0,inf:innerHeight,k:1};
  const r=document.body.getBoundingClientRect();return {sup:r.top,inf:r.bottom,k:r.height/innerHeight};
 }
 addEventListener('mousemove',e=>{if(!BARRAS_OCULTAS.matches)return;const y=e.clientY,{sup,inf,k}=bordes();
  poner(verArriba?y<sup+(arriba.offsetHeight+16)*k:(y<40||y<sup+40*k),verAbajo?y>inf-(abajo.offsetHeight+16)*k:(y>innerHeight-40||y>inf-40*k))});
 document.documentElement.addEventListener('mouseleave',()=>poner(false,false));
})();
