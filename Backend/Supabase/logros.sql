-- Ejecutar una vez en SQL Editor del proyecto Supabase. Reejecutable sin borrar progreso.
begin;
create table if not exists public.rw_achievement_catalog (
 id text primary key, color smallint not null check (color between 0 and 2),
 target integer not null check (target > 0)
);
insert into public.rw_achievement_catalog values
 ('azul_10',0,10),('azul_50',0,50),('azul_100',0,100),
 ('verde_10',1,10),('verde_50',1,50),('verde_100',1,100),
 ('dorado_10',2,10),('dorado_50',2,50),('dorado_100',2,100)
on conflict (id) do update set color=excluded.color, target=excluded.target;
-- Logros de combate (F13): en vez de color tienen la estadistica del jugador (F24) que los desbloquea, con los mismos
-- nombres que rw_stat_value. Se agregan siempre al final, como en LogrosCometas.Catalogo.
alter table public.rw_achievement_catalog alter column color drop not null;
alter table public.rw_achievement_catalog add column if not exists stat text;
insert into public.rw_achievement_catalog(id,color,target,stat) values
 ('bajas_10',null,10,'bajas'),('bajas_100',null,100,'bajas'),('bajas_500',null,500,'bajas'),
 ('cabezas_10',null,10,'cabezas'),('cabezas_50',null,50,'cabezas'),('cabezas_200',null,200,'cabezas'),
 ('asistencias_50',null,50,'asistencias'),('victorias_1',null,1,'victorias'),('victorias_25',null,25,'victorias'),
 ('mvp_1',null,1,'mvp'),('mvp_10',null,10,'mvp'),('racha_5',null,5,'mejor_racha'),('racha_10',null,10,'mejor_racha'),
 ('plantados_10',null,10,'plantados'),('desactivados_10',null,10,'desactivados'),('partidas_50',null,50,'partidas')
on conflict (id) do update set color=excluded.color, target=excluded.target, stat=excluded.stat;
create table if not exists public.rw_achievement_players (
 player_id uuid primary key references auth.users(id) on delete cascade,
 display_name text not null check (char_length(display_name) between 3 and 16),
 blue integer not null default 0 check (blue >= 0),
 green integer not null default 0 check (green >= 0),
 gold integer not null default 0 check (gold >= 0),
 created_at timestamptz not null default now()
);
create table if not exists public.rw_achievement_unlocks (
 player_id uuid references public.rw_achievement_players(player_id) on delete cascade,
 achievement_id text references public.rw_achievement_catalog(id),
 unlocked_at timestamptz not null default now(),
 primary key (player_id, achievement_id)
);
create index if not exists rw_unlock_owners on public.rw_achievement_unlocks(achievement_id,player_id);
alter table public.rw_achievement_catalog enable row level security;
alter table public.rw_achievement_players enable row level security;
alter table public.rw_achievement_unlocks enable row level security;
-- No acceso directo a tablas; solo RPC acotadas. Tampoco se exponen tokens ni auth.users.
revoke all on public.rw_achievement_catalog, public.rw_achievement_players, public.rw_achievement_unlocks from anon, authenticated;

create or replace function public.rw_sync_comets(p_name text,p_blue integer,p_green integer,p_gold integer)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare uid uuid := auth.uid(); p public.rw_achievement_players;
begin
 if uid is null then raise exception 'Authentication required' using errcode='28000'; end if;
 if p_name is null or char_length(btrim(p_name)) not between 3 and 16
    or btrim(p_name) !~ '^[[:alnum:] _-]+$' then raise exception 'Invalid name'; end if;
 if p_blue is null or p_green is null or p_gold is null or least(p_blue,p_green,p_gold)<0 then raise exception 'Invalid progress'; end if;
 insert into public.rw_achievement_players(player_id,display_name,blue,green,gold)
 values(uid,btrim(p_name),p_blue,p_green,p_gold)
 on conflict(player_id) do update set display_name=excluded.display_name,
 blue=greatest(rw_achievement_players.blue,excluded.blue),
 green=greatest(rw_achievement_players.green,excluded.green),
 gold=greatest(rw_achievement_players.gold,excluded.gold)
 returning * into p;
 insert into public.rw_achievement_unlocks(player_id,achievement_id)
 select uid,c.id from public.rw_achievement_catalog c
 where c.color is not null and (case c.color when 0 then p.blue when 1 then p.green else p.gold end)>=c.target
 on conflict do nothing;
 return jsonb_build_object('player_id',uid,'display_name',p.display_name,'blue',p.blue,'green',p.green,'gold',p.gold);
end $$;

create or replace function public.rw_achievement_board(p_offset integer default 0,p_achievement text default '')
returns jsonb language plpgsql security definer set search_path = '' as $$
declare result jsonb;
begin
 if auth.uid() is null then raise exception 'Authentication required' using errcode='28000'; end if;
 if p_offset is null or p_offset<0 or p_offset>100000 then raise exception 'Invalid offset'; end if;
 if p_achievement is null then p_achievement := ''; end if;
 if p_achievement<>'' and not exists(select 1 from public.rw_achievement_catalog where id=p_achievement) then raise exception 'Unknown achievement'; end if;
 with counts as (
   select p.player_id,p.display_name,count(u.achievement_id)::integer total,
     coalesce(jsonb_agg(u.achievement_id order by u.achievement_id) filter(where u.achievement_id is not null),'[]'::jsonb) achievements
   from public.rw_achievement_players p left join public.rw_achievement_unlocks u using(player_id)
   group by p.player_id,p.display_name
 ), ranked as (
   select *,dense_rank() over(order by total desc)::integer rank from counts
 ), filtered as (
   select * from ranked where p_achievement='' or achievements ? p_achievement
 ), page as (
   select * from filtered order by total desc,display_name,player_id limit 50 offset p_offset
 ), population as (select count(*) n from public.rw_achievement_players), stats as (
   select c.id achievement_id,count(u.player_id)::integer owners,
     coalesce(round(100.0*count(u.player_id)/nullif((select n from population),0),1),0) percent
   from public.rw_achievement_catalog c left join public.rw_achievement_unlocks u on u.achievement_id=c.id group by c.id
 ) select jsonb_build_object(
   'players',coalesce((select jsonb_agg(to_jsonb(page) order by total desc,display_name,player_id) from page),'[]'::jsonb),
   'stats',coalesce((select jsonb_agg(to_jsonb(stats) order by achievement_id) from stats),'[]'::jsonb),
   'population',(select n from population),
   'me',(select to_jsonb(ranked) from ranked where player_id=auth.uid()),
   'has_more',(select count(*) from filtered)>p_offset+50
 ) into result;
 return result;
end $$;
revoke all on function public.rw_sync_comets(text,integer,integer,integer) from public,anon;
revoke all on function public.rw_achievement_board(integer,text) from public,anon;
grant execute on function public.rw_sync_comets(text,integer,integer,integer) to authenticated;
grant execute on function public.rw_achievement_board(integer,text) to authenticated;
-- Resto del progreso del jugador (F18, US 162 y US 163): estadisticas del minijuego, records y cosmeticos.
-- El juego manda listas {clave, valor}; la base junta: contadores con el maximo, minimos con el minimo,
-- conjuntos con la union y valores con lo ultimo que mando el juego. Devuelve todo en el mismo formato.
create table if not exists public.rw_player_progress (
 player_id uuid primary key references auth.users(id) on delete cascade,
 contadores jsonb not null default '{}'::jsonb,
 minimos jsonb not null default '{}'::jsonb,
 conjuntos jsonb not null default '{}'::jsonb,
 valores jsonb not null default '{}'::jsonb,
 updated_at timestamptz not null default now()
);
alter table public.rw_player_progress enable row level security;
revoke all on public.rw_player_progress from anon, authenticated;

create or replace function public.rw_sync_progress(p jsonb)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare
 uid uuid := auth.uid();
 r public.rw_player_progress;
 c_in jsonb := '{}'::jsonb; m_in jsonb := '{}'::jsonb; s_in jsonb := '{}'::jsonb; v_in jsonb := '{}'::jsonb;
 e jsonb;
begin
 if uid is null then raise exception 'Authentication required' using errcode='28000'; end if;
 if p is null or jsonb_typeof(p) <> 'object' or octet_length(p::text) > 65536 then raise exception 'Invalid progress'; end if;
 -- Claves cortas y simples; numeros enteros no negativos; textos y elementos de hasta 64 caracteres.
 for e in select * from jsonb_array_elements(coalesce(p->'contadores','[]'::jsonb)) loop
  if coalesce(e->>'clave','') !~ '^[a-z0-9_.-]{1,64}$' or jsonb_typeof(e->'valor') <> 'number' or (e->>'valor')::numeric < 0
  then raise exception 'Invalid progress'; end if;
  c_in := c_in || jsonb_build_object(e->>'clave', trunc((e->>'valor')::numeric));
 end loop;
 for e in select * from jsonb_array_elements(coalesce(p->'minimos','[]'::jsonb)) loop
  if coalesce(e->>'clave','') !~ '^[a-z0-9_.-]{1,64}$' or jsonb_typeof(e->'valor') <> 'number' or (e->>'valor')::numeric <= 0
  then raise exception 'Invalid progress'; end if;
  m_in := m_in || jsonb_build_object(e->>'clave', trunc((e->>'valor')::numeric));
 end loop;
 for e in select * from jsonb_array_elements(coalesce(p->'conjuntos','[]'::jsonb)) loop
  if coalesce(e->>'clave','') !~ '^[a-z0-9_.-]{1,64}$' or jsonb_typeof(e->'elementos') <> 'array'
     or jsonb_array_length(e->'elementos') > 1000
     or exists(select 1 from jsonb_array_elements(e->'elementos') x where jsonb_typeof(x) <> 'string' or char_length(x #>> '{}') > 64)
  then raise exception 'Invalid progress'; end if;
  s_in := s_in || jsonb_build_object(e->>'clave', e->'elementos');
 end loop;
 for e in select * from jsonb_array_elements(coalesce(p->'valores','[]'::jsonb)) loop
  if coalesce(e->>'clave','') !~ '^[a-z0-9_.-]{1,64}$' or jsonb_typeof(e->'valor') <> 'string' or char_length(e->>'valor') > 64
  then raise exception 'Invalid progress'; end if;
  v_in := v_in || jsonb_build_object(e->>'clave', e->>'valor');
 end loop;

 insert into public.rw_player_progress(player_id) values (uid) on conflict (player_id) do nothing;
 select * into r from public.rw_player_progress where player_id = uid for update;
 update public.rw_player_progress set
  contadores = (select coalesce(jsonb_object_agg(k, greatest(coalesce((r.contadores->>k)::numeric, 0), coalesce((c_in->>k)::numeric, 0))), '{}'::jsonb)
   from (select jsonb_object_keys(r.contadores) k union select jsonb_object_keys(c_in)) t),
  minimos = (select coalesce(jsonb_object_agg(k, least(coalesce((r.minimos->>k)::numeric, (m_in->>k)::numeric), coalesce((m_in->>k)::numeric, (r.minimos->>k)::numeric))), '{}'::jsonb)
   from (select jsonb_object_keys(r.minimos) k union select jsonb_object_keys(m_in)) t),
  conjuntos = (select coalesce(jsonb_object_agg(k, (select coalesce(jsonb_agg(distinct x order by x), '[]'::jsonb)
     from (select jsonb_array_elements_text(coalesce(r.conjuntos->k, '[]'::jsonb)) x
           union select jsonb_array_elements_text(coalesce(s_in->k, '[]'::jsonb))) u)), '{}'::jsonb)
   from (select jsonb_object_keys(r.conjuntos) k union select jsonb_object_keys(s_in)) t),
  valores = r.valores || v_in,
  updated_at = now()
 where player_id = uid
 returning * into r;
 -- Logros de combate (F13): con las estadisticas recien guardadas. Hace falta que el jugador ya este en el ranking
 -- (rw_sync_comets va antes en cada sincronizacion).
 insert into public.rw_achievement_unlocks(player_id, achievement_id)
 select uid, c.id from public.rw_achievement_catalog c join public.rw_achievement_players a on a.player_id = uid
 where c.stat is not null and public.rw_stat_value(c.stat, r.contadores, r.minimos, a.blue, a.green, a.gold) >= c.target
 on conflict do nothing;

 return jsonb_build_object(
  'contadores', coalesce((select jsonb_agg(jsonb_build_object('clave', key, 'valor', value) order by key) from jsonb_each(r.contadores)), '[]'::jsonb),
  'minimos', coalesce((select jsonb_agg(jsonb_build_object('clave', key, 'valor', value) order by key) from jsonb_each(r.minimos)), '[]'::jsonb),
  'conjuntos', coalesce((select jsonb_agg(jsonb_build_object('clave', key, 'elementos', value) order by key) from jsonb_each(r.conjuntos)), '[]'::jsonb),
  'valores', coalesce((select jsonb_agg(jsonb_build_object('clave', key, 'valor', value) order by key) from jsonb_each(r.valores)), '[]'::jsonb));
end $$;
revoke all on function public.rw_sync_progress(jsonb) from public, anon;
grant execute on function public.rw_sync_progress(jsonb) to authenticated;
-- Ranking global por estadistica (US 226): ordena a todos los jugadores por el dato elegido. Usa lo que ya sube cada
-- juego: los cometas de cada color (rw_sync_comets) y el resto del progreso (rw_sync_progress, F24 y US 159).
-- Los porcentajes y promedios piden un minimo (5 partidas terminadas; 50 disparos en el minijuego) y solo aparecen
-- los jugadores con un valor mayor que 0. La mejor reaccion gana con el menor tiempo. Empates: mismo puesto.
create or replace function public.rw_num(j jsonb, k text) returns numeric
language sql immutable set search_path = '' as $$ select coalesce((j->>k)::numeric, 0) $$;
create or replace function public.rw_both(j jsonb, k text) returns numeric
language sql immutable set search_path = '' as $$
 select public.rw_num(j, 'estadisticas.tactico.' || k) + public.rw_num(j, 'estadisticas.deathmatch.' || k) $$;

-- El valor de cada estadistica de un jugador (para el ranking y para los logros de combate). null: no llega al minimo.
create or replace function public.rw_stat_value(p_stat text, c jsonb, m jsonb, p_blue integer, p_green integer, p_gold integer)
returns numeric language sql immutable set search_path = '' as $$
 select case p_stat
  when 'partidas' then public.rw_both(c,'partidas')
  when 'victorias' then public.rw_both(c,'victorias')
  when 'porcentaje_victorias' then case when public.rw_both(c,'partidas')>=5
    then 100*public.rw_both(c,'victorias')/public.rw_both(c,'partidas') end
  when 'bajas' then public.rw_both(c,'bajas')
  when 'muertes' then public.rw_both(c,'muertes')
  when 'asistencias' then public.rw_both(c,'asistencias')
  when 'cabezas' then public.rw_both(c,'cabezas')
  when 'kda' then case when public.rw_both(c,'partidas')>=5
    then (public.rw_both(c,'bajas')+public.rw_both(c,'asistencias'))/greatest(1,public.rw_both(c,'muertes')) end
  when 'tiros_cabeza' then case when public.rw_both(c,'partidas')>=5 and public.rw_both(c,'bajas')>0
    then 100*public.rw_both(c,'cabezas')/public.rw_both(c,'bajas') end
  when 'horas' then public.rw_both(c,'segundos')/3600
  when 'tactico_partidas' then public.rw_num(c,'estadisticas.tactico.partidas')
  when 'tactico_victorias' then public.rw_num(c,'estadisticas.tactico.victorias')
  when 'rondas_ganadas' then public.rw_num(c,'estadisticas.tactico.rondas_ganadas')
  when 'plantados' then public.rw_num(c,'estadisticas.tactico.plantados')
  when 'desactivados' then public.rw_num(c,'estadisticas.tactico.desactivados')
  when 'mvp' then public.rw_num(c,'estadisticas.tactico.mvp')
  when 'deathmatch_partidas' then public.rw_num(c,'estadisticas.deathmatch.partidas')
  when 'primer_puesto' then public.rw_num(c,'estadisticas.deathmatch.victorias')
  when 'deathmatch_bajas' then public.rw_num(c,'estadisticas.deathmatch.bajas')
  when 'mejor_racha' then public.rw_num(c,'estadisticas.deathmatch.mejor_racha')
  when 'cometas' then (p_blue+p_green+p_gold)::numeric
  when 'cometas_azules' then p_blue::numeric
  when 'cometas_verdes' then p_green::numeric
  when 'cometas_dorados' then p_gold::numeric
  when 'minijuego_puntos' then public.rw_num(c,'minijuego.puntos')
  when 'record_puntos' then public.rw_num(c,'minijuego.record_puntos')
  when 'precision' then case when public.rw_num(c,'minijuego.disparos')>=50
    then 100*(public.rw_num(c,'minijuego.aciertos.comun')+public.rw_num(c,'minijuego.aciertos.raro')
      +public.rw_num(c,'minijuego.aciertos.epico'))/public.rw_num(c,'minijuego.disparos') end
  when 'mejor_reaccion' then case when public.rw_num(c,'minijuego.disparos')>=50
    then public.rw_num(m,'minijuego.mejor_reaccion_ms') end
 end $$;

create or replace function public.rw_stat_board(p_stat text, p_offset integer default 0)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare result jsonb; menor boolean := p_stat = 'mejor_reaccion';
begin
 if auth.uid() is null then raise exception 'Authentication required' using errcode='28000'; end if;
 if p_offset is null or p_offset<0 or p_offset>100000 then raise exception 'Invalid offset'; end if;
 if p_stat is null or p_stat not in ('partidas','victorias','porcentaje_victorias','bajas','muertes','asistencias','kda',
    'tiros_cabeza','horas','tactico_partidas','tactico_victorias','rondas_ganadas','plantados','desactivados','mvp',
    'deathmatch_partidas','primer_puesto','deathmatch_bajas','mejor_racha','cabezas','cometas','cometas_azules','cometas_verdes',
    'cometas_dorados','minijuego_puntos','record_puntos','precision','mejor_reaccion') then raise exception 'Unknown stat'; end if;
 with base as (
   select p.player_id, p.display_name, p.blue, p.green, p.gold,
     coalesce(r.contadores, '{}'::jsonb) c, coalesce(r.minimos, '{}'::jsonb) m
   from public.rw_achievement_players p left join public.rw_player_progress r using(player_id)
 ), valores as (
   select player_id, display_name, public.rw_stat_value(p_stat, c, m, blue, green, gold) as value
   from base
 ), ranked as (
   select player_id, display_name, value,
     dense_rank() over(order by case when menor then value else -value end)::integer rank
   from valores where value is not null and value>0
 ), page as (
   select * from ranked order by rank, display_name, player_id limit 50 offset p_offset
 ) select jsonb_build_object(
   'stat', p_stat,
   'players', coalesce((select jsonb_agg(jsonb_build_object('player_id',player_id,'display_name',display_name,
       'value',round(value,2),'rank',rank) order by rank,display_name,player_id) from page),'[]'::jsonb),
   'me', (select jsonb_build_object('player_id',player_id,'display_name',display_name,'value',round(value,2),'rank',rank)
       from ranked where player_id=auth.uid()),
   'population', (select count(*) from ranked),
   'has_more', (select count(*) from ranked)>p_offset+50
 ) into result;
 return result;
end $$;
revoke all on function public.rw_num(jsonb,text) from public, anon, authenticated;
revoke all on function public.rw_both(jsonb,text) from public, anon, authenticated;
revoke all on function public.rw_stat_value(text,jsonb,jsonb,integer,integer,integer) from public, anon, authenticated;
revoke all on function public.rw_stat_board(text,integer) from public, anon;
grant execute on function public.rw_stat_board(text,integer) to authenticated;
commit;
