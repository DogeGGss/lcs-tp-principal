# Arma menu/extras.js con lo copiado del juego (skins, emotes, origen de cada cosmético, pase, desafíos y paquetes).
# Uso: python3 generar-extras.py guia/menu/extras.js
import json
ORIG = ['#070707', '#757575', '#1A1A1A', '#9D3607']
h = lambda x: '#%06X' % x
armas = [("mitre","Mitre"),("urquiza","Urquiza"),("belgrano-sur","Belgrano Sur"),("roca","Roca"),("ultimo-tren","Último Tren"),
         ("gran-capitan","El Gran Capitán"),("la-portena","La Porteña"),("la-trochita","La Trochita"),("cuchillo","Cuchillo")]
combos = [("rift","Rift",(0xB9A7E8,0x6E3FCF,0x2A2140,0xF2ECFF)),("albiceleste","Albiceleste",(0x74ACDF,0xF2F5F8,0x2B3A4A,0xF6B40E)),
          ("brasa","Brasa",(0x1B1B1B,0x4A4A4A,0x0A0A0A,0xE0281F)),("artico","Ártico",(0xDDE3E8,0x5FD3E8,0x2A2F35,0xA7B0B8)),
          ("desierto","Desierto",(0xC8A26B,0x6B5A40,0x2B241A,0xD9C9A8))]
# id, nombre, rareza, partes, muestra, extras {parte: muestra}
dis = [("marmol-de-retiro","Mármol de Retiro",2,["White"],0xEEEDE8,{"Black":0x16171A,"Grey":0x16171A,"Main":0xE8B84A}),
       ("azulejos-del-subte","Azulejos del subte",2,["White"],0x7FB6D9,{"Black":0xECEBE6,"Grey":0xECEBE6,"Main":0x8A8D92}),
       ("cristal-del-rift","Cristal del Rift",2,["White"],0x7B3FE4,{"Black":0x2A1F4A,"Grey":0x2A1F4A,"Main":0x3B1F7A}),
       ("codigo-binario","Código binario",3,["White","Main"],0x26FF59,{}),
       ("proxima-estacion","Próxima estación",3,["White"],0xFF8A1E,{"Black":0x1A0E05,"Grey":0x1A0E05,"Main":0xFFB347}),
       ("mapa-del-subte","Mapa del subte",3,["White"],0xF5F2E8,{"Black":0xF5F2E8,"Grey":0xF5F2E8,"Main":0xEE3D3D}),
       ("dragon-abisal","Dragón Abisal",4,["White"],0x1E6BFF,{"Black":0x2A2C36,"Grey":0x2A2C36,"Main":0x4FC8FF}),
       ("tormenta","Tormenta",4,["White"],0xFFC81E,{"Black":0x1E2433,"Grey":0x1E2433,"Main":0xFFE27A})]
cuch = [("dragon-abisal","Dragón Abisal",0x1E6BFF,0x1A1C24),("hoja-de-plasma","Hoja de plasma",0x9B4DFF,None),
        ("cristal-de-hielo","Cristal de hielo",0x8CCBFF,None),("sangre-de-invasor","Sangre de invasor",0xE0141E,None),
        ("holograma","Holograma",0x26C0FF,None)]
orden = ["Black","White","Grey","Main"]
skins = []
for aid, an in armas:
    if aid != "cuchillo":
        for cid, cn, cols in combos:
            skins.append({"id":f"{aid}-{cid}","arma":aid,"estilo":cid,"nombre":cn,"rareza":1,"barra":[h(c) for c in cols]})
        for did, dn, r, partes, m, ex in dis:
            barra = [h(ex[o]) if o in ex else h(m) if o in partes else ORIG[i] for i, o in enumerate(orden)]
            skins.append({"id":f"{aid}-{did}","arma":aid,"estilo":did,"nombre":dn,"rareza":r,"barra":barra})
    else:
        for did, dn, m, cuerpo in cuch:
            c = cuerpo if cuerpo is not None else m
            skins.append({"id":f"{aid}-{did}","arma":aid,"estilo":did,"nombre":dn,"rareza":4,"barra":[h(c),h(m),h(c),h(m)]})
emotes = [("Shuffle","Shuffle",2),("Twerk","Twerk",4),("Flair","Flair",3),("Rumba","Rumba",1),("Silly","Payaseando",1),
          ("DefaultDance","El Clásico",4),("Floss","Floss",3),("Dancing","Pasito",1),("Greeting","Saludo",0),("HipHop","Hip Hop",1),
          ("HipHop2","Freestyle",2),("HipHop3","Old School",2),("NorthernSoul","Giro Soul",3),("PunchingBag","Round 1",2),("Twist","Twist",1)]
baile = {"Flair":"alfa-flair","Shuffle":"astra-shuffle","Silly":"bravo-silly","Rumba":"charlie-rumba"}
datos = {
 "armas":[{"id":a,"nombre":n} for a,n in armas],
 "skins":skins,
 "emotes":[{"estado":e,"nombre":n,"rareza":r,"baile":baile.get(e)} for e,n,r in emotes],
 "origen":{
  "conjuntos":{"umbral-celeste":"capsulas","forja-ambar":"capsulas","pulso-carmesi":"capsulas","pesado-realista":"capsulas","guerrero-synthwave":"capsulas",
    "sigiloso-manga":"capsulas","sigiloso-8bit":"capsulas","pesado-tinta-china":"capsulas","astra-pop":"pase","guerrero-fauve":"pase","sigiloso-tinta":"pase",
    "astra-acuarela":"pase","velo-esmeralda":"tienda","astra-nouveau":"tienda","pesado-cubista":"tienda","guerrero-puntillismo":"tienda","ultima-oleada":"tienda","nucleo-del-rift":"tienda"},
  "bannerDelAvatar":{"astra-celeste":"umbral-celeste","pesado-ambar":"forja-ambar","guerrero-carmesi":"pulso-carmesi","sigiloso-esmeralda":"velo-esmeralda","no-muerto":"ultima-oleada","corazon-del-rift":"nucleo-del-rift"},
  "titulosPase":["Último vagón","Vía muerta","Fuera de horario","Expreso al infierno","Pasajero fantasma","Destino desconocido","Servicio interrumpido","Perdí el tren","Sin retorno","Maquinista del apocalipsis","Cambio de vía"],
  "titulosTienda":["ACE","El del clutch","Grieta viviente","Último pasajero","Exterminador de zombies","Jefe de estación","Claramente era hacker","Era lag","Sobreviví de pedo","Mi mouse no anda","MVP del equipo contrario","Aprobado por lástima","Esto no entraba en el parcial","Vine por la cursada"],
  "estilos":{"rift":"capsulas","brasa":"capsulas","artico":"capsulas","albiceleste":"tienda","desierto":"tienda","marmol-de-retiro":"capsulas","cristal-del-rift":"capsulas",
    "azulejos-del-subte":"tienda","codigo-binario":"capsulas","proxima-estacion":"capsulas","mapa-del-subte":"tienda","dragon-abisal":"capsulas","tormenta":"tienda"},
  "emotes":{"Silly":"capsulas","Rumba":"capsulas","HipHop3":"capsulas","Shuffle":"capsulas","Flair":"capsulas","Dancing":"pase","HipHop2":"pase","Floss":"pase","Twerk":"pase",
    "Twist":"tienda","HipHop":"tienda","PunchingBag":"tienda","NorthernSoul":"tienda","DefaultDance":"tienda"}},
 "pase":["Último vagón","Vía muerta","sk:belgrano-sur-albiceleste","av:astra-pop","cr:100","bn:astra-pop","em:Dancing","mc:astra-pop","Fuera de horario","sk:la-portena-azulejos-del-subte",
   "cr:100","Expreso al infierno","av:guerrero-fauve","em:HipHop2","cr:100","bn:guerrero-fauve","sk:la-trochita-marmol-de-retiro","mc:guerrero-fauve","Pasajero fantasma","sk:ultimo-tren-codigo-binario",
   "av:sigiloso-tinta","sk:urquiza-mapa-del-subte","bn:sigiloso-tinta","mc:sigiloso-tinta","cr:100","Destino desconocido","Servicio interrumpido","em:Floss","Perdí el tren","sk:roca-proxima-estacion",
   "av:astra-acuarela","bn:astra-acuarela","Sin retorno","mc:astra-acuarela","cr:150","Maquinista del apocalipsis","sk:ultimo-tren-dragon-abisal","Cambio de vía","em:Twerk","sk:mitre-tormenta"],
 "desafios":[],
 "packs":[{"id":"bienvenida","nombre":"Oferta de bienvenida","detalle":"5 boletos + 200 cristales","cristales":200,"boletos":5,"precio":1990,"unaVez":True},
   {"id":"c80","nombre":"Bolsa de cristales","cristales":80,"bonus":0,"precio":1200},{"id":"c400","nombre":"Cofre de cristales","cristales":400,"bonus":40,"precio":5500},
   {"id":"c1000","nombre":"Caja de cristales","cristales":1000,"bonus":150,"precio":12900},{"id":"c2500","nombre":"Bóveda de cristales","cristales":2500,"bonus":500,"precio":29900}],
}
D = datos["desafios"]
def d(nombre, modo, metas, xp): D.append({"nombre":nombre,"modo":modo,"metas":metas,"xp":xp})
for p in ["Soledad","Unzué","Goya","Necochea"]: d(f"Jugar 3 partidas con {p}","Táctico",[3],[300])
d("Eliminar 3 enemigos con disparos a la cabeza","Deathmatch o Táctico",[3],[300])
d("Eliminar {0} enemigos","Invasión",[10,20,40,80,160,320],[300,300,500,500,750,1000])
d("Ganar 2 partidas","Táctico",[2],[300]); d("Ganar 1 partida","Deathmatch",[1],[300])
d("Llegar a la oleada {0}","Invasión",[10,20,30,40],[300,500,750,1000])
d("Eliminar 5 enemigos sin apuntar con el Último Tren","Deathmatch o Táctico",[5],[500])
for a, n in armas:
    if a not in ("cuchillo","gran-capitan"): d(f"Eliminar 15 enemigos con {n}","Deathmatch o Táctico",[15],[300])
d("Cargar 3 puestos de suministros","Invasión",[3],[300]); d("Eliminar al Jefe","Invasión",[1],[750]); d("Eliminar 10 alfas","Invasión",[10],[500])
txt = ("// Copiado del juego al 24/11/2026: las skins (CatalogoDeSkins.cs), los emotes (Resources/Emotes.asset), de dónde sale cada\n"
       "// cosmético y lo que da cada nivel del pase (OrigenDeCosmeticos.cs), los desafíos (PaseDeBatalla.cs) y los paquetes de\n"
       "// cristales (PerfilDatos.cs). barra: los colores de cuerpo, paneles, detalles y acento (CatalogoDeSkins.ColoresDeBarra).\n"
       "window.EXTRAS = " + json.dumps(datos, ensure_ascii=False) + ";\n")
open(__import__('sys').argv[1], 'w').write(txt)
print(len(skins), "skins", len(D), "desafios")
