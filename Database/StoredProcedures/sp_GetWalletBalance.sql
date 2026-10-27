-- No NOLOCK: a dirty read could show a balance from a withdrawal that is about to roll back.
CREATE OR ALTER PROCEDURE dbo.sp_GetWalletBalance
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, UserId, Balance, UpdatedAt FROM dbo.Wallets WHERE UserId = @UserId;
END
GO
