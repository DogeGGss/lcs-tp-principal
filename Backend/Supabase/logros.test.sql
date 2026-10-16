-- Prueba transaccional: ejecutar en SQL Editor después de logros.sql. No deja jugadores de prueba.
begin;
insert into auth.users(id) values ('00000000-0000-4000-8000-000000000901'),('00000000-0000-4000-8000-000000000902');
select set_config('request.jwt.claim.sub','00000000-0000-4000-8000-000000000901',true);
select public.rw_sync_comets('Prueba Uno',100,50,9);
select public.rw_sync_comets('Prueba Uno',100,50,9);
select public.rw_sync_comets('Prueba Uno',1,0,0);
select set_config('request.jwt.claim.sub','00000000-0000-4000-8000-000000000902',true);
select public.rw_sync_comets('Prueba Dos',50,100,0);
do $$
declare a jsonb; b jsonb; n integer;
begin
 select count(*) into n from public.rw_achievement_catalog;
 if n<>9 then raise exception 'Expected 9 achievements'; end if;
 select count(*) into n from public.rw_achievement_unlocks where player_id='00000000-0000-4000-8000-000000000901';
 if n<>5 then raise exception 'Duplicate unlock or incorrect thresholds'; end if;
 if (select blue from public.rw_achievement_players where player_id='00000000-0000-4000-8000-000000000901')<>100 then raise exception 'Progress regressed'; end if;
 a:=public.rw_achievement_board(0,'');
 perform set_config('request.jwt.claim.sub','00000000-0000-4000-8000-000000000901',true);
 b:=public.rw_achievement_board(0,'');
 if a#>>'{me,rank}'<>b#>>'{me,rank}' then raise exception 'Tied ranks differ'; end if;
 if jsonb_array_length(b->'stats')<>9 then raise exception 'Missing percentages'; end if;
 b:=public.rw_achievement_board(0,'dorado_100');
 if exists(select 1 from jsonb_array_elements(b->'players') p where not (p->'achievements' ? 'dorado_100')) then raise exception 'Owner filter failed'; end if;
 if has_function_privilege('anon','public.rw_sync_comets(text,integer,integer,integer)','execute') then raise exception 'Anonymous role has write access'; end if;
 if has_table_privilege('authenticated','public.rw_achievement_players','insert') then raise exception 'Direct write exposed'; end if;
 if (select count(*) from public.rw_achievement_players where player_id in ('00000000-0000-4000-8000-000000000901','00000000-0000-4000-8000-000000000902'))<>2 then raise exception 'Identity isolation failed'; end if;
 raise notice 'Achievement backend checks passed';
end $$;
rollback;
