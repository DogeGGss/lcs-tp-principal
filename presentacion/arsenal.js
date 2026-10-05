// Instantánea de la guía, 05/10/2026. Confirmar contra la build S3.
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
    "retroceso": "Leve y vuelve rápido: premia tirar de a un tiro."
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
    "retroceso": "Patada fuerte hacia arriba: para acertar, un tiro cada 0,4 s."
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
    "retroceso": "Suave y parejo; es el arma más precisa en movimiento."
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
    "retroceso": "Un golpe por disparo; el cono de 4,5° no cambia al moverse."
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
    "retroceso": "Cada ráfaga sube un poco y vuelve antes de la siguiente."
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
    "retroceso": "Primer tiro perfecto; sube fuerte desde la tercera bala y después zigzaguea."
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
    "retroceso": "Igual de preciso con la mira o sin ella (dispersión −30 %: 4,2° en movimiento); quieto, al centro. Sin mira en el HUD, como el AWP. Después de cada tiro se recarga el cerrojo (1,33 s)."
  }
];