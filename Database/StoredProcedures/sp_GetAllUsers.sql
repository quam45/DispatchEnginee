-- Paged: result set 1 = total count, result set 2 = the page.
CREATE OR ALTER PROCEDURE dbo.sp_GetAllUsers
    @Page     INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1 SET @Page = 1;
    IF @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;

    SELECT COUNT(*) AS TotalCount FROM dbo.Users;

    SELECT u.Id, u.Name, u.Email, u.Phone, u.Role, ISNULL(w.Balance, 0) AS WalletBalance
    FROM dbo.Users AS u
    LEFT JOIN dbo.Wallets AS w ON w.UserId = u.Id
    ORDER BY u.Id DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO
