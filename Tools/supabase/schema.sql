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
-- «Məlumatlarımla Chevir-i inkişaf etdirməyə kömək et»: the learner allows saving their sign attempts (asked at sign-up).
alter table public.profiles add column if not exists data_consent    boolean not null default false;
alter table public.profiles add column if not exists data_consent_at timestamptz;

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

-- XP per month ("2026-11") and per year ("2026"): the monthly and yearly rankings (everyone, no tiers).
create table if not exists public.period_xp (
    user_id uuid not null references auth.users (id) on delete cascade,
    period  text not null check (period ~ '^[0-9]{4}(-[0-9]{2})?$'),
    xp      int  not null default 0 check (xp >= 0),
    updated_at timestamptz not null default now(),
    primary key (user_id, period)
);
create index if not exists period_xp_rank on public.period_xp (period, xp desc);

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

-- Sign attempts for the dataset (only from learners with profiles.data_consent): the MediaPipe landmarks of every
-- frame, what was asked, what the model said, and the path of a short low-resolution video in bucket "sign-samples".
-- landmarks JSON (v1): {"v":1,"aspect":w/h,"face_idx":[mesh point ids],"blendshapes":[names],
--   "frames":[{"t":seconds,"l":[21*xyz],"r":[21*xyz],"p":[33*xyz],"f":[face_idx*xyz],"b":[blendshapes],"h":[pitch,yaw,roll]}]}
-- x, y are normalised to the upright, NOT mirrored camera picture (y down); missing parts are left out of a frame.
create table if not exists public.sign_samples (
    id           uuid primary key default gen_random_uuid(),
    user_id      uuid not null default auth.uid() references auth.users (id) on delete cascade,
    created_at   timestamptz not null default now(),
    sign_id      text,
    gloss        text,
    sign_kind    text,
    pack         text,
    context      text,
    context_kind text,
    judge        text,
    score        real,
    correct      boolean not null default false,
    feedback     text,
    duration_s   real,
    frame_count  int,
    left_handed  boolean not null default false,
    app_version  text,
    device_model text,
    os           text,
    video_path   text,
    landmarks    jsonb not null
);
create index if not exists sign_samples_gloss on public.sign_samples (gloss, correct);
create index if not exists sign_samples_user on public.sign_samples (user_id, created_at desc);

-- ---------------------------------------------------------------- row level security

alter table public.profiles           enable row level security;
alter table public.progress           enable row level security;
alter table public.weekly_xp          enable row level security;
alter table public.period_xp          enable row level security;
alter table public.league_settlements enable row level security;
alter table public.sign_samples       enable row level security;

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

drop policy if exists "own period xp" on public.period_xp;
create policy "own period xp" on public.period_xp
    for all to authenticated using (user_id = auth.uid()) with check (user_id = auth.uid());

drop policy if exists "own settlements" on public.league_settlements;
create policy "own settlements" on public.league_settlements
    for select to authenticated using (user_id = auth.uid());

-- True when the signed-in learner allowed saving sign samples.
create or replace function public.has_data_consent() returns boolean
language sql stable security definer set search_path = public as $$
    select coalesce((select data_consent from public.profiles where id = auth.uid()), false);
$$;
revoke all on function public.has_data_consent() from public, anon;
grant execute on function public.has_data_consent() to authenticated;

-- Learners add only their own samples and only with permission; they can list and delete theirs (to withdraw).
-- The team reads everything in the dashboard (Table Editor / SQL / Storage), which bypasses these rules.
drop policy if exists "own sign samples insert" on public.sign_samples;
create policy "own sign samples insert" on public.sign_samples
    for insert to authenticated with check (user_id = auth.uid() and public.has_data_consent());
drop policy if exists "own sign samples select" on public.sign_samples;
create policy "own sign samples select" on public.sign_samples
    for select to authenticated using (user_id = auth.uid());
drop policy if exists "own sign samples delete" on public.sign_samples;
create policy "own sign samples delete" on public.sign_samples
    for delete to authenticated using (user_id = auth.uid());

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
    -- When the data permission was given (or taken back).
    if tg_op = 'INSERT' then
        if new.data_consent then new.data_consent_at := now(); end if;
    elsif new.data_consent is distinct from old.data_consent then
        new.data_consent_at := now();
    else
        new.data_consent_at := old.data_consent_at;
    end if;
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

-- A profile row for every new account (name, name sign and the data permission from the sign-up form).
create or replace function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = public as $$
begin
    insert into public.profiles (id, display_name, name_sign, data_consent)
    values (new.id,
            coalesce(new.raw_user_meta_data ->> 'display_name', ''),
            left(coalesce(new.raw_user_meta_data ->> 'name_sign', ''), 40),
            coalesce(new.raw_user_meta_data ->> 'data_consent', '') = 'true')
    on conflict (id) do nothing;
    return new;
end $$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created after insert on auth.users
    for each row execute function public.handle_new_user();

-- ---------------------------------------------------------------- league functions

-- Ranking of the caller's league group for a week: everyone in that tier and week, most XP first (top 30),
-- with each learner's profile photo. (Dropped first: the result columns changed when avatar_url was added.)
drop function if exists public.league_standings(text, int);
create or replace function public.league_standings(p_week text, p_tier int)
returns table (display_name text, avatar_url text, xp int, is_you boolean)
language sql stable security definer set search_path = public as $$
    select coalesce(nullif(p.display_name, ''), '?') as display_name,
           coalesce(p.avatar_url, '') as avatar_url,
           w.xp,
           w.user_id = auth.uid() as is_you
    from public.weekly_xp w
    join public.profiles p on p.id = w.user_id
    where w.week = p_week and w.tier = p_tier and auth.uid() is not null
    order by w.xp desc, w.updated_at asc
    limit 30;
$$;

-- Monthly ("2026-11") or yearly ("2026") ranking of all learners by XP: the top 50, plus the caller's own row
-- with their place even when they are further down.
create or replace function public.period_standings(p_period text)
returns table (display_name text, avatar_url text, xp int, is_you boolean, rank int)
language sql stable security definer set search_path = public as $$
    with ranked as (
        select x.user_id, x.xp, x.updated_at,
               (rank() over (order by x.xp desc))::int as place
        from public.period_xp x
        where x.period = p_period and auth.uid() is not null
    )
    select coalesce(nullif(p.display_name, ''), '?') as display_name,
           coalesce(p.avatar_url, '') as avatar_url,
           r.xp,
           r.user_id = auth.uid() as is_you,
           r.place as rank
    from ranked r
    join public.profiles p on p.id = r.user_id
    where r.place <= 50 or r.user_id = auth.uid()
    order by (r.user_id = auth.uid()) desc, r.xp desc, r.updated_at asc
    limit 51;
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
revoke all on function public.period_standings(text) from public, anon;
revoke all on function public.settle_league(text) from public, anon;
grant execute on function public.league_standings(text, int) to authenticated;
grant execute on function public.period_standings(text) to authenticated;
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

-- ---------------------------------------------------------------- sign sample videos

-- Private bucket "sign-samples": short Motion-JPEG .avi clips (max 1 MB), in a folder named after the learner's id.
-- Only the team sees them (dashboard > Storage); a learner may upload only with permission and delete their own.
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('sign-samples', 'sign-samples', false, 1048576, array['video/x-msvideo', 'video/avi'])
on conflict (id) do update set public = false, file_size_limit = 1048576, allowed_mime_types = array['video/x-msvideo', 'video/avi'];

drop policy if exists "sign samples own insert" on storage.objects;
create policy "sign samples own insert" on storage.objects
    for insert to authenticated with check (bucket_id = 'sign-samples' and (storage.foldername(name))[1] = auth.uid()::text
                                            and public.has_data_consent());
drop policy if exists "sign samples own select" on storage.objects;
create policy "sign samples own select" on storage.objects
    for select to authenticated using (bucket_id = 'sign-samples' and (storage.foldername(name))[1] = auth.uid()::text);
drop policy if exists "sign samples own delete" on storage.objects;
create policy "sign samples own delete" on storage.objects
    for delete to authenticated using (bucket_id = 'sign-samples' and (storage.foldername(name))[1] = auth.uid()::text);

-- ---------------------------------------------------------------- account deletion

-- Deletes the caller's account; profiles, progress, weekly XP, settlements and sign samples go with it (on delete
-- cascade). The app removes the photo and the sample videos through the storage API first.
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
