SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO


IF EXISTS (SELECT * FROM sysobjects WHERE type = 'P' AND name = 'usp_global_createlock')
	BEGIN
		PRINT 'Dropping Procedure usp_global_createlock'
		DROP  Procedure  usp_global_createlock
	END

GO

PRINT 'Creating Procedure usp_global_createlock'
GO

CREATE Procedure usp_global_createlock
	@lockId nvarchar(38),
	@stale int,	
	@instance uniqueidentifier OUT
AS

/******************************************************************************
**		File: usp_global_createlock
**		Name: usp_global_createlock
**		Desc: Creates across whole database lock
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
BEGIN TRAN
EXEC @RC = sp_getapplock @Resource=@lockId, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=100
IF @RC >= 0 BEGIN
	IF NOT
		exists(
			SELECT LockId 
			FROM dbo.tbl_global_locks
			WHERE LockId = @lockId
			AND Stale > SYSUTCDATETIME())
	BEGIN
		DELETE FROM dbo.tbl_global_locks WHERE LockId = @lockId
		SET @instance = NEWID()
		INSERT INTO dbo.tbl_global_locks VALUES (@lockId, DATEADD(ms, @stale, SYSUTCDATETIME()), @instance)
	END
	ELSE
	BEGIN
		SET @RC = -1
	END
END
SELECT @RC
COMMIT
SET NOCOUNT OFF
GO