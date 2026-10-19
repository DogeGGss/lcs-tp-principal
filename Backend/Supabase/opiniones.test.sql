-- Prueba transaccional: ejecutar en SQL Editor después de opiniones.sql. No deja datos ni avisa a Discord.
begin;
select set_config('rw.sin_discord', '1', true);
insert into auth.users(id) values ('00000000-0000-4000-8000-000000000911'),('00000000-0000-4000-8000-000000000912');
select set_config('request.jwt.claim.sub', '00000000-0000-4000-8000-000000000911', true);
do $$
declare r jsonb; n integer;
begin
 r := public.rw_send_feedback('00000000-0000-4000-8000-0000000009a1', 'sugerencia', 4, '  Probar el mapa nuevo  ', '0.3.0-exp.4', 'Prueba Uno');
 if (r->>'duplicate')::boolean then raise exception 'First send marked as duplicate'; end if;
 -- Reenviar la misma (por un corte) no la duplica.
 r := public.rw_send_feedback('00000000-0000-4000-8000-0000000009a1', 'sugerencia', 4, 'Probar el mapa nuevo', '0.3.0-exp.4', 'Prueba Uno');
 if not (r->>'duplicate')::boolean then raise exception 'Resend not detected'; end if;
 select count(*) into n from public.rw_feedback where player_id = '00000000-0000-4000-8000-000000000911';
 if n <> 1 then raise exception 'Duplicate feedback stored'; end if;
 if (select message from public.rw_feedback where id = '00000000-0000-4000-8000-0000000009a1') <> 'Probar el mapa nuevo' then raise exception 'Message not trimmed'; end if;
 if (select display_name from public.rw_feedback where id = '00000000-0000-4000-8000-0000000009a1') <> 'Prueba Uno' then raise exception 'Name fallback failed'; end if;
 begin
  perform public.rw_send_feedback('00000000-0000-4000-8000-0000000009a2', 'opinion', 3, 'Otra enseguida', null, null);
  raise exception 'Rate limit not applied';
 exception when others then if sqlerrm <> 'Too many feedback' then raise; end if;
 end;
 begin
  perform public.rw_send_feedback('00000000-0000-4000-8000-0000000009a3', 'spam', 3, 'Tipo raro', null, null);
  raise exception 'Invalid kind accepted';
 exception when others then if sqlerrm <> 'Invalid feedback' then raise; end if;
 end;
 begin
  perform public.rw_send_feedback('00000000-0000-4000-8000-0000000009a4', 'opinion', 9, 'Valoración rara', null, null);
  raise exception 'Invalid rating accepted';
 exception when others then if sqlerrm <> 'Invalid feedback' then raise; end if;
 end;
 begin
  perform public.rw_send_feedback('00000000-0000-4000-8000-0000000009a5', 'opinion', 0, '   ', null, null);
  raise exception 'Empty message accepted';
 exception when others then if sqlerrm <> 'Invalid feedback' then raise; end if;
 end;
 begin
  perform public.rw_send_feedback('00000000-0000-4000-8000-0000000009a6', 'opinion', 0, repeat('a', 501), null, null);
  raise exception 'Long message accepted';
 exception when others then if sqlerrm <> 'Invalid feedback' then raise; end if;
 end;
 -- Otro jugador no puede pisar una opinión ajena reusando su id.
 perform set_config('request.jwt.claim.sub', '00000000-0000-4000-8000-000000000912', true);
 begin
  perform public.rw_send_feedback('00000000-0000-4000-8000-0000000009a1', 'opinion', 0, 'Id ajeno', null, null);
  raise exception 'Foreign id accepted';
 exception when others then if sqlerrm <> 'Invalid feedback' then raise; end if;
 end;
 r := public.rw_send_feedback('00000000-0000-4000-8000-0000000009a7', 'error', 0, 'Se cierra al abrir la tienda', null, 'Prueba Dos');
 if (select count(*) from public.rw_feedback where player_id = '00000000-0000-4000-8000-000000000912') <> 1 then raise exception 'Second player not stored'; end if;
 if has_function_privilege('anon', 'public.rw_send_feedback(uuid,text,integer,text,text,text)', 'execute') then raise exception 'Anonymous role can send'; end if;
 if has_table_privilege('authenticated', 'public.rw_feedback', 'select') then raise exception 'Direct read exposed'; end if;
 if has_table_privilege('authenticated', 'public.rw_feedback', 'insert') then raise exception 'Direct write exposed'; end if;
 raise notice 'Feedback backend checks passed';
end $$;
rollback;
