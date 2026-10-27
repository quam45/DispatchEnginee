-- Error convention for all procedures: THROW (50000 + HTTP status), 'CODE: Human readable message'.
-- The API's exception handler turns these into { success:false, error:{ code, message } } responses.

-- Creates the user, their wallet and (for riders) the rider profile in one transaction,
-- so a failure can never leave a user without a wallet.
CREATE OR ALTER PROCEDURE dbo.sp_RegisterUser
    @Name         NVARCHAR(100),
    @Phone        NVARCHAR(20),
    @Email        NVARCHAR(100),
    @PasswordHash NVARCHAR(255),
    @Role         NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId INT, @RiderId INT = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.Users (Name, Phone, Email, PasswordHash, Role)
        VALUES (@Name, @Phone, @Email, @PasswordHash, @Role);
        SET @UserId = SCOPE_IDENTITY();

        INSERT INTO dbo.Wallets (UserId, Balance) VALUES (@UserId, 0);

        IF @Role = 'RIDER'
        BEGIN
            INSERT INTO dbo.Riders (UserId, IsOnline) VALUES (@UserId, 0);
            SET @RiderId = SCOPE_IDENTITY();
        END

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        -- The unique indexes are the real guard: two simultaneous sign-ups with the same email both
        -- pass any "does it exist?" check, but only one insert can succeed.
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            IF ERROR_MESSAGE() LIKE '%UX_Users_Email%'
                THROW 50409, 'EMAIL_TAKEN: An account with this email already exists.', 1;
            THROW 50409, 'PHONE_TAKEN: An account with this phone number already exists.', 1;
        END;
        THROW;
    END CATCH

    SELECT @UserId AS Id, @RiderId AS RiderId;
END
GO
