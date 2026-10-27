CREATE OR ALTER PROCEDURE dbo.sp_GetAllWallets
AS
BEGIN
    SET NOCOUNT ON;

    SELECT w.Id, w.UserId, w.Balance, w.UpdatedAt, u.Name
    FROM dbo.Wallets AS w
    JOIN dbo.Users AS u ON u.Id = w.UserId
    ORDER BY w.Id;
END
GO
