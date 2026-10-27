-- Wallet ledger, for one user (@UserId) or everyone (NULL). Result set 1 = total count, 2 = the page.
CREATE OR ALTER PROCEDURE dbo.sp_GetWalletTransactions
    @UserId   INT = NULL,
    @Page     INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1 SET @Page = 1;
    IF @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.WalletTransactions AS t
    JOIN dbo.Wallets AS w ON w.Id = t.WalletId
    WHERE @UserId IS NULL OR w.UserId = @UserId
    OPTION (RECOMPILE);

    SELECT t.Id, t.WalletId, t.Type, t.Amount, t.Reference, t.Description, t.CreatedAt, u.Name
    FROM dbo.WalletTransactions AS t
    JOIN dbo.Wallets AS w ON w.Id = t.WalletId
    JOIN dbo.Users AS u ON u.Id = w.UserId
    WHERE @UserId IS NULL OR w.UserId = @UserId
    ORDER BY t.CreatedAt DESC, t.Id DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
