-- Opiniones de "Danos tu opinión" (menú principal) y aviso a Discord de cada una nueva.
-- Ejecutar en SQL Editor después de logros.sql. Reejecutable sin borrar opiniones.
-- El webhook de Discord NO va en este archivo ni en el juego: se guarda una sola vez en el Vault (ver README.md).
begin;
-- pg_net manda el aviso a Discord desde la base, sin frenar el guardado (el pedido sale después del commit).
create extension if not exists pg_net with schema extensions;

create table if not exists public.rw_feedback (
 id uuid primary key,
 player_id uuid not null references auth.users(id) on delete cascade,
 display_name text check (char_length(display_name) <= 24),
 kind text not null check (kind in ('opinion','sugerencia','error')),
 rating smallint not null default 0 check (rating between 0 and 5),
 message text not null check (char_length(message) between 1 and 500),
 game_version text check (char_length(game_version) <= 32),
 created_at timestamptz not null default now()
);
create index if not exists rw_feedback_player_time on public.rw_feedback(player_id, created_at desc);
alter table public.rw_feedback enable row level security;
-- Como el resto: sin acceso directo desde el juego, solo la RPC. Se leen en el Table Editor de Supabase.
revoke all on public.rw_feedback from anon, authenticated;

-- Guarda una opinión del jugador de la sesión. p_id lo genera el juego: si la reenvía (por un corte), no se duplica
-- ni vuelve a avisar. Límites: una cada 10 s y 20 por día por jugador.
create or replace function public.rw_send_feedback(p_id uuid, p_kind text, p_rating integer, p_message text,
 p_version text default null, p_name text default null)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare uid uuid := auth.uid(); msg text := btrim(p_message); nombre text;
begin
 if uid is null then raise exception 'Authentication required' using errcode='28000'; end if;
 if p_id is null or p_kind is null or p_kind not in ('opinion','sugerencia','error')
    or p_rating is null or p_rating not between 0 and 5
    or msg is null or char_length(msg) not between 1 and 500
    or char_length(coalesce(p_version,'')) > 32 then raise exception 'Invalid feedback'; end if;
 if exists(select 1 from public.rw_feedback where id = p_id) then
  if exists(select 1 from public.rw_feedback where id = p_id and player_id = uid) then
   return jsonb_build_object('id', p_id, 'duplicate', true);
  end if;
  raise exception 'Invalid feedback';
 end if;
 if exists(select 1 from public.rw_feedback where player_id = uid and created_at > now() - interval '10 seconds')
    or (select count(*) from public.rw_feedback where player_id = uid and created_at > now() - interval '1 day') >= 20
 then raise exception 'Too many feedback'; end if;
 -- El nombre del ranking si ya sincronizó; si no, el que manda el juego (recortado).
 select display_name into nombre from public.rw_achievement_players where player_id = uid;
 nombre := coalesce(nombre, nullif(left(btrim(coalesce(p_name,'')), 24), ''));
 insert into public.rw_feedback(id, player_id, display_name, kind, rating, message, game_version)
 values (p_id, uid, nombre, p_kind, p_rating, msg, nullif(btrim(coalesce(p_version,'')), ''));
 return jsonb_build_object('id', p_id, 'duplicate', false);
end $$;
revoke all on function public.rw_send_feedback(uuid,text,integer,text,text,text) from public, anon;
grant execute on function public.rw_send_feedback(uuid,text,integer,text,text,text) to authenticated;

-- Aviso a Discord. Lee el webhook del Vault (nombre 'discord_feedback_webhook'); si no está, no avisa. Un error al
-- avisar nunca impide guardar la opinión. allowed_mentions vacío: un "@everyone" escrito en la opinión no notifica.
create or replace function public.rw_feedback_discord() returns trigger
language plpgsql security definer set search_path = '' as $$
declare url text; titulo text; color integer; valor text;
begin
 if coalesce(current_setting('rw.sin_discord', true), '') = '1' then return new; end if; -- pruebas
 select decrypted_secret into url from vault.decrypted_secrets where name = 'discord_feedback_webhook' limit 1;
 if url is null or url !~ '^https://(canary\.|ptb\.)?(discord|discordapp)\.com/api/webhooks/' then return new; end if;
 titulo := case new.kind when 'error' then '🐞 Error reportado' when 'sugerencia' then '💡 Sugerencia nueva' else '💬 Opinión nueva' end;
 color := case new.kind when 'error' then 15548997 when 'sugerencia' then 16705372 else 5793266 end;
 valor := case when new.rating = 0 then 'Sin valorar'
               else repeat('★', new.rating) || repeat('☆', 5 - new.rating) || ' (' || new.rating || '/5)' end;
 perform net.http_post(
  url := url,
  body := jsonb_build_object(
   'username', 'Riftwalker · Opiniones',
   'allowed_mentions', jsonb_build_object('parse', '[]'::jsonb),
   'embeds', jsonb_build_array(jsonb_build_object(
    'title', titulo,
    'description', new.message,
    'color', color,
    'fields', jsonb_build_array(
     jsonb_build_object('name', 'Valoración', 'value', valor, 'inline', true),
     jsonb_build_object('name', 'Jugador', 'value', coalesce(new.display_name, 'Sin nombre'), 'inline', true),
     jsonb_build_object('name', 'Versión', 'value', coalesce(new.game_version, '—'), 'inline', true)),
    'timestamp', to_char(new.created_at at time zone 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')))),
  headers := '{"Content-Type": "application/json"}'::jsonb);
 return new;
exception when others then
 raise warning 'rw_feedback_discord: %', sqlerrm;
 return new;
end $$;
revoke all on function public.rw_feedback_discord() from public, anon, authenticated;
drop trigger if exists rw_feedback_discord on public.rw_feedback;
create trigger rw_feedback_discord after insert on public.rw_feedback
 for each row execute function public.rw_feedback_discord();
commit;
