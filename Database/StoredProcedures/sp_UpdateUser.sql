CREATE OR ALTER PROCEDURE dbo.sp_UpdateUser
    @UserId INT,
    @Name   NVARCHAR(100),
    @Phone  NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Rows INT;
    BEGIN TRY
        UPDATE dbo.Users SET Name = @Name, Phone = @Phone WHERE Id = @UserId;
        SET @Rows = @@ROWCOUNT;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() IN (2601, 2627)
            THROW 50409, 'PHONE_TAKEN: An account with this phone number already exists.', 1;
        THROW;
    END CATCH

    IF @Rows = 0
        THROW 50404, 'USER_NOT_FOUND: User not found.', 1;

    SELECT Id, Name, Phone, Email, Role FROM dbo.Users WHERE Id = @UserId;
END
GO
