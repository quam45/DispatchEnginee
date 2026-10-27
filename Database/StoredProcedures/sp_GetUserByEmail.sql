-- Used by login. RiderId is returned so it can go into the JWT, which lets rider endpoints
-- trust the token instead of a riderId supplied by the client.
CREATE OR ALTER PROCEDURE dbo.sp_GetUserByEmail
    @Email NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT u.Id, u.Name, u.Phone, u.Email, u.PasswordHash, u.Role, r.Id AS RiderId
    FROM dbo.Users AS u
    LEFT JOIN dbo.Riders AS r ON r.UserId = u.Id
    WHERE u.Email = @Email;
END
GO
