// Autenticación propia de Photon (F28, US 277, CA1). El juego publicado se conecta a Photon con la sesión de su cuenta
// y Photon, antes de dejarlo entrar, le pasa esa sesión a esta función, que le pregunta a Supabase Auth si es válida.
// Responde en el formato que espera Photon:
// - { ResultCode: 1, UserId, Nickname }: la sesión vale. UserId es el id de la cuenta, así la misma cuenta no puede
//   estar dos veces en una sala (CA3), y Nickname es el nombre de la cuenta (CA2).
// - { ResultCode: 2, Message }: sin sesión, vencida, o de un jugador anónimo.
// Se publica SIN «Verify JWT»: Photon no manda el encabezado de Supabase, la sesión viene en el cuerpo (o en ?token=) y
// la verifica la función. Ver Backend/Supabase/README.md, «Funciones».
const URL_SUPABASE = Deno.env.get("SUPABASE_URL") ?? "";
const CLAVE_PUBLICA = Deno.env.get("SUPABASE_ANON_KEY") || Deno.env.get("CLAVE_PUBLICA") || "";

const responder = (cuerpo: Record<string, unknown>) =>
  new Response(JSON.stringify(cuerpo), { headers: { "Content-Type": "application/json" } });

// El juego manda { "token": "<access token>" } por POST (AuthenticationValues.SetAuthPostData); también se acepta ?token=.
async function sesionDelPedido(req: Request): Promise<string | null> {
  const enUrl = new URL(req.url).searchParams.get("token");
  if (enUrl) return enUrl;
  if (req.method !== "POST") return null;
  const texto = await req.text();
  try {
    const datos = JSON.parse(texto);
    if (typeof datos?.token === "string" && datos.token) return datos.token;
  } catch { /* no es JSON: puede venir como formulario */ }
  return new URLSearchParams(texto).get("token") || null;
}

Deno.serve(async (req) => {
  const sesion = await sesionDelPedido(req);
  if (!sesion) return responder({ ResultCode: 2, Message: "Falta la sesión de la cuenta" });
  const r = await fetch(`${URL_SUPABASE}/auth/v1/user`, { headers: { apikey: CLAVE_PUBLICA, Authorization: `Bearer ${sesion}` } });
  if (!r.ok) return responder({ ResultCode: 2, Message: "La sesión no es válida o venció" });
  const usuario = await r.json();
  if (!usuario?.id || usuario.is_anonymous) return responder({ ResultCode: 2, Message: "Hace falta una cuenta" });
  const nombre = usuario.user_metadata?.nombre || String(usuario.email ?? "").split("@")[0] || "Jugador";
  return responder({ ResultCode: 1, UserId: usuario.id, Nickname: nombre });
});
