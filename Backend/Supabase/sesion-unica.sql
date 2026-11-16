-- Una sola sesión por cuenta (F38, US 393). Ejecutar en SQL Editor después de logros.sql. Es reejecutable y no borra nada.
-- Como en el LoL, vale el último inicio de sesión:
-- - Al entrar (en el launcher, o en el Perfil del editor), el que entra cierra las demás sesiones de la cuenta con
--   POST /auth/v1/logout?scope=others (la API de Supabase Auth): las otras ya no se pueden renovar.
-- - El juego y el launcher preguntan cada 30 s con rw_check_session si su sesión sigue siendo la de la cuenta. La que
--   no es la más nueva sale sola y avisa «Tu cuenta se abrió en otra computadora» (CA2 y CA3), y no sube nada más (CA4).
-- El launcher y el juego de una misma PC comparten la sesión (el mismo archivo), así que cuentan como una sola (CA5).
-- La sesión es la de Supabase: su id viaja en el token (session_id) y no cambia al renovarlo.
begin;
create or replace function public.rw_check_session()
returns jsonb language plpgsql stable security definer set search_path = '' as $$
declare
 uid uuid := auth.uid();
 sid uuid;
 nueva uuid;
begin
 if uid is null then raise exception 'Authentication required' using errcode='28000'; end if;
 -- Las sesiones anónimas (opiniones y ranking sin cuenta) no cuentan.
 if coalesce((auth.jwt()->>'is_anonymous')::boolean, false) then return jsonb_build_object('active', true); end if;
 begin sid := nullif(auth.jwt()->>'session_id', '')::uuid; exception when others then sid := null; end;
 if sid is null then return jsonb_build_object('active', true); end if; -- un token viejo, sin id de sesión: no se echa a nadie
 -- La sesión más nueva de la cuenta (la del último inicio de sesión) es la única que vale.
 select s.id into nueva from auth.sessions s where s.user_id = uid order by s.created_at desc, s.id desc limit 1;
 return jsonb_build_object('active', nueva is null or nueva = sid);
end $$;
revoke all on function public.rw_check_session() from public, anon;
grant execute on function public.rw_check_session() to authenticated;
commit;
