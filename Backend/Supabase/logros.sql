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
 where (case c.color when 0 then p.blue when 1 then p.green else p.gold end)>=c.target
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
commit;
