-- Reset de las cuentas (US 281, CA3). Lo corre el dueño del proyecto UNA vez en SQL Editor, antes de abrir las
-- cuentas, y después de volver a correr logros.sql. NO SE PUEDE DESHACER.
-- Borra el ranking, los logros y el progreso que quedaron de las pruebas, y los jugadores anónimos sin opiniones.
-- Los anónimos que mandaron opiniones quedan: rw_feedback.player_id borra en cascada, y se perderían las opiniones.
-- Con la base limpia, solo las cuentas suben progreso y entran al ranking (rw_sync_comets y rw_sync_progress
-- rechazan las sesiones anónimas).

-- 1. Antes, para ver qué se va a borrar:
select
 (select count(*) from public.rw_achievement_players) as jugadores_en_ranking,
 (select count(*) from public.rw_achievement_unlocks) as logros_desbloqueados,
 (select count(*) from public.rw_player_progress) as progresos,
 (select count(*) from auth.users u where u.is_anonymous
   and not exists (select 1 from public.rw_feedback f where f.player_id = u.id)) as anonimos_sin_opiniones,
 (select count(*) from auth.users u where u.is_anonymous
   and exists (select 1 from public.rw_feedback f where f.player_id = u.id)) as anonimos_con_opiniones_quedan,
 (select count(*) from public.rw_feedback) as opiniones_quedan;

-- 2. El reset:
begin;
delete from public.rw_achievement_unlocks;
delete from public.rw_achievement_players;
delete from public.rw_player_progress;
delete from auth.users u
 where u.is_anonymous
   and not exists (select 1 from public.rw_feedback f where f.player_id = u.id);
commit;

-- 3. Si quedó alguna cuenta con mail de las pruebas anteriores (la conversión de anónimo a cuenta que se descartó),
-- se borra desde Authentication → Users, de a una. Ojo: borra también sus opiniones.
