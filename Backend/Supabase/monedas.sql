-- Monedas de la cuenta (F18, US 272). Ejecutar en SQL Editor después de logros.sql. Es reejecutable y no borra nada.
-- El saldo de créditos, boletos y cristales lo lleva la base: con dos PCs no se puede juntar con el máximo, como el
-- resto del progreso, porque al gastar el saldo baja y el máximo devolvería lo gastado. El juego anota cada vez que
-- gana o gasta un movimiento con un id propio y los manda con rw_wallet_apply:
-- - un movimiento que ya llegó (un reintento, un corte en el medio) no se vuelve a aplicar (CA3);
-- - un gasto que no alcanza con el saldo de la base (se gastó en otra PC) se rechaza y no cambia nada (CA3);
-- - lo ganado sin conexión espera en el juego y se manda cuando vuelve; para gastar hace falta conexión (CA4).
-- El sorteo de las cápsulas sigue en el juego: como el resto del progreso, no es a prueba de trampas.
create table if not exists public.rw_wallet (
 player_id uuid primary key references auth.users(id) on delete cascade,
 creditos bigint not null default 0 check (creditos >= 0),
 boletos bigint not null default 0 check (boletos >= 0),
 cristales bigint not null default 0 check (cristales >= 0),
 updated_at timestamptz not null default now()
);
alter table public.rw_wallet enable row level security;
revoke all on public.rw_wallet from anon, authenticated;

create table if not exists public.rw_wallet_moves (
 id uuid primary key,
 player_id uuid not null references auth.users(id) on delete cascade,
 motivo text not null,
 creditos integer not null,
 boletos integer not null,
 cristales integer not null,
 aplicado boolean not null,
 created_at timestamptz not null default now()
);
create index if not exists rw_wallet_moves_player on public.rw_wallet_moves(player_id, created_at);
alter table public.rw_wallet_moves enable row level security;
revoke all on public.rw_wallet_moves from anon, authenticated;

-- Aplica los movimientos en orden y devuelve el saldo de la cuenta y los ids rechazados. Con la lista vacía solo
-- devuelve el saldo (al abrir el juego en otra PC).
create or replace function public.rw_wallet_apply(p_moves jsonb)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare
 uid uuid := auth.uid();
 w public.rw_wallet;
 m jsonb;
 mid uuid;
 v_motivo text;
 c integer; b integer; k integer;
 rechazados jsonb := '[]'::jsonb;
begin
 if uid is null then raise exception 'Authentication required' using errcode='28000'; end if;
 -- Como la nube y el ranking (US 281): solo las cuentas.
 if coalesce((auth.jwt()->>'is_anonymous')::boolean,false) then raise exception 'Account required' using errcode='42501'; end if;
 if p_moves is null or jsonb_typeof(p_moves) <> 'array' or jsonb_array_length(p_moves) > 200 then raise exception 'Invalid moves'; end if;

 insert into public.rw_wallet(player_id) values (uid) on conflict (player_id) do nothing;
 select * into w from public.rw_wallet where player_id = uid for update; -- dos PCs a la vez: de a una

 for m in select * from jsonb_array_elements(p_moves) loop
  v_motivo := coalesce(m->>'motivo','');
  if jsonb_typeof(m) <> 'object' or coalesce(m->>'id','') !~ '^[0-9a-fA-F-]{36}$'
     or v_motivo not in ('partida','repetido','capsula','canje_creditos','canje_cristales','compra','debug')
     or jsonb_typeof(m->'creditos') <> 'number' or jsonb_typeof(m->'boletos') <> 'number' or jsonb_typeof(m->'cristales') <> 'number'
     or (m->>'creditos')::numeric <> trunc((m->>'creditos')::numeric)
     or (m->>'boletos')::numeric <> trunc((m->>'boletos')::numeric)
     or (m->>'cristales')::numeric <> trunc((m->>'cristales')::numeric)
     or abs((m->>'creditos')::numeric) > 100000 or abs((m->>'boletos')::numeric) > 1000 or abs((m->>'cristales')::numeric) > 100000
  then raise exception 'Invalid moves'; end if;
  mid := (m->>'id')::uuid;
  c := (m->>'creditos')::integer; b := (m->>'boletos')::integer; k := (m->>'cristales')::integer;

  -- Ya llegó: no se vuelve a aplicar. Si se había rechazado (o es de otro jugador), sigue rechazado.
  if exists (select 1 from public.rw_wallet_moves where id = mid) then
   if not exists (select 1 from public.rw_wallet_moves where id = mid and player_id = uid and aplicado) then
    rechazados := rechazados || to_jsonb(mid::text);
   end if;
   continue;
  end if;

  if w.creditos + c < 0 or w.boletos + b < 0 or w.cristales + k < 0 then
   insert into public.rw_wallet_moves(id, player_id, motivo, creditos, boletos, cristales, aplicado)
   values (mid, uid, v_motivo, c, b, k, false);
   rechazados := rechazados || to_jsonb(mid::text);
  else
   w.creditos := w.creditos + c; w.boletos := w.boletos + b; w.cristales := w.cristales + k;
   insert into public.rw_wallet_moves(id, player_id, motivo, creditos, boletos, cristales, aplicado)
   values (mid, uid, v_motivo, c, b, k, true);
  end if;
 end loop;

 update public.rw_wallet set creditos = w.creditos, boletos = w.boletos, cristales = w.cristales, updated_at = now()
 where player_id = uid;
 return jsonb_build_object('creditos', w.creditos, 'boletos', w.boletos, 'cristales', w.cristales, 'rechazados', rechazados);
end $$;
revoke all on function public.rw_wallet_apply(jsonb) from public, anon;
grant execute on function public.rw_wallet_apply(jsonb) to authenticated;
