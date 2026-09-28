-- Admin user management: forced password change + session stamp.
--
-- The repository carries a single Initial EF migration; schema changes are
-- applied with SQL and mirrored in AppDbContext.Partial.cs and DATABASE.md.
-- Idempotent: safe to re-run on an already-upgraded database.
--
--   psql "$DATABASE_URL" -f docs/sql/2026-09-10-admin-user-password-policy.sql

ALTER TABLE admin_users
    ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT false;

ALTER TABLE admin_users
    ADD COLUMN IF NOT EXISTS password_changed_at timestamp without time zone;

-- Existing accounts keep their password, but every token minted before this
-- upgrade carries no stamp and is rejected on first use, so everyone signs in
-- again once. Backfilling from created_at keeps the stamp non-null going forward.
UPDATE admin_users
SET password_changed_at = created_at
WHERE password_changed_at IS NULL;
