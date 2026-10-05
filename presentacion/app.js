'use strict';
const D=window.PRESENTATION, $=s=>document.querySelector(s);
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let current=0,metric='s1',decision=0,business=0,weaponId='fusil',shield=50;
const tag=(text,pending=false)=>`<span class="tag ${pending?'pending':''}">${esc(text)}</span>`;
const card=(title,text)=>`<article class="card"><h3>${esc(title)}</h3><p>${esc(text)}</p></article>`;
const head=s=>`<div class="head"><p class="eyebrow">${String(current+1).padStart(2,'0')} / ${esc(s.label)} · ${s.minutes} min</p><h2>${esc(s.title)}</h2></div>`;
const buttons=(items,active,attribute)=>`<div class="tabs">${items.map((x,i)=>`<button ${attribute}="${i}" aria-pressed="${i===active}">${esc(x.name)}</button>`).join('')}</div>`;
function metrics(){
 const m=D.metrics[metric];
 let body;
 if(['planned','done','review','todo','tests','passed','failed','blocked','unrun'].some(k=>m[k]==null)) body='<div class="pending-box"><strong>Acá van los resultados del Sprint 3</strong><p>Historias entregadas · trabajo pendiente · resultados de pruebas</p><div class="rail"></div><p>Los datos del Sprint 1 quedan disponibles como referencia.</p></div>';
 else {
 const percent=(m.planned ? 100*m.done/m.planned : 0).toLocaleString('es-AR',{maximumFractionDigits:1});
 const success=(m.tests>m.unrun ? 100*m.passed/(m.tests-m.unrun) : 0).toLocaleString('es-AR',{maximumFractionDigits:1});
 body=`<div class="grid two"><article class="card"><p class="eyebrow">Entrega</p><div class="metric-number">${m.done}<small> / ${m.planned} US</small></div><p>${percent} % en Done</p><div class="bars" role="img" aria-label="${m.done} Done, ${m.review} en revisión, ${m.todo} sin empezar"><span style="width:${m.done/Math.max(1,m.planned)*100}%;background:var(--green)"></span><span style="width:${m.review/Math.max(1,m.planned)*100}%;background:var(--accent)"></span><span style="flex:1;background:var(--line)"></span></div><div class="legend"><span>${m.review} en revisión</span><span>${m.todo} sin empezar</span></div></article><article class="card"><p class="eyebrow">Calidad</p><div class="metric-number">${m.passed}<small> / ${m.tests-m.unrun} ejecutados</small></div><p>${success} % de pruebas exitosas</p><div class="bars" role="img" aria-label="${m.passed} exitosas, ${m.failed} errores, ${m.blocked} bloqueadas, ${m.unrun} sin ejecutar"><span style="width:${m.passed/Math.max(1,m.tests)*100}%;background:var(--green)"></span><span style="width:${m.failed/Math.max(1,m.tests)*100}%;background:var(--red)"></span><span style="width:${m.blocked/Math.max(1,m.tests)*100}%;background:var(--accent)"></span><span style="flex:1;background:var(--line)"></span></div><div class="legend"><span>${m.failed} errores</span><span>${m.blocked} bloqueada</span><span>${m.unrun} sin ejecutar</span></div></article></div>`;
 }
 return `<div class="tabs"><button data-metric="s1" aria-pressed="${metric==='s1'}">Sprint 1 · referencia</button><button data-metric="s3" aria-pressed="${metric==='s3'}">Sprint 3${D.metrics.s3.planned==null?' · pendiente':''}</button></div>${body}<p class="footnote">${esc(m.name)} · Fuente: ${esc(m.source||(metric==='s1'?D.source:'Pendiente de cargar el corte y la fuente del Sprint 3'))}. Los datos se actualizan por el equipo.</p>`;
}
function arsenal(){
 const w=window.ARSENAL.find(x=>x.id===weaponId)||window.ARSENAL[0],t=w.tramos[0];
 return `<div class="row" style="margin-bottom:20px"><label for="weaponSelect" class="sr">Elegir arma</label><select id="weaponSelect">${window.ARSENAL.map(x=>`<option value="${esc(x.id)}" ${x.id===w.id?'selected':''}>${esc(x.alias)} · ${esc(x.nombre)}</option>`).join('')}</select>${tag('Ficha de la guía · validar para S3',true)}</div><div class="weapon"><div class="weapon-art"><img src="../img/${esc(w.icono)}.png" alt="${esc(w.alias)}"></div><div><p class="eyebrow">${esc(w.modo)}</p><h3>${esc(w.alias)}</h3><div class="stats"><div><b>${w.precio.toLocaleString('es-AR')}</b><span>monedas de partida</span></div><div><b>${w.cargador}</b><span>balas / cargador</span></div><div><b>${w.recarga.toLocaleString('es-AR')} s</b><span>recarga</span></div><div><b>${t.cabeza}</b><span>daño · cabeza</span></div><div><b>${t.cuerpo}</b><span>daño · cuerpo</span></div><div><b>${Math.ceil((100+shield)/(t.cuerpo*(w.perdigones||1)))}</b><span>impactos al cuerpo*</span></div></div></div></div><div class="row" style="margin-top:20px"><span>Rival: 100 de vida +</span>${[0,25,50].map(s=>`<button data-shield="${s}" aria-pressed="${shield===s}">${s} de escudo</button>`).join('')}</div><p class="footnote">*Primer tramo: ${t.hasta==null?'cualquier distancia':`0–${t.hasta} m`}. Escopeta: todos los perdigones impactan. Datos de la guía al 05/10/2026; confirmar en la build del Sprint 3.</p>`;
}
function content(s){
 switch(s.id){
 case 'inicio':return `<div class="hero"><p class="eyebrow">Laboratorio de Construcción de Software · Sprint 3</p><img class="hero-logo" src="media/logo.png" alt="Project Riftwalker"><h1>Del equipo<br>al juego.</h1><p class="lead">Decisiones, evidencia y una experiencia para jugar.</p><div class="row">${tag('20 minutos')}${tag('Presentación en construcción',true)}</div></div>`;
 case 'producto':return head(s)+`<div class="grid"><article class="card"><span class="big">4 vs 4</span><h3>Coordinar</h3><p>Táctico por equipos.<br>Un objetivo compartido.</p></article><article class="card"><span class="big">Deathmatch</span><h3>Dominar</h3><p>Duelo individual.<br>Adaptarse y acertar.</p></article><article class="card"><span class="big">Oleadas</span><h3>Resistir</h3><p>La invasión dimensional.<br>Superar el propio récord.</p></article></div><p class="lead">El entrenamiento une a los Riftwalkers. La invasión los pone a prueba.</p><p class="footnote">Visión de producto · disponibilidad y reglas definitivas a confirmar para Sprint 3.</p>`;
 case 'equipo':return head(s)+`<div class="row" style="margin-bottom:20px">${tag('Informe S1 · acuerdos y acciones para S2')}${tag('Ratificar para S3',true)}</div><div class="grid two">${D.agreements.map(x=>card(...x)).join('')}</div><p class="footnote">Por completar: integrantes, roles y un ejemplo de cómo estos acuerdos cambiaron el trabajo.</p>`;
 case 'metricas':return head(s)+metrics();
 case 'decisiones':{const d=D.decisions[decision];return head(s)+buttons(D.decisions,decision,'data-decision')+`<div class="decision"><article class="card"><small>EL PROBLEMA</small><h3>${esc(d.problem)}</h3></article><span class="arrow">→</span><article class="card"><small>LA DECISIÓN</small><h3>${esc(d.choice)}</h3></article><span class="arrow">→</span><article class="card"><small>EL BENEFICIO BUSCADO</small><h3>${esc(d.gain)}</h3></article></div><p class="footnote">${esc(d.source)} · sumar evidencia del resultado en Sprint 3.</p>`;}
 case 'menus':return head(s)+`<div class="grid"><button class="demo-card" data-demo="tienda"><strong>01 / Tienda</strong><span>Elegir, comparar y comprar.</span><em>Abrir demo →</em></button><button class="demo-card" data-demo="modos"><strong>02 / Modos</strong><span>Tres andenes. Una elección.</span><em>Abrir demo →</em></button><button class="demo-card" data-demo="hud"><strong>03 / HUD</strong><span>Información durante el combate.</span><em>Abrir demo →</em></button></div><p class="lead">Primero lo probamos como maqueta.<br>Después lo llevamos a Unity.</p><p class="footnote">Demos web de la guía. No representan por sí solas el estado de la build.</p>`;
 case 'arsenal':return head(s)+arsenal();
 case 'juego':return head(s)+`<div class="grid">${D.clips.map((c,i)=>`<article class="card"><h3>${esc(c.title)}</h3><p>${esc(c.detail)}</p><div style="margin-top:28px">${c.src?`<button data-clip="${i}">Reproducir clip →</button>`:tag('Clip pendiente',true)}</div></article>`).join('')}</div><div class="pending-box" style="margin-top:24px"><strong>Espacio reservado para la demo real</strong><p>Una secuencia corta del Sprint 3 y un video de respaldo.</p></div>`;
 case 'negocio':{const b=D.business[business];return head(s)+`<div class="row" style="margin-bottom:22px">${tag('Propuesta comercial · no implementada',true)}${tag('Cosméticos, sin ventajas competitivas · propuesta')}</div>`+buttons(D.business,business,'data-business')+`<div class="business"><div><p class="eyebrow">Free to play</p><p class="pitch">Jugar con amigos<br>sin pagar una entrada.</p><div class="rail"></div><p class="lead">La personalización sostiene la propuesta comercial.</p></div><article class="card"><p class="eyebrow">${esc(b.name)}</p><h3>${esc(b.headline)}</h3><p>${esc(b.text)}</p><span class="big">${esc(b.model)}</span><p class="footnote">${esc(b.pending)}</p></article></div>`;}
 case 'cierre':return head(s)+`<div class="timeline"><span>SPRINT 1<br>Núcleo jugable</span><span>SPRINT 2<br>Integración</span><span>SPRINT 3<br>Evidencia y demo</span></div><div class="grid two">${card('Lo que aprendimos','Completar con dos aprendizajes del Sprint 3.')}${card('Lo que viene','Confirmar próximos objetivos y propuesta comercial.')}</div><p class="pitch">Construir. Probar. Aprender.</p><p class="footnote">Preguntas · <a href="../index.html">Explorar la guía de diseño</a></p>`;
 }
}
function render(focus=false){const s=D.slides[current];$('#slide').innerHTML=content(s);$('#position').textContent=`${current+1} / ${D.slides.length}`;$('#prev').disabled=current===0;$('#next').disabled=current===D.slides.length-1;$('#progress').style.width=`${(current+1)/D.slides.length*100}%`;$('#note').textContent=s.note;$('#announcement').textContent=`${current+1} de ${D.slides.length}: ${s.title}`;history.replaceState(null,'','#'+s.id);if(focus){$('#slide').focus({preventScroll:true});scrollTo(0,0)}}
function go(n){current=Math.max(0,Math.min(D.slides.length-1,n));render(true)}
$('#edition').textContent=D.edition;
$('#indexGrid').innerHTML=D.slides.map((s,i)=>`<button data-go="${i}">${String(i+1).padStart(2,'0')} / ${esc(s.label)}<small>${s.minutes} min · ${esc(s.title)}</small></button>`).join('');
$('#prev').onclick=()=>go(current-1);$('#next').onclick=()=>go(current+1);
$('#indexButton').onclick=()=>$('#indexDialog').showModal();
$('#notesButton').onclick=()=>{$('#notesDialog').showModal();$('#notesButton').setAttribute('aria-expanded','true')};
$('#notesDialog').addEventListener('close',()=>$('#notesButton').setAttribute('aria-expanded','false'));
$('#contrast').onclick=()=>{const on=document.body.classList.toggle('projector');$('#contrast').setAttribute('aria-pressed',on)};
$('#fullscreen').onclick=async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen()}catch{$('#announcement').textContent='El navegador no permitió pantalla completa. Podés usar F11.'}};
function blackout(on){$('#blackout').hidden=!on;if(on)$('#unblank').focus();else $('#blank').focus()}
$('#blank').onclick=()=>blackout(true);$('#unblank').onclick=()=>blackout(false);
let demo='';function openDemo(id){demo=id;$('#demoTitle').textContent={tienda:'Tienda de armas',modos:'Selección de modo',hud:'HUD de combate'}[id];$('#demoFrame').src=`../index.html?demo=${id}`;$('#demoDialog').showModal()}
$('#resetDemo').onclick=()=>{$('#demoFrame').src=`../index.html?demo=${demo}`};
$('#demoDialog').addEventListener('close',()=>{$('#demoFrame').src='about:blank'});
$('#clipDialog').addEventListener('close',()=>{$('#video').pause();$('#video').removeAttribute('src');$('#video').load()});
document.addEventListener('click',e=>{const b=e.target.closest('button');if(!b)return;const d=b.dataset;
 if(d.close)$('#'+d.close).close();
 if(d.go!==undefined){$('#indexDialog').close();go(+d.go)}
 if(d.metric){metric=d.metric;render()}
 if(d.decision!==undefined){decision=+d.decision;render()}
 if(d.business!==undefined){business=+d.business;render()}
 if(d.shield!==undefined){shield=+d.shield;render()}
 if(d.demo)openDemo(d.demo);
 if(d.clip!==undefined){const c=D.clips[+d.clip];if(c.src){$('#clipTitle').textContent=c.title;$('#video').src=c.src;$('#clipDialog').showModal()}}
});
document.addEventListener('change',e=>{if(e.target.id==='weaponSelect'){weaponId=e.target.value;render()}});
document.addEventListener('keydown',e=>{if(!$('#blackout').hidden){if(e.key==='Escape'||e.key.toLowerCase()==='b')blackout(false);return}if(document.querySelector('dialog[open]')||e.target.closest('input,select,textarea,button,a'))return;
 if(e.key==='ArrowRight'||e.key==='PageDown'){e.preventDefault();go(current+1)}if(e.key==='ArrowLeft'||e.key==='PageUp'){e.preventDefault();go(current-1)}if(e.key.toLowerCase()==='b')blackout(true);if(e.key.toLowerCase()==='f')$('#fullscreen').click();
});
// Child demos use their own keyboard; only an explicit return message closes them.
addEventListener('message',e=>{if(e.origin===location.origin&&e.source===$('#demoFrame').contentWindow&&e.data==='riftwalker:close-demo')$('#demoDialog').close()});
let running=false,elapsed=0,last=0;const duration=D.slides.reduce((sum,s)=>sum+s.minutes*60,0);
function clock(){const total=elapsed+(running?(performance.now()-last)/1000:0),remaining=duration-total,seconds=Math.ceil(Math.abs(remaining));$('#timer').textContent=`${remaining<0?'+':''}${String(Math.floor(seconds/60)).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;$('#timer').classList.toggle('over',remaining<0)}
$('#timerToggle').onclick=()=>{if(running)elapsed+=(performance.now()-last)/1000;else last=performance.now();running=!running;$('#timerToggle').textContent=running?'Ⅱ Pausar':'▶ Ensayar';clock()};
$('#timerReset').onclick=()=>{elapsed=0;last=performance.now();clock()};setInterval(clock,200);
const initial=D.slides.findIndex(s=>'#'+s.id===location.hash);current=initial<0?0:initial;render();clock();

addEventListener('hashchange',()=>{const i=D.slides.findIndex(s=>'#'+s.id===location.hash);if(i>=0&&i!==current)go(i)});
