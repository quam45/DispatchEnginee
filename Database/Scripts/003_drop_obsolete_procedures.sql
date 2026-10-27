-- sp_CheckIdempotency/sp_SaveIdempotency: replaced by the atomic idempotency handling inside sp_CreateOrder.
-- sp_GetAllOrders: duplicate of sp_GetAllOrdersList. sp_GetWalletByUserId: duplicate of sp_GetWalletBalance.
DROP PROCEDURE IF EXISTS dbo.sp_CheckIdempotency;
DROP PROCEDURE IF EXISTS dbo.sp_SaveIdempotency;
DROP PROCEDURE IF EXISTS dbo.sp_GetAllOrders;
DROP PROCEDURE IF EXISTS dbo.sp_GetWalletByUserId;
GO
