-- Prueba transaccional: ejecutar en SQL Editor después de monedas.sql. No deja jugadores de prueba.
begin;
insert into auth.users(id) values ('00000000-0000-4000-8000-000000000911'),('00000000-0000-4000-8000-000000000912');
select set_config('request.jwt.claim.sub','00000000-0000-4000-8000-000000000911',true);
do $$
declare a jsonb;
begin
 -- Una cuenta nueva arranca en cero (US 237, CA3).
 a := public.rw_wallet_apply('[]');
 if (a->>'creditos')::int <> 0 or (a->>'boletos')::int <> 0 or (a->>'cristales')::int <> 0 then raise exception 'New account should start at zero'; end if;
 -- Ganar y gastar (CA2).
 a := public.rw_wallet_apply('[{"id":"00000000-0000-4000-8000-0000000a0001","motivo":"partida","creditos":150,"boletos":0,"cristales":0},
                               {"id":"00000000-0000-4000-8000-0000000a0002","motivo":"compra","creditos":0,"boletos":5,"cristales":200}]');
 if (a->>'creditos')::int <> 150 or (a->>'boletos')::int <> 5 or (a->>'cristales')::int <> 200 then raise exception 'Earnings not applied'; end if;
 a := public.rw_wallet_apply('[{"id":"00000000-0000-4000-8000-0000000a0003","motivo":"capsula","creditos":0,"boletos":-2,"cristales":0}]');
 if (a->>'boletos')::int <> 3 then raise exception 'Spending not applied'; end if;
 -- Reintentar no repite (CA3).
 a := public.rw_wallet_apply('[{"id":"00000000-0000-4000-8000-0000000a0003","motivo":"capsula","creditos":0,"boletos":-2,"cristales":0},
                               {"id":"00000000-0000-4000-8000-0000000a0001","motivo":"partida","creditos":150,"boletos":0,"cristales":0}]');
 if (a->>'boletos')::int <> 3 or (a->>'creditos')::int <> 150 then raise exception 'Retry applied twice'; end if;
 if jsonb_array_length(a->'rechazados') <> 0 then raise exception 'Retry reported as rejected'; end if;
 -- Un gasto que no alcanza se rechaza y no cambia nada (CA3), y sigue rechazado si se reintenta.
 a := public.rw_wallet_apply('[{"id":"00000000-0000-4000-8000-0000000a0004","motivo":"canje_creditos","creditos":-600,"boletos":1,"cristales":0}]');
 if (a->>'creditos')::int <> 150 or (a->>'boletos')::int <> 3 then raise exception 'Overspend changed the balance'; end if;
 if not (a->'rechazados' ? '00000000-0000-4000-8000-0000000a0004') then raise exception 'Overspend not rejected'; end if;
 a := public.rw_wallet_apply('[{"id":"00000000-0000-4000-8000-0000000a0002","motivo":"compra","creditos":0,"boletos":5,"cristales":200},
                               {"id":"00000000-0000-4000-8000-0000000a0004","motivo":"canje_creditos","creditos":-600,"boletos":1,"cristales":0}]');
 if not (a->'rechazados' ? '00000000-0000-4000-8000-0000000a0004') or (a->>'creditos')::int <> 150 then raise exception 'Rejected move applied on retry'; end if;
 -- Datos inválidos.
 begin
  perform public.rw_wallet_apply('[{"id":"00000000-0000-4000-8000-0000000a0005","motivo":"regalo","creditos":999,"boletos":0,"cristales":0}]');
  raise exception 'Unknown reason accepted';
 exception when others then
  if sqlerrm <> 'Invalid moves' then raise; end if;
 end;
 begin
  perform public.rw_wallet_apply('[{"id":"00000000-0000-4000-8000-0000000a0006","motivo":"partida","creditos":1.5,"boletos":0,"cristales":0}]');
  raise exception 'Fractional amount accepted';
 exception when others then
  if sqlerrm <> 'Invalid moves' then raise; end if;
 end;
 raise notice 'Wallet checks passed';
end $$;
-- Otra cuenta no ve ni usa los movimientos de la primera.
select set_config('request.jwt.claim.sub','00000000-0000-4000-8000-000000000912',true);
do $$
declare a jsonb;
begin
 a := public.rw_wallet_apply('[{"id":"00000000-0000-4000-8000-0000000a0001","motivo":"partida","creditos":150,"boletos":0,"cristales":0}]');
 if (a->>'creditos')::int <> 0 then raise exception 'Another player''s move was applied'; end if;
 if not (a->'rechazados' ? '00000000-0000-4000-8000-0000000a0001') then raise exception 'Foreign move not rejected'; end if;
 if has_function_privilege('anon','public.rw_wallet_apply(jsonb)','execute') then raise exception 'Anonymous role has wallet access'; end if;
 if has_table_privilege('authenticated','public.rw_wallet','update') then raise exception 'Direct wallet write exposed'; end if;
 if has_table_privilege('authenticated','public.rw_wallet_moves','select') then raise exception 'Direct moves read exposed'; end if;
 raise notice 'Wallet isolation checks passed';
end $$;
-- Una sesión anónima no tiene monedas en la nube.
select set_config('request.jwt.claims','{"sub":"00000000-0000-4000-8000-000000000912","is_anonymous":true}',true);
do $$
begin
 begin
  perform public.rw_wallet_apply('[]');
  raise exception 'Anonymous wallet accepted';
 exception when others then
  if sqlerrm <> 'Account required' then raise; end if;
 end;
 raise notice 'Wallet account-only checks passed';
end $$;
rollback;
