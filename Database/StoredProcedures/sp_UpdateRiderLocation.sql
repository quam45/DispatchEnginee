-- A location ping also marks the rider online.
CREATE OR ALTER PROCEDURE dbo.sp_UpdateRiderLocation
    @RiderId INT,
    @Lat     FLOAT,
    @Lng     FLOAT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Riders
    SET CurrentLat = @Lat, CurrentLng = @Lng, LastSeen = GETDATE(), IsOnline = 1
    WHERE Id = @RiderId;

    IF @@ROWCOUNT = 0
        THROW 50404, 'RIDER_NOT_FOUND: Rider not found.', 1;

    SELECT Id, UserId, IsOnline, CurrentLat, CurrentLng, LastSeen, Rating FROM dbo.Riders WHERE Id = @RiderId;
END
GO
