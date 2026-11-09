// Borrar la cuenta (F18, US 273, CA2). La pide el launcher con la sesión de la cuenta (encabezado Authorization) y la
// contraseña, para confirmar que es el dueño. La función comprueba la contraseña con Supabase Auth y borra el usuario
// con la clave de servicio, que solo existe acá (nunca en el juego ni en el launcher). Sus datos se borran en cascada:
// progreso, monedas, logros, lugar en el ranking y opiniones. El mail queda libre para crear otra cuenta (CA3).
// Se publica SIN «Verify JWT with legacy secret»: la sesión la valida la función (paso 1), así sirve también con las
// sesiones firmadas con las claves nuevas de Supabase. Respuestas:
// - 200 { borrada: true }
// - 401 { error: "sesion" } (sin sesión o vencida) · 401 { error: "clave" } (la contraseña no es la de la cuenta)
// - 400 { error: "clave" } (falta la contraseña) · 500 { error: "borrar" }
const URL_SUPABASE = Deno.env.get("SUPABASE_URL") ?? "";
const CLAVE_PUBLICA = Deno.env.get("SUPABASE_ANON_KEY") || Deno.env.get("CLAVE_PUBLICA") || "";
const CLAVE_SERVICIO = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") || Deno.env.get("CLAVE_SERVICIO") || "";

const responder = (estado: number, cuerpo: Record<string, unknown>) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { "Content-Type": "application/json" } });

Deno.serve(async (req) => {
  if (req.method !== "POST") return responder(405, { error: "metodo" });
  const sesion = req.headers.get("Authorization") ?? "";
  if (!sesion.startsWith("Bearer ")) return responder(401, { error: "sesion" });
  let clave = "";
  try { clave = (await req.json())?.clave ?? ""; } catch { /* sin cuerpo */ }
  if (!clave) return responder(400, { error: "clave" });

  // 1. De quién es la sesión.
  const u = await fetch(`${URL_SUPABASE}/auth/v1/user`, { headers: { apikey: CLAVE_PUBLICA, Authorization: sesion } });
  if (!u.ok) return responder(401, { error: "sesion" });
  const usuario = await u.json();
  if (!usuario?.id || !usuario.email || usuario.is_anonymous) return responder(401, { error: "sesion" });

  // 2. La contraseña es la de esa cuenta.
  const t = await fetch(`${URL_SUPABASE}/auth/v1/token?grant_type=password`, {
    method: "POST",
    headers: { apikey: CLAVE_PUBLICA, "Content-Type": "application/json" },
    body: JSON.stringify({ email: usuario.email, password: clave }),
  });
  if (!t.ok) return responder(401, { error: "clave" });

  // 3. Se borra el usuario; sus filas se van en cascada.
  const b = await fetch(`${URL_SUPABASE}/auth/v1/admin/users/${usuario.id}`, {
    method: "DELETE",
    headers: { apikey: CLAVE_SERVICIO, Authorization: `Bearer ${CLAVE_SERVICIO}` },
  });
  if (!b.ok) return responder(500, { error: "borrar" });
  return responder(200, { borrada: true });
});
