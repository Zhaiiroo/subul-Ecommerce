using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <summary>
    /// Forced password change + session stamp on admin_users.
    ///
    /// This schema change first shipped as a hand-applied script
    /// (docs/sql/2026-09-10-admin-user-password-policy.sql), so existing
    /// Development and Staging databases already carry both columns while their
    /// history only records Initial. Up is therefore written as idempotent SQL
    /// rather than AddColumn, which would fail there with "column already exists".
    /// On an empty database it produces exactly what AddColumn would.
    /// </summary>
    public partial class AdminUserPasswordPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE admin_users
                    ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT false;

                ALTER TABLE admin_users
                    ADD COLUMN IF NOT EXISTS password_changed_at timestamp without time zone;

                -- Tokens minted before this column existed carry no stamp and are
                -- rejected on first use. Backfilling from created_at keeps the
                -- stamp non-null for accounts that predate the change.
                UPDATE admin_users
                SET password_changed_at = created_at
                WHERE password_changed_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "password_changed_at",
                table: "admin_users");
        }
    }
}
