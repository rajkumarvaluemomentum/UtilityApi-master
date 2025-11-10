USE [ExcelImportDB];
GO

-- =============================================
-- Author:      Jadi Rajkumar
-- Procedure:   sp_CleanupOldRecords
-- Description: Deletes old data from multiple tables
-- Tables:      ErrorRecords, Sales, Customers, Products
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[sp_CleanupOldRecords]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RetentionDays INT = 30;  -- ⏱ keep only last 30 days of data

    BEGIN TRY
        PRINT ' Cleanup started...';

        -- ✅ 1. Delete from ErrorRecords (based on LoggedDate)
        DELETE FROM [dbo].[ErrorRecords]
        WHERE LoggedDate < DATEADD(DAY, -@RetentionDays, GETDATE());

        PRINT CONCAT(' Deleted old ErrorRecords older than ', @RetentionDays, ' days.');

        -- ✅ 2. Delete from Sales (based on CreatedAt column)
        IF COL_LENGTH('dbo.Sales', 'CreatedAt') IS NOT NULL
        BEGIN
            DELETE FROM [dbo].[Sales]
            WHERE CreatedAt < DATEADD(DAY, -@RetentionDays, GETDATE());

            PRINT CONCAT(' Deleted old Sales records older than ', @RetentionDays, ' days.');
        END

        -- ✅ 3. Delete from Products (based on CreatedAt column)
        IF COL_LENGTH('dbo.Products', 'CreatedAt') IS NOT NULL
        BEGIN
            DELETE FROM [dbo].[Products]
            WHERE CreatedAt < DATEADD(DAY, -@RetentionDays, GETDATE());

            PRINT CONCAT(' Deleted old Products older than ', @RetentionDays, ' days.');
        END

        -- ✅ 4. Delete from Customers (based on CreatedAt column)
        IF COL_LENGTH('dbo.Customers', 'CreatedAt') IS NOT NULL
        BEGIN
            DELETE FROM [dbo].[Customers]
            WHERE CreatedAt < DATEADD(DAY, -@RetentionDays, GETDATE());

            PRINT CONCAT(' Deleted old Customers older than ', @RetentionDays, ' days.');
        END

        PRINT 'Cleanup completed successfully.';
    END TRY
    BEGIN CATCH
        DECLARE @ErrMsg NVARCHAR(MAX) = ERROR_MESSAGE();
        PRINT CONCAT('Cleanup failed: ', @ErrMsg);
        RAISERROR('Cleanup failed: %s', 16, 1, @ErrMsg);
    END CATCH
END;
GO

