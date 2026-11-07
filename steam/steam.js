'use strict';
(() => {
  const $ = selector => document.querySelector(selector);
  const $$ = selector => [...document.querySelectorAll(selector)];
  const launcher = 'https://github.com/DogeGGss/lcs-tp-principal/releases/download/launcher/Riftwalker-Launcher.exe';
  const media = [
    {src:'assets/trailer-poster.jpg',alt:'Los cuatro personajes de Riftwalker dentro de un tren',kind:'TRÁILER DEL PROYECTO',caption:'Tráiler del proyecto universitario.',video:true},
    {src:'assets/tactico-vista.jpg',alt:'Vista del mapa Táctico MALVINAS-01',kind:'CAPTURA DEL PROYECTO',caption:'MALVINAS-01 · Escenario del modo Táctico.'},
    {src:'assets/deathmatch-vista.jpg',alt:'Colonia ARA San Juan, mapa de Deathmatch',kind:'CAPTURA DEL PROYECTO',caption:'Colonia ARA San Juan · Escenario de Deathmatch.'},
    {src:'assets/tactico-planta-baja.jpg',alt:'Planta baja del escenario orbital MALVINAS-01',kind:'CAPTURA DEL PROYECTO',caption:'MALVINAS-01 · Recorridos y conexiones de la planta baja.'},
    {src:'assets/tactico-nivel-alto.jpg',alt:'Nivel superior del escenario MALVINAS-01',kind:'CAPTURA DEL PROYECTO',caption:'MALVINAS-01 · Nivel superior.'},
    {src:'assets/equipo.jpg',alt:'Arte promocional de Goya y Necochea en una estación',kind:'ARTE PROMOCIONAL',caption:'Ilustración del universo de Riftwalker · No es una captura de gameplay.'}
  ];
  const video = $('#gallery-video');
  let currentMedia = 0, toastTimer;
  const safeRead = key => { try { return localStorage.getItem('rw-steam-demo:'+key); } catch { return null; } };
  const safeWrite = (key,value) => { try { localStorage.setItem('rw-steam-demo:'+key,value); } catch {} };
  function toast(message) { clearTimeout(toastTimer); $('#toast').textContent=message; $('#toast').hidden=false; toastTimer=setTimeout(()=>{$('#toast').hidden=true;},4200); }
  function showMedia(index) {
    currentMedia=(index+media.length)%media.length;
    const item=media[currentMedia];
    video.pause();video.hidden=true;$('#gallery-image').hidden=false;
    $('#gallery-image').src=item.src;$('#gallery-image').alt=item.alt;
    $('#play-trailer').hidden=!item.video;$('#expand-media').hidden=!!item.video;
    $('#media-kind').hidden=false;$('#media-kind').textContent=item.kind;$('#media-description').textContent=item.caption;
    $$('.thumbnail').forEach((button,i)=>{button.classList.toggle('selected',i===currentMedia);button.setAttribute('aria-pressed',String(i===currentMedia));});
    const chosen=$$('.thumbnail')[currentMedia], strip=$('#thumbnails');
    if(chosen.offsetLeft<strip.scrollLeft||chosen.offsetLeft+chosen.offsetWidth>strip.scrollLeft+strip.clientWidth) strip.scrollTo({left:chosen.offsetLeft-strip.offsetLeft,behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'instant':'smooth'});
  }
  $$('.thumbnail').forEach(button=>button.addEventListener('click',()=>showMedia(Number(button.dataset.media))));
  $('#gallery-prev').addEventListener('click',()=>showMedia(currentMedia-1));
  $('#gallery-next').addEventListener('click',()=>showMedia(currentMedia+1));
  $('#media-stage').addEventListener('keydown',event=>{if(event.target===video)return;if(event.key==='ArrowLeft'||event.key==='ArrowRight'){event.preventDefault();showMedia(currentMedia+(event.key==='ArrowRight'?1:-1));}});
  $('#play-trailer').addEventListener('click',()=>{
    if(!video.querySelector('source').src){video.querySelector('source').src=video.querySelector('source').dataset.src;video.load();}
    $('#gallery-image').hidden=true;$('#play-trailer').hidden=true;$('#media-kind').hidden=true;video.hidden=false;
    video.play().catch(()=>toast('Usá el control de reproducción para iniciar el tráiler.'));
  });
  $('#expand-media').addEventListener('click',()=>{$('#lightbox-image').src=media[currentMedia].src;$('#lightbox-image').alt=media[currentMedia].alt;$('#lightbox-caption').textContent=media[currentMedia].caption;$('#image-dialog').showModal();});
  $('.image-dialog-close').addEventListener('click',()=>$('#image-dialog').close());
  $('#image-dialog').addEventListener('click',event=>{if(event.target===event.currentTarget)event.currentTarget.close();});
  const toggleConfig={wishlist:{off:'＋ Añadir a tu lista de deseados',on:'✓ En tu lista de deseados',added:'Añadido a tus deseados de demostración.',removed:'Eliminado de tus deseados de demostración.'},follow:{off:'Seguir',on:'✓ Siguiendo',added:'Ahora seguís el juego en esta simulación.',removed:'Dejaste de seguir el juego en esta simulación.'},ignore:{off:'Ignorar ▾',on:'✓ Ignorado',added:'Juego ignorado en esta simulación.',removed:'El juego vuelve a mostrarse en esta simulación.'}};
  function setToggle(key,value,announce=false){const button=$('#'+key),config=toggleConfig[key];button.setAttribute('aria-pressed',String(value));button.textContent=value?config.on:config.off;safeWrite(key,String(value));if(key==='wishlist'&&$('#publication-view').value==='upcoming')$('#purchase-action').textContent=value?'✓ En tus deseados':'Añadir a deseados';if(announce)toast(value?config.added:config.removed);}
  for(const key of Object.keys(toggleConfig)){setToggle(key,safeRead(key)==='true');$('#'+key).addEventListener('click',()=>setToggle(key,$('#'+key).getAttribute('aria-pressed')!=='true',true));}
  const dialog=$('#info-dialog');
  const dialogs={
    steam:['Instalar Steam','<p>Este control forma parte de la simulación académica. No instala Steam ni abre el cliente.</p><p>Para probar nuestro juego, descargá su launcher desde GitHub.</p>'],
    account:['Cuenta de demostración','<p>Esta página no inicia sesión en Steam y no solicita usuario, contraseña ni datos personales.</p><p>Podés usar deseados y seguir sin una cuenta: las elecciones se guardan únicamente en este navegador.</p>'],
    support:['Soporte del proyecto','<p>Para informar un problema real de Project Riftwalker, usá las issues de su repositorio.</p><p><a class="blue-button" href="https://github.com/DogeGGss/lcs-tp-principal/issues" target="_blank" rel="noopener noreferrer">Abrir issues en GitHub ↗</a></p>'],
    language:['Idioma de la simulación','<p>Esta ficha está preparada en español para la presentación universitaria.</p><p>La vista no cambia preferencias de Steam ni del navegador.</p>'],
    community:['Comunidad de Project Riftwalker','<p>El centro de la comunidad de Steam está representado de forma conceptual.</p><p>El desarrollo real, las versiones y las novedades se pueden consultar en el repositorio del proyecto.</p><a class="blue-button" href="https://github.com/DogeGGss/lcs-tp-principal" target="_blank" rel="noopener noreferrer">Ver proyecto ↗</a>'],
    tags:['Etiquetas de la ficha','<div class="dialog-tags"><span>FPS</span><span>Multijugador</span><span>Ciencia ficción</span><span>Táctico</span><span>Acción</span><span>Por equipos</span><span>Primera persona</span><span>Personalización</span></div><p>Etiquetas propuestas para esta presentación; no proceden de usuarios reales de Steam.</p>'],
    play:['Jugar a Project Riftwalker','<p>Estás viendo el estado «Juego lanzado» de una ficha simulada. No hay una instalación ni un producto de Steam asociados.</p><p>La versión universitaria real se puede instalar con nuestro launcher de GitHub.</p><a class="blue-button launcher-download" href="'+launcher+'">⇩ Descargar launcher del proyecto</a>'],
    share:['Compartir la simulación','<p>Compartí la dirección de esta página. Es una demostración universitaria, no un enlace a un producto publicado en Steam.</p><p id="share-url"></p>']
  };
  function showDialog(name){const content=dialogs[name];if(!content)return;$('#dialog-title').textContent=content[0];$('#dialog-content').innerHTML=content[1];if(name==='share')$('#share-url').textContent=location.href;dialog.showModal();}
  $$('[data-dialog]').forEach(button=>button.addEventListener('click',()=>showDialog(button.dataset.dialog)));
  $$('.dialog-close,.dialog-done').forEach(button=>button.addEventListener('click',()=>dialog.close()));
  dialog.addEventListener('click',event=>{if(event.target===dialog)dialog.close();});
  function updatePublication(){const released=$('#publication-view').value==='released';$('#release-status').textContent=released?'Vista de lanzamiento simulada':'Próximamente';$('#purchase-title').textContent=released?'Jugar a Project Riftwalker':'Próximamente';$('#purchase-description').textContent=released?'Una vista de cómo se vería la ficha al publicar el juego. Disponibilidad y precio ilustrativos.':'Este sería el lugar para anunciar el lanzamiento y reunir a los próximos pasajeros.';$('#purchase-price').textContent=released?'Free to Play · Simulado':'Fecha de lanzamiento por anunciar';$('#purchase-action').textContent=released?'Jugar':($('#wishlist').getAttribute('aria-pressed')==='true'?'✓ En tus deseados':'Añadir a deseados');}
  $('#publication-view').addEventListener('change',updatePublication);
  $('#purchase-action').addEventListener('click',()=>{$('#publication-view').value==='released'?showDialog('play'):setToggle('wishlist',$('#wishlist').getAttribute('aria-pressed')!=='true',true);});
  $('#share').addEventListener('click',async()=>{try{if(!navigator.clipboard)throw Error();await navigator.clipboard.writeText(location.href);toast('Enlace de la simulación copiado.');}catch{showDialog('share');}});
  const trigger=$('.menu-trigger'),popover=$('#store-popover');
  trigger.addEventListener('click',()=>{popover.hidden=!popover.hidden;trigger.setAttribute('aria-expanded',String(!popover.hidden));});
  popover.addEventListener('click',event=>{if(event.target.closest('a')){popover.hidden=true;trigger.setAttribute('aria-expanded','false');}});
  document.addEventListener('click',event=>{if(!event.target.closest('.menu-trigger')&&!event.target.closest('#store-popover')){popover.hidden=true;trigger.setAttribute('aria-expanded','false');}if(!event.target.closest('#store-search')&&!event.target.closest('#search-result'))$('#search-result').hidden=true;});
  document.addEventListener('keydown',event=>{if(event.key==='Escape'){popover.hidden=true;trigger.setAttribute('aria-expanded','false');$('#search-result').hidden=true;}});
  $('#store-search').addEventListener('submit',event=>{event.preventDefault();const query=$('#search-input').value.trim().toLocaleLowerCase('es');const box=$('#search-result');box.replaceChildren();const message=document.createElement('span');message.textContent=!query?'Escribí el nombre del juego.':('project riftwalker'.includes(query)?'1 resultado: Project Riftwalker':'Esta tienda de demostración contiene únicamente Project Riftwalker.');box.append(message);if(query){const button=document.createElement('button');button.textContent='Ver Project Riftwalker';button.addEventListener('click',()=>{box.hidden=true;$('#producto').scrollIntoView({behavior:'smooth'});});box.append(button);}box.hidden=false;});
  $$('[data-tag]').forEach(button=>button.addEventListener('click',()=>showDialog('tags')));
  const characters={soledad:{name:'Soledad',role:'DUELISTA',ability:'Trasbordo',description:'Reposicionate con un trasbordo para sorprender desde otro ángulo.',portrait:'assets/astra.png',color:'#73d9ff'},unzue:{name:'Unzué',role:'CENTINELA',ability:'Barrera',description:'Levantá una barrera para cortar líneas de tiro y darle cobertura a tu equipo.',portrait:'assets/alfa.png',color:'#f4c338'},necochea:{name:'Necochea',role:'EXPLORADOR',ability:'Vista aérea',description:'Explorá desde arriba con un dron para leer el terreno antes de avanzar.',portrait:'assets/bravo.png',color:'#63d77c'},goya:{name:'Goya',role:'INICIADOR',ability:'Callejero',description:'Soltá a tu compañero para buscar y presionar a los rivales.',portrait:'assets/charlie.png',color:'#f29a38'}};
  function selectCharacter(key,focus=false){const c=characters[key];if(!c)return;$$('[data-character]').forEach(button=>{const selected=button.dataset.character===key;button.setAttribute('aria-selected',String(selected));button.tabIndex=selected?0:-1;if(selected&&focus)button.focus();});$('#character-name').textContent=c.name;$('#character-role').textContent=c.role;$('#character-role').style.color=c.color;$('#character-ability').textContent=c.ability;$('#character-description').textContent=c.description;$('#character-portrait').src=c.portrait;$('#character-portrait').alt=c.name;$('#character-panel').setAttribute('aria-labelledby','tab-'+key);$('#character-panel').style.borderColor=c.color;}
  const characterTabs=$$('[data-character]');
  characterTabs.forEach((button,index)=>{button.addEventListener('click',()=>selectCharacter(button.dataset.character));button.addEventListener('keydown',event=>{let next;if(event.key==='ArrowRight')next=(index+1)%characterTabs.length;if(event.key==='ArrowLeft')next=(index+characterTabs.length-1)%characterTabs.length;if(event.key==='Home')next=0;if(event.key==='End')next=characterTabs.length-1;if(next!==undefined){event.preventDefault();selectCharacter(characterTabs[next].dataset.character,true);}});});
  document.addEventListener('visibilitychange',()=>{if(document.hidden)video.pause();});
  addEventListener('pageshow',updatePublication);
  updatePublication();
})();
