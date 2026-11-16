-- Reinicio por el catálogo nuevo (US 392, CA10). Lo corre el dueño del proyecto UNA vez en SQL Editor, cuando se
-- publica la versión del juego con el catálogo nuevo. NO SE PUEDE DESHACER.
-- Los créditos, los boletos y los cristales de todos vuelven a lo de una cuenta nueva (0). Los cosméticos, las skins,
-- los emotes y el pase ya empiezan de cero solos: el juego los guarda con claves nuevas ("perfil2.", "skins2.",
-- "emotes2.obtenidos" y "pase2."), así que lo viejo queda en rw_player_progress sin usar. Este script además lo borra,
-- para que la base quede limpia. Las estadísticas, los logros, el ranking, el historial de partidas y lo equipado
-- ("perfil.avatar", etc., que leen los perfiles de los demás) no se tocan.

-- 1. Antes, para ver qué se va a cambiar:
select
 (select count(*) from public.rw_wallet) as billeteras,
 (select coalesce(sum(creditos), 0) from public.rw_wallet) as creditos,
 (select coalesce(sum(boletos), 0) from public.rw_wallet) as boletos,
 (select coalesce(sum(cristales), 0) from public.rw_wallet) as cristales,
 (select count(*) from public.rw_wallet_moves) as movimientos,
 (select count(*) from public.rw_player_progress) as progresos;

-- 2. El reinicio:
begin;
update public.rw_wallet set creditos = 0, boletos = 0, cristales = 0, updated_at = now();
-- Los movimientos viejos ya no cuentan (su id evitaba aplicarlos dos veces; el saldo arranca de cero).
delete from public.rw_wallet_moves;
-- Lo viejo del Perfil (lo conseguido, las cápsulas, las compras y los últimos premios), las skins, los emotes y el pase.
update public.rw_player_progress p set
 contadores = coalesce((select jsonb_object_agg(k, v) from jsonb_each(p.contadores) x(k, v)
   where k not like 'perfil.%' and k not like 'pase.%'), '{}'::jsonb),
 conjuntos = coalesce((select jsonb_object_agg(k, v) from jsonb_each(p.conjuntos) x(k, v)
   where k not like 'perfil.%' and k not like 'skins.%' and k not like 'emotes.%' and k not like 'pase.%'), '{}'::jsonb),
 valores = coalesce((select jsonb_object_agg(k, v) from jsonb_each(p.valores) x(k, v)
   where k not like 'perfil.reciente.%' and k not like 'skins.%'), '{}'::jsonb),
 updated_at = now();
commit;
