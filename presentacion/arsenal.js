// Las 19 armas de la guía (26/11/2026). estado: 'juego' (las 7 del MVP, como en la instantánea del 05/10/2026, y El Gran
// Capitán, US 287) o 'preparada' (diseñada en la guía, para más adelante). imagen: la de media/armas o el ícono de la guía.
window.ARSENAL = [
  {
    "id": "pistola",
    "nombre": "Pistola",
    "alias": "La Porteña",
    "cat": "pistolas",
    "slot": "Secundaria",
    "icono": "g05",
    "precio": 0,
    "backlog": "US 071",
    "nivel": "MVP",
    "rol": "La que tenés siempre. Gana duelos a la cabeza; al cuerpo no alcanza.",
    "modo": "Semiautomática",
    "cadencia": 6.75,
    "cargador": 20,
    "reserva": 60,
    "recarga": 1.5,
    "equipar": 0.75,
    "movilidad": 1.0,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Baja",
    "tramos": [
      {
        "hasta": 30,
        "cuerpo": 25,
        "cabeza": 75,
        "piernas": 21
      },
      {
        "hasta": null,
        "cuerpo": 21,
        "cabeza": 63,
        "piernas": 18
      }
    ],
    "dispersion": [
      0.4,
      2.5
    ],
    "retroceso": "Leve y vuelve rápido: premia tirar de a un tiro.",
    "estado": "juego",
    "imagen": "media/armas/pistola.png"
  },
  {
    "id": "pistolaAuto",
    "nombre": "Pistola automática",
    "alias": "El Marplatense",
    "cat": "pistolas",
    "slot": "Secundaria",
    "icono": "g10",
    "precio": 450,
    "backlog": null,
    "nivel": "Opcional",
    "rol": "Ráfaga barata para rondas de ahorro a corta distancia.",
    "modo": "Automática",
    "cadencia": 10,
    "cargador": 13,
    "reserva": 39,
    "recarga": 1.5,
    "equipar": 0.75,
    "movilidad": 1.0,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Baja",
    "tramos": [
      {
        "hasta": 20,
        "cuerpo": 26,
        "cabeza": 78,
        "piernas": 22
      },
      {
        "hasta": null,
        "cuerpo": 21,
        "cabeza": 63,
        "piernas": 18
      }
    ],
    "dispersion": [
      0.65,
      2.0
    ],
    "retroceso": "Se descontrola después de la cuarta bala.",
    "estado": "preparada",
    "imagen": "../img/g10.png"
  },
  {
    "id": "ballestaMano",
    "nombre": "Ballesta de mano",
    "alias": "La Estrella del Norte",
    "cat": "pistolas",
    "slot": "Secundaria",
    "icono": "g11",
    "precio": 600,
    "backlog": null,
    "nivel": "Opcional",
    "rol": "Silenciosa. Un virote a la cabeza mata si el rival tiene escudo liviano o nada.",
    "modo": "Proyectil (1 virote)",
    "cadencia": 0.8,
    "cargador": 1,
    "reserva": 8,
    "recarga": 0,
    "equipar": 0.8,
    "movilidad": 1.0,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Ninguna",
    "tramos": [
      {
        "hasta": null,
        "cuerpo": 55,
        "cabeza": 140,
        "piernas": 47
      }
    ],
    "dispersion": [
      0.0,
      1.5
    ],
    "retroceso": "Sin retroceso; el virote viaja a 60 m/s y cae un poco con la distancia.",
    "estado": "preparada",
    "imagen": "../img/g11.png"
  },
  {
    "id": "pistolaPesada",
    "nombre": "Pistola pesada",
    "alias": "La Trochita",
    "cat": "pistolas",
    "slot": "Secundaria",
    "icono": "g15",
    "precio": 800,
    "backlog": "US 072",
    "nivel": "MVP",
    "rol": "El arma de las rondas de ahorro: un tiro a la cabeza mata a cualquiera a menos de 30 m.",
    "modo": "Semiautomática",
    "cadencia": 4,
    "cargador": 6,
    "reserva": 24,
    "recarga": 2.25,
    "equipar": 0.9,
    "movilidad": 1.0,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Media",
    "tramos": [
      {
        "hasta": 30,
        "cuerpo": 55,
        "cabeza": 159,
        "piernas": 46
      },
      {
        "hasta": null,
        "cuerpo": 50,
        "cabeza": 145,
        "piernas": 42
      }
    ],
    "dispersion": [
      0.25,
      3.0
    ],
    "retroceso": "Patada fuerte hacia arriba: para acertar, un tiro cada 0,4 s.",
    "estado": "juego",
    "imagen": "media/armas/pistolaPesada.png"
  },
  {
    "id": "subfusil",
    "nombre": "Subfusil",
    "alias": "Urquiza",
    "cat": "subfusiles",
    "slot": "Principal",
    "icono": "g07",
    "precio": 1600,
    "backlog": "US 067",
    "nivel": "MVP",
    "rol": "Corta distancia y movimiento. Barato, pero pierde contra un fusil a más de 20 m.",
    "modo": "Automática",
    "cadencia": 11,
    "cargador": 30,
    "reserva": 90,
    "recarga": 2.25,
    "equipar": 0.75,
    "movilidad": 0.97,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Baja",
    "tramos": [
      {
        "hasta": 20,
        "cuerpo": 25,
        "cabeza": 75,
        "piernas": 21
      },
      {
        "hasta": null,
        "cuerpo": 21,
        "cabeza": 63,
        "piernas": 18
      }
    ],
    "dispersion": [
      0.4,
      0.9
    ],
    "retroceso": "Suave y parejo; es el arma más precisa en movimiento.",
    "estado": "juego",
    "imagen": "media/armas/subfusil.png"
  },
  {
    "id": "subfusilCompacto",
    "nombre": "Subfusil compacto",
    "alias": "Premetro",
    "cat": "subfusiles",
    "slot": "Principal",
    "icono": "g13",
    "precio": 1100,
    "backlog": null,
    "nivel": "Opcional",
    "rol": "Cadencia altísima a quemarropa. Con clic derecho dispara ráfagas de 4.",
    "modo": "Automática",
    "cadencia": 14,
    "cargador": 20,
    "reserva": 60,
    "recarga": 2.25,
    "equipar": 0.7,
    "movilidad": 0.98,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Baja",
    "tramos": [
      {
        "hasta": 15,
        "cuerpo": 27,
        "cabeza": 67,
        "piernas": 23
      },
      {
        "hasta": null,
        "cuerpo": 22,
        "cabeza": 55,
        "piernas": 19
      }
    ],
    "dispersion": [
      0.8,
      1.2
    ],
    "retroceso": "Vibra mucho: sirve pegado al rival.",
    "estado": "preparada",
    "imagen": "../img/g13.png"
  },
  {
    "id": "pdw",
    "nombre": "PDW",
    "alias": "Tren de la Costa",
    "cat": "subfusiles",
    "slot": "Principal",
    "icono": "g17",
    "precio": 1850,
    "backlog": null,
    "nivel": "Opcional",
    "rol": "Cargador de 50 para sostener el fuego; menos daño por bala.",
    "modo": "Automática",
    "cadencia": 12,
    "cargador": 50,
    "reserva": 100,
    "recarga": 2.8,
    "equipar": 0.85,
    "movilidad": 0.95,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Media",
    "tramos": [
      {
        "hasta": 25,
        "cuerpo": 24,
        "cabeza": 60,
        "piernas": 20
      },
      {
        "hasta": null,
        "cuerpo": 21,
        "cabeza": 52,
        "piernas": 18
      }
    ],
    "dispersion": [
      0.5,
      1.1
    ],
    "retroceso": "Sube despacio; con el cargador grande se corrige sobre la marcha.",
    "estado": "preparada",
    "imagen": "../img/g17.png"
  },
  {
    "id": "escopeta",
    "nombre": "Escopeta",
    "alias": "Roca",
    "cat": "escopetas",
    "slot": "Principal",
    "icono": "g14",
    "precio": 900,
    "backlog": "US 070",
    "nivel": "MVP",
    "rol": "A menos de 8 m mata de un tiro si el rival no tiene escudo o tiene el liviano.",
    "modo": "Bombeo (12 perdigones)",
    "cadencia": 1.0,
    "cargador": 7,
    "reserva": 21,
    "recarga": 0.5,
    "recargaPorCartucho": true,
    "equipar": 1.0,
    "movilidad": 0.95,
    "zoom": null,
    "perdigones": 12,
    "penetracion": "Baja",
    "tramos": [
      {
        "hasta": 8,
        "cuerpo": 12,
        "cabeza": 24,
        "piernas": 10
      },
      {
        "hasta": 15,
        "cuerpo": 7,
        "cabeza": 14,
        "piernas": 6
      },
      {
        "hasta": null,
        "cuerpo": 4,
        "cabeza": 8,
        "piernas": 3
      }
    ],
    "dispersion": [
      4.5,
      4.5
    ],
    "retroceso": "Un golpe por disparo; el cono de 4,5° no cambia al moverse.",
    "estado": "juego",
    "imagen": "media/armas/escopeta.png"
  },
  {
    "id": "escopetaAuto",
    "nombre": "Escopeta automática",
    "alias": "Sarmiento",
    "cat": "escopetas",
    "slot": "Principal",
    "icono": "g12",
    "precio": 1850,
    "backlog": null,
    "nivel": "Opcional",
    "rol": "Para defender pasillos: menos daño por disparo, pero tres y medio por segundo.",
    "modo": "Semiautomática (10 perdigones)",
    "cadencia": 3.5,
    "cargador": 7,
    "reserva": 21,
    "recarga": 2.2,
    "equipar": 1.0,
    "movilidad": 0.94,
    "zoom": null,
    "perdigones": 10,
    "penetracion": "Baja",
    "tramos": [
      {
        "hasta": 10,
        "cuerpo": 14,
        "cabeza": 28,
        "piernas": 12
      },
      {
        "hasta": 15,
        "cuerpo": 8,
        "cabeza": 16,
        "piernas": 7
      },
      {
        "hasta": null,
        "cuerpo": 4,
        "cabeza": 8,
        "piernas": 3
      }
    ],
    "dispersion": [
      5.5,
      5.5
    ],
    "retroceso": "Sube un poco con cada disparo seguido.",
    "estado": "preparada",
    "imagen": "../img/g12.png"
  },
  {
    "id": "rafaga",
    "nombre": "Fusil de ráfaga",
    "alias": "Belgrano Sur",
    "cat": "fusiles",
    "slot": "Principal",
    "icono": "g08",
    "precio": 2100,
    "backlog": "US 069",
    "nivel": "MVP",
    "rol": "El fusil de las rondas forzadas: una ráfaga entera al cuerpo mata a quien no tiene escudo.",
    "modo": "Ráfaga de 3",
    "rafaga": 3,
    "cadenciaInterna": 15,
    "cadencia": 2.5,
    "cargador": 30,
    "reserva": 90,
    "recarga": 2.5,
    "equipar": 1.0,
    "movilidad": 0.93,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Media",
    "tramos": [
      {
        "hasta": 40,
        "cuerpo": 35,
        "cabeza": 115,
        "piernas": 30
      },
      {
        "hasta": null,
        "cuerpo": 31,
        "cabeza": 100,
        "piernas": 26
      }
    ],
    "dispersion": [
      0.3,
      3.5
    ],
    "retroceso": "Cada ráfaga sube un poco y vuelve antes de la siguiente.",
    "estado": "juego",
    "imagen": "media/armas/rafaga.png"
  },
  {
    "id": "fusilTactico",
    "nombre": "Fusil táctico",
    "alias": "San Martín",
    "cat": "fusiles",
    "slot": "Principal",
    "icono": "g19",
    "precio": 2900,
    "backlog": null,
    "nivel": "Extensión",
    "rol": "Silenciado y fácil de controlar. Mata de un tiro a la cabeza con escudo completo solo de cerca.",
    "modo": "Automática (silenciada)",
    "cadencia": 11,
    "cargador": 30,
    "reserva": 60,
    "recarga": 2.5,
    "equipar": 1.0,
    "movilidad": 0.92,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Media",
    "tramos": [
      {
        "hasta": 15,
        "cuerpo": 39,
        "cabeza": 156,
        "piernas": 33
      },
      {
        "hasta": 30,
        "cuerpo": 35,
        "cabeza": 140,
        "piernas": 30
      },
      {
        "hasta": null,
        "cuerpo": 31,
        "cabeza": 124,
        "piernas": 26
      }
    ],
    "dispersion": [
      0.2,
      4.5
    ],
    "retroceso": "Poca patada y sin trazadoras: el rival no ve de dónde le disparan.",
    "estado": "preparada",
    "imagen": "../img/g19.png"
  },
  {
    "id": "fusil",
    "nombre": "Fusil",
    "alias": "Mitre",
    "cat": "fusiles",
    "slot": "Principal",
    "icono": "g01",
    "precio": 2900,
    "backlog": "US 013",
    "nivel": "MVP",
    "rol": "El caro de siempre: un tiro a la cabeza mata a cualquiera a cualquier distancia. Al cuerpo necesita cuatro.",
    "modo": "Automática",
    "cadencia": 9.75,
    "cargador": 30,
    "reserva": 60,
    "recarga": 2.5,
    "equipar": 1.0,
    "movilidad": 0.92,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Media",
    "tramos": [
      {
        "hasta": null,
        "cuerpo": 40,
        "cabeza": 160,
        "piernas": 34
      }
    ],
    "dispersion": [
      0.25,
      5.0
    ],
    "retroceso": "Primer tiro perfecto; sube fuerte desde la tercera bala y después zigzaguea.",
    "estado": "juego",
    "imagen": "media/armas/fusil.png"
  },
  {
    "id": "dmr",
    "nombre": "Fusil de precisión",
    "alias": "Belgrano Norte",
    "cat": "fusiles",
    "slot": "Principal",
    "icono": "g02",
    "precio": 2250,
    "backlog": null,
    "nivel": "Extensión",
    "rol": "Semiautomático de media distancia: cabeza mata a cualquiera, cuerpo en dos o tres tiros.",
    "modo": "Semiautomática",
    "cadencia": 5,
    "cargador": 12,
    "reserva": 36,
    "recarga": 2.5,
    "equipar": 1.0,
    "movilidad": 0.9,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Alta",
    "tramos": [
      {
        "hasta": null,
        "cuerpo": 65,
        "cabeza": 195,
        "piernas": 55
      }
    ],
    "dispersion": [
      0.1,
      4.0
    ],
    "retroceso": "Patada seca por tiro; vuelve al centro en 0,2 s.",
    "estado": "preparada",
    "imagen": "../img/g02.png"
  },
  {
    "id": "sniperLiviano",
    "nombre": "Francotirador liviano",
    "alias": "Tren a las Nubes",
    "cat": "francotiradores",
    "slot": "Principal",
    "icono": "g09",
    "precio": 950,
    "backlog": null,
    "nivel": "Extensión",
    "rol": "Barato y ágil: cabeza mata a cualquiera; al cuerpo solo a quien no tiene escudo.",
    "modo": "Cerrojo",
    "cadencia": 1.4,
    "cargador": 5,
    "reserva": 15,
    "recarga": 2.5,
    "equipar": 1.1,
    "movilidad": 0.9,
    "zoom": 3.5,
    "perdigones": 1,
    "penetracion": "Media",
    "tramos": [
      {
        "hasta": null,
        "cuerpo": 101,
        "cabeza": 202,
        "piernas": 85
      }
    ],
    "dispersion": [
      0.0,
      3.0
    ],
    "retroceso": "Igual de preciso con la mira o sin ella (dispersión −30 %): quieto, el primer tiro va al centro. Sin mira en el HUD.",
    "estado": "preparada",
    "imagen": "../img/g09.png"
  },
  {
    "id": "francotirador",
    "nombre": "Francotirador",
    "alias": "Último Tren",
    "cat": "francotiradores",
    "slot": "Principal",
    "icono": "g04",
    "precio": 4700,
    "backlog": "US 068",
    "nivel": "MVP",
    "rol": "Un tiro al cuerpo o a la cabeza mata a cualquiera, con escudo o sin él. Es la única arma que lo hace.",
    "modo": "Cerrojo",
    "cadencia": 0.75,
    "cargador": 5,
    "reserva": 10,
    "recarga": 3.7,
    "equipar": 1.25,
    "movilidad": 0.85,
    "zoom": 2.5,
    "zoom2": 5,
    "perdigones": 1,
    "penetracion": "Alta",
    "tramos": [
      {
        "hasta": null,
        "cuerpo": 150,
        "cabeza": 255,
        "piernas": 127
      }
    ],
    "dispersion": [
      0.0,
      6.0
    ],
    "retroceso": "Igual de preciso con la mira o sin ella (dispersión −30 %: 4,2° en movimiento); quieto, al centro. Sin mira en el HUD, como el AWP. Después de cada tiro se recarga el cerrojo (1,33 s).",
    "estado": "juego",
    "imagen": "media/armas/francotirador.png"
  },
  {
    "id": "ballesta",
    "nombre": "Ballesta",
    "alias": "Tren del Fin del Mundo",
    "cat": "francotiradores",
    "slot": "Principal",
    "icono": "g06",
    "precio": 1500,
    "backlog": null,
    "nivel": "Opcional",
    "rol": "Silenciosa y con caída. En Invasión, cada virote atraviesa hasta 2 zombies.",
    "modo": "Proyectil (1 virote)",
    "cadencia": 0.55,
    "cargador": 1,
    "reserva": 10,
    "recarga": 0,
    "equipar": 1.0,
    "movilidad": 0.92,
    "zoom": 2.0,
    "perdigones": 1,
    "penetracion": "Ninguna",
    "tramos": [
      {
        "hasta": null,
        "cuerpo": 70,
        "cabeza": 180,
        "piernas": 60
      }
    ],
    "dispersion": [
      0.0,
      2.0
    ],
    "retroceso": "Sin retroceso; el virote viaja a 80 m/s con caída.",
    "estado": "preparada",
    "imagen": "../img/g06.png"
  },
  {
    "id": "ametralladoraLigera",
    "nombre": "Ametralladora liviana",
    "alias": "Tren de las Sierras",
    "cat": "pesadas",
    "slot": "Principal",
    "icono": "g03",
    "precio": 1600,
    "backlog": null,
    "nivel": "Opcional",
    "rol": "Cargador de 50 y penetración alta: sirve para tirar a través de paredes finas.",
    "modo": "Automática (acelera)",
    "cadencia": 13,
    "cadenciaInicial": 10,
    "cargador": 50,
    "reserva": 50,
    "recarga": 3.25,
    "equipar": 1.25,
    "movilidad": 0.85,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Alta",
    "tramos": [
      {
        "hasta": 30,
        "cuerpo": 30,
        "cabeza": 72,
        "piernas": 25
      },
      {
        "hasta": null,
        "cuerpo": 27,
        "cabeza": 65,
        "piernas": 23
      }
    ],
    "dispersion": [
      0.8,
      6.0
    ],
    "retroceso": "Empieza lenta (10/s) y en un segundo llega a 13/s; hay que apoyarse o agacharse.",
    "estado": "preparada",
    "imagen": "../img/g03.png"
  },
  {
    "id": "ametralladoraPesada",
    "nombre": "Ametralladora pesada",
    "alias": "Tren Patagónico",
    "cat": "pesadas",
    "slot": "Principal",
    "icono": "g16",
    "precio": 3200,
    "backlog": null,
    "nivel": "Opcional",
    "rol": "Cien balas para cubrir una entrada entera. Lenta de mover y de recargar.",
    "modo": "Automática (acelera)",
    "cadencia": 15.6,
    "cadenciaInicial": 12,
    "cargador": 100,
    "reserva": 100,
    "recarga": 5.0,
    "equipar": 1.25,
    "movilidad": 0.82,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Alta",
    "tramos": [
      {
        "hasta": 30,
        "cuerpo": 38,
        "cabeza": 95,
        "piernas": 32
      },
      {
        "hasta": null,
        "cuerpo": 31,
        "cabeza": 77,
        "piernas": 26
      }
    ],
    "dispersion": [
      0.8,
      6.0
    ],
    "retroceso": "Muy fuerte al principio; agachado mejora un 25 %.",
    "estado": "preparada",
    "imagen": "../img/g16.png"
  },
  {
    "id": "lanzagranadas",
    "nombre": "Lanzagranadas",
    "alias": "El Gran Capitán",
    "cat": "pesadas",
    "slot": "Principal",
    "icono": "g18",
    "precio": 2500,
    "backlog": null,
    "nivel": "Invasión",
    "rol": "Solo en Invasión: limpia grupos. Explota en un radio de 4 m y te lastima si tirás muy cerca.",
    "modo": "Proyectil explosivo",
    "cadencia": 1.2,
    "cargador": 6,
    "reserva": 12,
    "recarga": 3.5,
    "equipar": 1.25,
    "movilidad": 0.85,
    "zoom": null,
    "perdigones": 1,
    "penetracion": "Ninguna",
    "tramos": [
      {
        "hasta": null,
        "cuerpo": 100,
        "cabeza": 100,
        "piernas": 100
      }
    ],
    "explosion": {
      "radio": 4,
      "centro": 100,
      "borde": 20,
      "autoDanio": 0.5
    },
    "dispersion": [
      0.0,
      1.0
    ],
    "retroceso": "Granada con caída; explota al impactar.",
    "estado": "juego",
    "imagen": "../img/g18.png"
  }
];
