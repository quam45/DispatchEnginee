-- Nearest online riders who are not already on a delivery (great-circle distance).
-- The ACOS input is clamped to [-1, 1]: floating-point rounding can push it to 1.0000000001 when a
-- rider stands exactly at the pickup point, and ACOS then fails with "invalid floating point operation".
CREATE OR ALTER PROCEDURE dbo.sp_GetNearestRiders
    @Lat   FLOAT,
    @Lng   FLOAT,
    @Limit INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Limit)
        r.Id, r.UserId, r.IsOnline, r.CurrentLat, r.CurrentLng, r.LastSeen, r.Rating, u.Name,
        6371 * ACOS(CASE WHEN c.x > 1 THEN 1 WHEN c.x < -1 THEN -1 ELSE c.x END) AS DistanceKm
    FROM dbo.Riders AS r
    JOIN dbo.Users AS u ON u.Id = r.UserId
    CROSS APPLY (SELECT COS(RADIANS(@Lat)) * COS(RADIANS(r.CurrentLat)) * COS(RADIANS(r.CurrentLng) - RADIANS(@Lng))
                        + SIN(RADIANS(@Lat)) * SIN(RADIANS(r.CurrentLat)) AS x) AS c
    WHERE r.IsOnline = 1
      AND r.CurrentLat IS NOT NULL AND r.CurrentLng IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Orders AS o WHERE o.RiderId = r.Id AND o.Status = 'ACCEPTED')
    ORDER BY DistanceKm;
END
GO
