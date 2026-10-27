CREATE OR ALTER PROCEDURE dbo.sp_GetAllRiders
AS
BEGIN
    SET NOCOUNT ON;

    SELECT r.Id, r.UserId, r.IsOnline, r.CurrentLat, r.CurrentLng, r.LastSeen, r.Rating, u.Name, u.Phone
    FROM dbo.Riders AS r
    JOIN dbo.Users AS u ON u.Id = r.UserId
    ORDER BY r.LastSeen DESC;
END
GO
