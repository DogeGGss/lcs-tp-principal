// Reuse the guide's live demos, while retaining all DOM nodes their scripts expect.
(()=>{
 const id=new URLSearchParams(location.search).get('demo');
 if(!['tienda','modos','hud'].includes(id))return;
 document.documentElement.dataset.theme='dark';
 document.body.classList.add('presentation-demo');
 document.getElementById(id).classList.add('selected-demo');
 const style=document.createElement('style');
 style.textContent=`body.presentation-demo{padding:12px!important;margin:0!important;background:#080b10!important}body.presentation-demo>.side,body.presentation-demo>.side-bg{display:none!important}.presentation-demo .wrap{margin:0!important;max-width:none!important;padding:0!important}.presentation-demo .wrap>*:not(.selected-demo){display:none!important}.presentation-demo section.selected-demo{margin:0!important}.presentation-demo .selected-demo>*:not(.stage):not(.controls){display:none!important}.presentation-demo .stage{max-width:calc((100vh - 110px)*16/9);margin:0 auto}.presentation-demo .controls{justify-content:center;font-size:16px;gap:8px}.presentation-demo .controls button{min-height:42px}.presentation-demo .controls .ctl-label{font-size:14px}.presentation-demo #controls>div:nth-child(4),.presentation-demo #controls>div:nth-child(5){display:none!important}`;
 document.head.appendChild(style);
 if(id==='tienda')document.getElementById('toggleShop').click();
 requestAnimationFrame(()=>{window.dispatchEvent(new Event('resize'));window.scrollTo(0,0)});
 document.addEventListener('keydown',e=>{if(e.key==='Escape'&&!e.repeat){window.parent.postMessage('riftwalker:close-demo',location.origin)}});
})();
