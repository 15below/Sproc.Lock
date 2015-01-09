SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO


IF EXISTS (SELECT * FROM sysobjects WHERE type = 'P' AND name = 'usp_organisation_createlock')
	BEGIN
		PRINT 'Dropping Procedure usp_organisation_createlock'
		DROP  Procedure  usp_organisation_createlock
	END

GO

PRINT 'Creating Procedure usp_organisation_createlock'
GO

CREATE Procedure usp_organisation_createlock
	@lockId nvarchar(44),
	@organisation nvarchar(44),
	@stale int,	
	@instance uniqueidentifier OUT
AS

/******************************************************************************
**		File: usp_organisation_createlock
**		Name: usp_organisation_createlock
**		Desc: Creates organisation lock
**
**		Auth: Michael Newton
**		Date: 2014-12-11
*******************************************************************************
**		Change History
*******************************************************************************
**		Date:		Author:				Description:
**		--------	--------			-------------------------------------------
**		2014-12-11	Michael Newton		Created
*******************************************************************************/
SET NOCOUNT ON
DECLARE @RC int
SET @RC = 1
BEGIN TRAN
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED
DECLARE @appLockId nvarchar(255) = @lockId + @organisation
EXEC @RC = sp_getapplock @Resource=@appLockId, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=500
IF @RC >= 0 BEGIN
	IF
		exists(
			SELECT LockId 
			FROM dbo.tbl_organisation_locks
			WHERE LockId = @lockId
			AND Organisation = @organisation
			AND Stale > SYSUTCDATETIME())
	BEGIN
		SET @RC = -1
	END
	ELSE
	BEGIN
		DELETE FROM dbo.tbl_organisation_locks WHERE LockId = @lockId AND Organisation = @organisation
		SET @instance = NEWID()
		INSERT INTO dbo.tbl_organisation_locks VALUES (@lockId, @organisation, DATEADD(ms, @stale, SYSUTCDATETIME()), @instance)
	END
END
SELECT @RC
COMMIT
SET NOCOUNT OFF
GO
