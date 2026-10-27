-- Self-registration only allows CUSTOMER or RIDER. Promote an existing account to ADMIN with:
--   sqlcmd -S localhost\SQLEXPRESS -E -d DispatchEngine -I -v Email="you@example.com" -i Database\Seed\make_admin.sql
-- then log in again so the new role is in your token.
UPDATE dbo.Users SET Role = 'ADMIN' WHERE Email = '$(Email)';
SELECT Id, Email, Role FROM dbo.Users WHERE Email = '$(Email)';
GO
