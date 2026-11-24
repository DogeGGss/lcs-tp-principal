# Arma menu/img/skins: el ícono de cada arma de la tienda (Assets/UI/Tienda/Iconos) pintado con los colores de cada skin.
# Uso: python3 generar-skins.py "<proyecto>/Assets/UI/Tienda/Iconos" guia/menu/img/skins  (necesita Pillow)
# Arma las imágenes de las skins de armas para la copia web del menú: el ícono de cada arma de la tienda
# (Assets/UI/Tienda/Iconos) con las partes pintadas como cada skin (CatalogoDeSkins.cs).
# Las partes se separan por el color del ícono: naranja = Main (acento), claro = White (paneles),
# oscuro = Black (cuerpo), el resto = Grey (detalles).
import sys, json, numpy as np
from PIL import Image

SRC = sys.argv[1]   # carpeta de los íconos
DST = sys.argv[2]   # carpeta de salida
ANCHO = 520

ARMAS = [("mitre", "Fusil"), ("urquiza", "Subfusil"), ("belgrano-sur", "FusilRafaga"), ("roca", "Escopeta"),
         ("ultimo-tren", "Francotirador"), ("gran-capitan", "GranCapitan"), ("la-portena", "Pistola"),
         ("la-trochita", "PistolaPesada"), ("cuchillo", "Cuchillo")]
H = lambda h: np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255], float)
COMBOS = {"rift": (0xB9A7E8, 0x6E3FCF, 0x2A2140, 0xF2ECFF), "albiceleste": (0x74ACDF, 0xF2F5F8, 0x2B3A4A, 0xF6B40E),
          "brasa": (0x1B1B1B, 0x4A4A4A, 0x0A0A0A, 0xE0281F), "artico": (0xDDE3E8, 0x5FD3E8, 0x2A2F35, 0xA7B0B8),
          "desierto": (0xC8A26B, 0x6B5A40, 0x2B241A, 0xD9C9A8)}
DISENOS = ["marmol-de-retiro", "azulejos-del-subte", "cristal-del-rift", "codigo-binario", "proxima-estacion",
           "mapa-del-subte", "dragon-abisal", "tormenta"]
CUCHILLO = ["dragon-abisal", "hoja-de-plasma", "cristal-de-hielo", "sangre-de-invasor", "holograma"]

rng = np.random.default_rng(7)

def ruido(h, w, escala, semilla):
    r = np.random.default_rng(semilla)
    gh, gw = max(2, h // escala + 2), max(2, w // escala + 2)
    g = r.random((gh, gw))
    img = Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    return np.asarray(img, float) / 255

def color(h, w, c):
    return np.broadcast_to(H(c), (h, w, 3)).copy()

def mezcla(a, b, k):
    return a * (1 - k[..., None]) + b * k[..., None]

# ---------- Texturas de los diseños (aproximadas, quietas) ----------
def marmol(h, w, base, veta, semilla):
    n = ruido(h, w, 24, semilla) * 0.6 + ruido(h, w, 7, semilla + 1) * 0.4
    y, x = np.mgrid[0:h, 0:w]
    v = np.abs(np.sin((x * 0.05 + y * 0.09 + n * 9)))
    k = np.clip((0.12 - v) / 0.12, 0, 1) ** 1.5
    return mezcla(color(h, w, base), color(h, w, veta), k * 0.9)

def azulejos(h, w):
    y, x = np.mgrid[0:h, 0:w]
    lado = 16
    k = ((x // lado + y // lado) % 2).astype(float)
    junta = ((x % lado < 1.5) | (y % lado < 1.5)).astype(float)
    t = mezcla(color(h, w, 0x7FB6D9), color(h, w, 0xF4F6F8), k)
    return mezcla(t, color(h, w, 0xC9CED3), junta)

def cristal(h, w, a, b, semilla):
    n = ruido(h, w, 18, semilla)
    y, x = np.mgrid[0:h, 0:w]
    facetas = (np.floor(n * 6) / 6)
    t = mezcla(color(h, w, a), color(h, w, b), facetas)
    brillo = np.clip(np.sin(x * 0.08 - y * 0.05) * 0.5 + 0.5, 0, 1) ** 8 * 0.5
    return np.clip(t + brillo[..., None] * 255, 0, 255)

def binario(h, w):
    t = color(h, w, 0x041208)
    y, x = np.mgrid[0:h, 0:w]
    col = x // 7
    fase = rng.random(w // 7 + 2) * 40
    k = ((np.sin((y + fase[col] * 9) * 0.35) > 0.55) & ((x % 7) < 4)).astype(float)
    cola = np.clip(1 - ((y + fase[col] * 13) % 60) / 60, 0, 1)
    return mezcla(t, color(h, w, 0x26FF59), k * (0.35 + 0.65 * cola))

def leds(h, w, encendido, fondo=0x3A2412, paso=5):
    y, x = np.mgrid[0:h, 0:w]
    punto = (((x % paso) - paso / 2) ** 2 + ((y % paso) - paso / 2) ** 2) < (paso * 0.32) ** 2
    banda = (np.sin(x * 0.04) > -0.2)
    k = punto * (0.55 + 0.45 * banda)
    return mezcla(color(h, w, fondo), color(h, w, encendido), k)

def mapa(h, w):
    t = color(h, w, 0xF5F2E8)
    y, x = np.mgrid[0:h, 0:w]
    lineas = [(0x18A8DF, 0.3, 10), (0xEE3D3D, -0.2, 30), (0x2B6FB6, 0.8, 55), (0x1C9E5A, -0.6, 75), (0xF2C230, 0.05, 90), (0x9B4D9E, 1.4, 20)]
    for c, pend, off in lineas:
        d = np.abs((y - off * h / 100) - pend * (x - w / 2) * 0.4)
        t = mezcla(t, color(h, w, c), (d < 2.2).astype(float))
    return t

def dragon_paneles(h, w, semilla, base=0x0B0D14, ancho=0.05):
    t = color(h, w, base)
    n = ruido(h, w, 14, semilla)
    grieta = np.clip(1 - np.abs(n - 0.5) / ancho, 0, 1)
    t = mezcla(t, color(h, w, 0x1E6BFF), grieta)
    return mezcla(t, color(h, w, 0xCFFAFF), grieta ** 4)

def escamas(h, w, base):
    y, x = np.mgrid[0:h, 0:w]
    k = np.abs(np.sin(x * 0.45 + np.sin(y * 0.45) * 1.4)) * np.abs(np.sin(y * 0.45))
    return mezcla(color(h, w, base), color(h, w, 0x0E0F14), np.clip(k, 0, 1) * 0.7)

def llamas(h, w, a, b, semilla):
    n = ruido(h, w, 6, semilla)
    return mezcla(color(h, w, a), color(h, w, b), n)

def nubes(h, w, base, claro, semilla, rayos=False):
    n = ruido(h, w, 20, semilla) * 0.7 + ruido(h, w, 6, semilla + 3) * 0.3
    t = mezcla(color(h, w, base), color(h, w, claro), n)
    if rayos:
        y, x = np.mgrid[0:h, 0:w]
        for k in range(4):
            x0 = rng.random() * w
            zig = x0 + np.sin(y * 0.3 + k) * 6 + (y * (rng.random() - 0.5))
            t = mezcla(t, color(h, w, 0xFFE27A), (np.abs(x - zig) < 1.3).astype(float) * (y < h * 0.9))
    return t

def venas(h, w, semilla):
    n = ruido(h, w, 10, semilla)
    k = np.clip(1 - np.abs(n - 0.5) / 0.06, 0, 1)
    return mezcla(color(h, w, 0x34343C), color(h, w, 0xFF2A35), k)

def holograma(h, w):
    y, x = np.mgrid[0:h, 0:w]
    lin = (y % 4 < 2).astype(float)
    return mezcla(color(h, w, 0x0B3B52), color(h, w, 0x26C0FF), 0.45 + 0.55 * lin)

# ---------- Partes del ícono ----------
def partes(rgb, a):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    main = (r > 140) & (r - b > 70) & (r - g > 25)
    white = ~main & (lum > 160)
    black = ~main & ~white & (lum < 62)
    grey = ~main & ~white & ~black
    vis = a > 8
    return {"Black": black & vis, "White": white & vis, "Grey": grey & vis, "Main": main & vis}, lum

def pintar(rgb, a, capas, lum, tex):
    """tex: parte -> imagen (h, w, 3). Mantiene el sombreado del ícono dentro de cada parte."""
    out = rgb.copy()
    for parte, m in capas.items():
        if parte not in tex or not m.any():
            continue
        media = max(1.0, lum[m].mean())
        sombra = np.clip(lum / media, 0.55, 1.35) ** 0.7
        t = tex[parte] * sombra[..., None]
        out[m] = np.clip(t[m], 0, 255)
    return out

def texturas(arma, estilo, h, w):
    if estilo in COMBOS:
        cu, pa, de, ac = COMBOS[estilo]
        return {"Black": color(h, w, cu), "White": color(h, w, pa), "Grey": color(h, w, de), "Main": color(h, w, ac)}
    if arma == "cuchillo":
        todo = {"dragon-abisal": dragon_paneles(h, w, 3, 0x2A2C38, 0.09), "hoja-de-plasma": llamas(h, w, 0x3A1470, 0xC08BFF, 4),
                "cristal-de-hielo": cristal(h, w, 0xBFE6FF, 0x5FA8E8, 5), "sangre-de-invasor": venas(h, w, 6),
                "holograma": holograma(h, w)}[estilo]
        return {k: todo for k in ("Black", "White", "Grey", "Main")}
    if estilo == "marmol-de-retiro":
        negro = marmol(h, w, 0x16171A, 0xD9A93E, 11)
        return {"White": marmol(h, w, 0xEEEDE8, 0x8F8C86, 12), "Black": negro, "Grey": negro, "Main": color(h, w, 0xE8B84A)}
    if estilo == "azulejos-del-subte":
        return {"White": azulejos(h, w), "Black": color(h, w, 0xECEBE6), "Grey": color(h, w, 0xECEBE6), "Main": color(h, w, 0x8A8D92)}
    if estilo == "cristal-del-rift":
        oscuro = cristal(h, w, 0x2A1F4A, 0x4A2F86, 13)
        return {"White": cristal(h, w, 0x7B3FE4, 0x4CC3FF, 14), "Black": oscuro, "Grey": oscuro, "Main": color(h, w, 0x3B1F7A)}
    if estilo == "codigo-binario":
        b = binario(h, w)
        return {"White": b, "Main": b}
    if estilo == "proxima-estacion":
        l = leds(h, w, 0xFF8A1E)
        return {"White": l, "Black": l, "Grey": l, "Main": leds(h, w, 0xFFB347, 0x2A1505, 5)}
    if estilo == "mapa-del-subte":
        m = mapa(h, w)
        return {"White": m, "Black": m, "Grey": m, "Main": llamas(h, w, 0xEE3D3D, 0x2B6FB6, 15)}
    if estilo == "dragon-abisal":
        e = escamas(h, w, 0x2A2C36)
        return {"White": dragon_paneles(h, w, 16), "Black": e, "Grey": e, "Main": llamas(h, w, 0x1E6BFF, 0x4FC8FF, 17)}
    if estilo == "tormenta":
        n = nubes(h, w, 0x141A26, 0x2E3850, 18)
        return {"White": nubes(h, w, 0x2E3850, 0x6C7A99, 19, True), "Black": n, "Grey": n, "Main": llamas(h, w, 0xFFC81E, 0xFFF3B0, 20)}
    raise ValueError(estilo)

hechos = []
for arma, icono in ARMAS:
    im = Image.open(f"{SRC}/{icono}.png").convert("RGBA")
    im = im.crop(im.getbbox())
    k = min(1.0, ANCHO / im.width)
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    arr = np.asarray(im, float)
    rgb, a = arr[..., :3], arr[..., 3]
    capas, lum = partes(rgb, a)
    h, w = a.shape
    Image.fromarray(arr.astype(np.uint8)).save(f"{DST}/{arma}.webp", quality=90, method=6)
    estilos = CUCHILLO if arma == "cuchillo" else list(COMBOS) + DISENOS
    for estilo in estilos:
        out = pintar(rgb, a, capas, lum, texturas(arma, estilo, h, w))
        img = np.dstack([out, a]).astype(np.uint8)
        Image.fromarray(img).save(f"{DST}/{arma}-{estilo}.webp", quality=88, method=6)
        hechos.append(f"{arma}-{estilo}")
print(len(hechos), "skins")
