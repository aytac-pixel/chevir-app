-- Chevir database (Supabase / Postgres).
-- Run once in the Supabase dashboard: SQL Editor > New query > paste this file > Run.
-- Safe to run again: everything is created only if missing and functions are replaced.

-- ---------------------------------------------------------------- tables

-- Public profile: what other learners may see (name on the league table, tier, totals).
create table if not exists public.profiles (
    id           uuid primary key references auth.users (id) on delete cascade,
    display_name text not null default '',
    league_tier  int  not null default 0 check (league_tier between 0 and 4),
    total_xp     int  not null default 0 check (total_xp >= 0),
    streak       int  not null default 0 check (streak >= 0),
    updated_at   timestamptz not null default now()
);

-- Columns added later (safe on an existing database).
alter table public.profiles add column if not exists avatar_url    text    not null default '';
alter table public.profiles add column if not exists reminders_on  boolean not null default true;
alter table public.profiles add column if not exists practice_from text;
alter table public.profiles add column if not exists practice_to   text;
-- Name sign (ad işarəsi): the nickname or most recognisable feature the learner is called by, e.g. 'eynək'.
alter table public.profiles add column if not exists name_sign     text    not null default '';

-- The app's full saved progress (lessons, hearts, streak, mastery ...) as JSON text. Private to its owner.
create table if not exists public.progress (
    user_id        uuid primary key references auth.users (id) on delete cascade,
    data           text   not null,
    client_updated bigint not null default 0,
    updated_at     timestamptz not null default now()
);

-- XP per ISO week ("2026-W46"), with the tier the learner played that week in: the league ranking.
create table if not exists public.weekly_xp (
    user_id uuid not null references auth.users (id) on delete cascade,
    week    text not null check (week ~ '^[0-9]{4}-W[0-9]{2}$'),
    tier    int  not null default 0 check (tier between 0 and 4),
    xp      int  not null default 0 check (xp >= 0),
    updated_at timestamptz not null default now(),
    primary key (user_id, week)
);
create index if not exists weekly_xp_group on public.weekly_xp (week, tier, xp desc);

-- Weeks already settled for a learner (so promotion / demotion happens once per week).
create table if not exists public.league_settlements (
    user_id  uuid not null references auth.users (id) on delete cascade,
    week     text not null,
    old_tier int  not null,
    new_tier int  not null,
    rank     int  not null,
    group_size int not null,
    settled_at timestamptz not null default now(),
    primary key (user_id, week)
);

-- ---------------------------------------------------------------- row level security

alter table public.profiles           enable row level security;
alter table public.progress           enable row level security;
alter table public.weekly_xp          enable row level security;
alter table public.league_settlements enable row level security;

drop policy if exists "profiles readable by signed-in users" on public.profiles;
create policy "profiles readable by signed-in users" on public.profiles
    for select to authenticated using (true);
drop policy if exists "own profile insert" on public.profiles;
create policy "own profile insert" on public.profiles
    for insert to authenticated with check (id = auth.uid());
drop policy if exists "own profile update" on public.profiles;
create policy "own profile update" on public.profiles
    for update to authenticated using (id = auth.uid()) with check (id = auth.uid());

drop policy if exists "own progress" on public.progress;
create policy "own progress" on public.progress
    for all to authenticated using (user_id = auth.uid()) with check (user_id = auth.uid());

drop policy if exists "own weekly xp" on public.weekly_xp;
create policy "own weekly xp" on public.weekly_xp
    for all to authenticated using (user_id = auth.uid()) with check (user_id = auth.uid());

drop policy if exists "own settlements" on public.league_settlements;
create policy "own settlements" on public.league_settlements
    for select to authenticated using (user_id = auth.uid());

-- The league tier is changed only by settle_league (below), never directly by the app.
create or replace function public.keep_league_tier() returns trigger
language plpgsql as $$
begin
    if current_setting('cevir.settling', true) is distinct from 'on' then
        if tg_op = 'INSERT' then
            new.league_tier := 0;
        else
            new.league_tier := old.league_tier;
        end if;
    end if;
    new.updated_at := now();
    return new;
end $$;

drop trigger if exists profiles_keep_tier on public.profiles;
create trigger profiles_keep_tier before insert or update on public.profiles
    for each row execute function public.keep_league_tier();

-- weekly_xp.tier always follows the learner's current tier, whatever the app sends.
create or replace function public.weekly_xp_tier() returns trigger
language plpgsql security definer set search_path = public as $$
begin
    select coalesce((select league_tier from public.profiles where id = new.user_id), 0) into new.tier;
    new.updated_at := now();
    return new;
end $$;

drop trigger if exists weekly_xp_set_tier on public.weekly_xp;
create trigger weekly_xp_set_tier before insert or update on public.weekly_xp
    for each row execute function public.weekly_xp_tier();

-- A profile row for every new account (name and name sign from the sign-up form).
create or replace function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = public as $$
begin
    insert into public.profiles (id, display_name, name_sign)
    values (new.id,
            coalesce(new.raw_user_meta_data ->> 'display_name', ''),
            left(coalesce(new.raw_user_meta_data ->> 'name_sign', ''), 40))
    on conflict (id) do nothing;
    return new;
end $$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created after insert on auth.users
    for each row execute function public.handle_new_user();

-- ---------------------------------------------------------------- league functions

-- Ranking of the caller's league group for a week: everyone in that tier and week, most XP first (top 30).
create or replace function public.league_standings(p_week text, p_tier int)
returns table (display_name text, xp int, is_you boolean)
language sql stable security definer set search_path = public as $$
    select coalesce(nullif(p.display_name, ''), '?') as display_name,
           w.xp,
           w.user_id = auth.uid() as is_you
    from public.weekly_xp w
    join public.profiles p on p.id = w.user_id
    where w.week = p_week and w.tier = p_tier and auth.uid() is not null
    order by w.xp desc, w.updated_at asc
    limit 30;
$$;

-- Settles every finished week (before p_week) the caller has not settled yet, in order, and returns the tier.
-- Group = everyone in the same week and tier. Top min(3, n/3) go up, bottom min(2, n/3) go down (never below 0,
-- never above 4), so a learner alone in a league neither moves up nor down.
create or replace function public.settle_league(p_week text)
returns int
language plpgsql security definer set search_path = public as $$
declare
    me uuid := auth.uid();
    r record;
    n int;
    my_rank int;
    promote int;
    demote int;
    v_tier int;
    v_old int;
begin
    if me is null then
        raise exception 'not signed in';
    end if;

    insert into public.profiles (id) values (me) on conflict (id) do nothing;
    select league_tier into v_tier from public.profiles where id = me;

    for r in
        select w.week, w.tier, w.xp
        from public.weekly_xp w
        where w.user_id = me
          and w.week < p_week
          and not exists (select 1 from public.league_settlements s where s.user_id = me and s.week = w.week)
        order by w.week
    loop
        select count(*) into n from public.weekly_xp x where x.week = r.week and x.tier = r.tier;
        select 1 + count(*) into my_rank from public.weekly_xp x where x.week = r.week and x.tier = r.tier and x.xp > r.xp;
        promote := least(3, n / 3);
        demote := least(2, n / 3);
        v_old := v_tier;
        if r.xp > 0 and my_rank <= promote then
            v_tier := least(v_tier + 1, 4);
        elsif my_rank > n - demote then
            v_tier := greatest(v_tier - 1, 0);
        end if;
        insert into public.league_settlements (user_id, week, old_tier, new_tier, rank, group_size)
        values (me, r.week, v_old, v_tier, my_rank, n);
    end loop;

    perform set_config('cevir.settling', 'on', true);
    update public.profiles set league_tier = v_tier where id = me;
    perform set_config('cevir.settling', 'off', true);
    return v_tier;
end $$;

revoke all on function public.league_standings(text, int) from public, anon;
revoke all on function public.settle_league(text) from public, anon;
grant execute on function public.league_standings(text, int) to authenticated;
grant execute on function public.settle_league(text) to authenticated;

-- ---------------------------------------------------------------- profile photos

-- Public bucket "avatars": everyone can see a photo, each user writes only the folder named after their id.
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('avatars', 'avatars', true, 2097152, array['image/jpeg', 'image/png'])
on conflict (id) do update set public = true, file_size_limit = 2097152, allowed_mime_types = array['image/jpeg', 'image/png'];

drop policy if exists "avatars read" on storage.objects;
create policy "avatars read" on storage.objects
    for select using (bucket_id = 'avatars');
drop policy if exists "avatars own insert" on storage.objects;
create policy "avatars own insert" on storage.objects
    for insert to authenticated with check (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth.uid()::text);
drop policy if exists "avatars own update" on storage.objects;
create policy "avatars own update" on storage.objects
    for update to authenticated using (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth.uid()::text)
    with check (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth.uid()::text);
drop policy if exists "avatars own delete" on storage.objects;
create policy "avatars own delete" on storage.objects
    for delete to authenticated using (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth.uid()::text);

-- ---------------------------------------------------------------- account deletion

-- Deletes the caller's account; profiles, progress, weekly XP and settlements go with it (on delete cascade).
-- The app removes the photo through the storage API first.
create or replace function public.delete_account()
returns void
language plpgsql security definer set search_path = public as $$
begin
    if auth.uid() is null then
        raise exception 'not signed in';
    end if;
    delete from auth.users where id = auth.uid();
end $$;

revoke all on function public.delete_account() from public, anon;
grant execute on function public.delete_account() to authenticated;
