// Arma menu/catalogo.js desde PerfilDatos.cs (avatares, banners, marcos, títulos, rarezas y medidas de los marcos).
const fs = require('fs');
const cs = fs.readFileSync('D:/Juego Unity/My project/Assets/Scripts/Perfil/PerfilDatos.cs', 'utf8');
const rz = { Inicial: 0, Comun: 1, Raro: 2, Epico: 3, Legendario: 4 };
const items = (letra) => [...cs.matchAll(new RegExp(String.raw`\b${letra}\("([^"]*)", "([^"]*)", "([^"]*)", "([^"]*)", Rareza\.(\w+)\)`, 'g'))]
  .map(m => ({ id: m[1], nombre: m[2], personaje: m[3], estilo: m[4], rareza: rz[m[5]] }));
const dic = (nombre) => {
  const desde = cs.indexOf(nombre + ' = new');
  const bloque = cs.slice(desde, cs.indexOf('};', desde));
  const out = {};
  for (const m of bloque.matchAll(/\{ "([^"]+)", (?:new Vector4)?\(([^)]*)\) \}/g))
    out[m[1]] = m[2].split(',').map(v => v.trim().replace(/f$/, '')).map(v => v === 'true' ? true : v === 'false' ? false : +v);
  return out;
};
const avatares = items('A'), banners = items('B');
const rieles = dic('RielesMarco'), interiores = dic('InterioresMarco'), cortes = dic('CortesMarco');
const marcos = [{ id: '', nombre: 'Sin marco', personaje: '', estilo: '', rareza: 0 }]
  .concat(banners.filter(b => rieles[b.id]).map(b => ({ ...b })));
const desdeT = cs.indexOf('string[] titulos =');
const titulos = [...cs.slice(desdeT, cs.indexOf('};', desdeT)).matchAll(/"([^"]+)"/g)].map(m => m[1]);
const desdeF = cs.indexOf('var fijos');
const fijos = {};
for (const m of cs.slice(desdeF, cs.indexOf('};', desdeF)).matchAll(/\{ "([^"]+)", Rareza\.(\w+) \}/g)) fijos[m[1]] = rz[m[2]];
const tit = [{ id: '', nombre: 'Sin título', rareza: 0 }].concat(titulos.map((t, i) => {
  const k = i % 20;
  return { id: 't' + (i + 1), nombre: t, rareza: t in fijos ? fijos[t] : k < 10 ? 1 : k < 16 ? 2 : k < 19 ? 3 : 4 };
}));
const pj = [...cs.matchAll(/new Personaje \{ id = "(\w+)", nombre = "([^"]+)", rol = "([^"]+)", color = "([^"]+)" \}/g)]
  .map(m => ({ id: m[1], nombre: m[2], rol: m[3], color: m[4] }));
const def = cs.match(/public string avatar = "([^"]+)", banner = "([^"]+)", titulo = "([^"]*)";/);
const defMarco = cs.match(/public string marco = "([^"]+)"/);
const datos = { avatares, banners, marcos, titulos: tit, personajes: pj, rieles, interiores, cortes,
  inicial: { avatar: def[1], banner: def[2], titulo: def[3], marco: defMarco[1] } };
const txt = '// Generado desde PerfilDatos.cs (Resources/Perfil del juego). Rarezas: 0 inicial, 1 común, 2 raro, 3 épico, 4 legendario.\n' +
  '// rieles e interiores: izquierda, arriba, derecha y abajo, en píxeles del marco original (3840 × 1280). cortes: esquina, centro y si se repite.\n' +
  'window.CATALOGO = ' + JSON.stringify(datos) + ';\n';
fs.writeFileSync('guia/menu/catalogo.js', txt);
console.log('avatares', avatares.length, 'banners', banners.length, 'marcos', marcos.length, 'titulos', tit.length, 'personajes', pj.length,
  'rieles', Object.keys(rieles).length, 'interiores', Object.keys(interiores).length, 'cortes', Object.keys(cortes).length, JSON.stringify(datos.inicial));
