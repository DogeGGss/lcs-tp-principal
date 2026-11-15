-- Perfiles y partidas (F34): el historial de partidas (US 374) y el perfil de otros jugadores (US 373).
-- Ejecutar en SQL Editor después de logros.sql. Es reejecutable y no borra nada.
-- - rw_add_matches: el juego manda las partidas terminadas que todavía no subió (con el id de cada una, así un
--   reintento no las duplica). La base guarda las 20 más nuevas de cada cuenta y las devuelve: el historial es el mismo
--   en cualquier computadora (US 374, CA4).
-- - rw_public_profile: lo público de un jugador para verlo desde el ranking, la tabla con Tab o el resultado (US 373):
--   nombre, lo equipado (avatar, banner, título y marco), el nivel, las estadísticas por modo, los logros, el puesto en
--   el ranking y su historial. Nunca el mail, las monedas, las compras ni nada de la cuenta (CA3).
begin;
create table if not exists public.rw_match_history (
 player_id uuid not null references auth.users(id) on delete cascade,
 id text not null check (char_length(id) between 1 and 96),
 played_at timestamptz not null,
 data jsonb not null,
 primary key (player_id, id)
);
create index if not exists rw_match_history_recent on public.rw_match_history(player_id, played_at desc);
alter table public.rw_match_history enable row level security;
-- Sin acceso directo a la tabla: solo las RPC de abajo.
revoke all on public.rw_match_history from anon, authenticated;

-- Las 20 partidas más nuevas de un jugador, en el formato del juego (HistorialDePartidas.Partida).
create or replace function public.rw_history_of(p_player uuid)
returns jsonb language sql stable security definer set search_path = '' as $$
 select coalesce(jsonb_agg(h.data || jsonb_build_object('id', h.id) order by h.played_at desc, h.id), '[]'::jsonb)
 from (select id, data, played_at from public.rw_match_history where player_id = p_player
       order by played_at desc, id limit 20) h
$$;
revoke all on function public.rw_history_of(uuid) from public, anon, authenticated;

create or replace function public.rw_add_matches(p jsonb)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare
 uid uuid := auth.uid();
 e jsonb;
 modo text; dificultad text;
 fecha bigint;
begin
 if uid is null then raise exception 'Authentication required' using errcode='28000'; end if;
 -- Como el resto del progreso, solo las cuentas guardan partidas (US 281).
 if coalesce((auth.jwt()->>'is_anonymous')::boolean,false) then raise exception 'Account required' using errcode='42501'; end if;
 if p is null or jsonb_typeof(p) <> 'array' or jsonb_array_length(p) > 20 or octet_length(p::text) > 32768 then
  raise exception 'Invalid matches';
 end if;
 for e in select * from jsonb_array_elements(p) loop
  modo := e->>'modo';
  dificultad := coalesce(e->>'dificultad', '');
  if jsonb_typeof(e) <> 'object'
     or coalesce(e->>'id','') !~ '^[A-Za-z0-9_.:#-]{1,96}$'
     or modo is null or modo not in ('tactico','deathmatch','invasion')
     or char_length(coalesce(e->>'mapa','')) > 64
     or jsonb_typeof(e->'fecha') <> 'number'
     or jsonb_typeof(e->'gano') <> 'boolean'
     or dificultad not in ('','facil','normal','dificil')
     or exists(select 1 from unnest(array['segundos','puesto','bajas','muertes','asistencias','oleada']) k
               where jsonb_typeof(coalesce(e->k,'0'::jsonb)) <> 'number'
                  or (coalesce(e->>k,'0'))::numeric < 0 or (coalesce(e->>k,'0'))::numeric > 100000)
  then raise exception 'Invalid matches'; end if;
  fecha := trunc((e->>'fecha')::numeric);
  -- Una fecha imposible (antes de 2026 o más de un día en el futuro) se toma como la de ahora.
  if fecha < 1767225600 or fecha > extract(epoch from now())::bigint + 86400 then fecha := extract(epoch from now())::bigint; end if;
  insert into public.rw_match_history(player_id, id, played_at, data)
  values (uid, e->>'id', to_timestamp(fecha), jsonb_build_object(
   'modo', modo,
   'mapa', coalesce(e->>'mapa',''),
   'fecha', fecha,
   'segundos', trunc(coalesce((e->>'segundos')::numeric, 0)),
   'gano', (e->>'gano')::boolean,
   'puesto', trunc(coalesce((e->>'puesto')::numeric, 0)),
   'bajas', trunc(coalesce((e->>'bajas')::numeric, 0)),
   'muertes', trunc(coalesce((e->>'muertes')::numeric, 0)),
   'asistencias', trunc(coalesce((e->>'asistencias')::numeric, 0)),
   'dificultad', dificultad,
   'oleada', trunc(coalesce((e->>'oleada')::numeric, 0)),
   'coop', coalesce((e->>'coop')::boolean, false)))
  on conflict (player_id, id) do nothing;
 end loop;
 -- Quedan las 20 más nuevas.
 delete from public.rw_match_history h where h.player_id = uid and h.id not in
  (select id from public.rw_match_history where player_id = uid order by played_at desc, id limit 20);
 return jsonb_build_object('partidas', public.rw_history_of(uid));
end $$;
revoke all on function public.rw_add_matches(jsonb) from public, anon;
grant execute on function public.rw_add_matches(jsonb) to authenticated;

create or replace function public.rw_public_profile(p_player uuid)
returns jsonb language plpgsql stable security definer set search_path = '' as $$
declare
 a public.rw_achievement_players;
 r public.rw_player_progress;
 c jsonb; v jsonb;
 rango_bajas integer; rango_logros integer; valor_bajas numeric; logros integer;
begin
 -- Como el ranking: se puede mirar con cualquier sesión, también la anónima.
 if auth.uid() is null then raise exception 'Authentication required' using errcode='28000'; end if;
 if p_player is null then raise exception 'Invalid player'; end if;
 select * into a from public.rw_achievement_players where player_id = p_player;
 -- Sin cuenta en el ranking no hay perfil que mostrar (CA4).
 if a.player_id is null then return jsonb_build_object('found', false); end if;
 select * into r from public.rw_player_progress where player_id = p_player;
 c := coalesce(r.contadores, '{}'::jsonb);
 v := coalesce(r.valores, '{}'::jsonb);
 -- Puesto en el ranking de bajas (el que abre la pantalla de Logros) y en el de logros, con las mismas reglas.
 valor_bajas := public.rw_stat_value('bajas', c, coalesce(r.minimos,'{}'::jsonb), a.blue, a.green, a.gold);
 if valor_bajas is not null and valor_bajas > 0 then
  select count(distinct x.valor) + 1 into rango_bajas from (
   select public.rw_stat_value('bajas', coalesce(pp.contadores,'{}'::jsonb), coalesce(pp.minimos,'{}'::jsonb), p.blue, p.green, p.gold) valor
   from public.rw_achievement_players p left join public.rw_player_progress pp using(player_id)) x
  where x.valor > valor_bajas;
 end if;
 select count(*) into logros from public.rw_achievement_unlocks where player_id = p_player;
 if logros > 0 then
  select count(distinct t.n) + 1 into rango_logros from (
   select count(*) n from public.rw_achievement_unlocks group by player_id) t where t.n > logros;
 end if;
 return jsonb_build_object(
  'found', true,
  'player_id', a.player_id,
  'display_name', a.display_name,
  -- Solo lo público: lo equipado y el nivel. No van las compras, los packs, los últimos premios ni nada de la cuenta (CA3).
  'valores', coalesce((select jsonb_agg(jsonb_build_object('clave', key, 'valor', value) order by key) from jsonb_each_text(v)
     where key in ('perfil.avatar','perfil.banner','perfil.titulo','perfil.marco','nivel.jugador')), '[]'::jsonb),
  -- Las estadísticas por modo (F24 y US 308), los récords de Invasión y el minijuego.
  'contadores', coalesce((select jsonb_agg(jsonb_build_object('clave', key, 'valor', value) order by key) from jsonb_each(c)
     where key like 'estadisticas.%' or key like 'zombie.record.%' or key like 'minijuego.%'), '[]'::jsonb),
  'cometas', jsonb_build_array(a.blue, a.green, a.gold),
  'achievements', coalesce((select jsonb_agg(achievement_id order by achievement_id) from public.rw_achievement_unlocks
     where player_id = p_player), '[]'::jsonb),
  'rank_bajas', coalesce(rango_bajas, 0),
  'rank_logros', coalesce(rango_logros, 0),
  'partidas', public.rw_history_of(p_player));
end $$;
revoke all on function public.rw_public_profile(uuid) from public, anon;
grant execute on function public.rw_public_profile(uuid) to authenticated;
commit;
