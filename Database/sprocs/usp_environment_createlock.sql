SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO


IF EXISTS (SELECT * FROM sysobjects WHERE type = 'P' AND name = 'usp_environment_createlock')
	BEGIN
		PRINT 'Dropping Procedure usp_environment_createlock'
		DROP  Procedure  usp_environment_createlock
	END

GO

PRINT 'Creating Procedure usp_environment_createlock'
GO

CREATE Procedure usp_environment_createlock
	@lockId nvarchar(38),
	@organisation nvarchar(38),
	@environment nvarchar(38),
	@stale int,	
	@instance uniqueidentifier OUT
AS

/******************************************************************************
**		File: usp_environment_createlock
**		Name: usp_environment_createlock
**		Desc: Creates environment lock
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
			FROM dbo.tbl_environment_locks
			WHERE LockId = @lockId
			AND Organisation = @organisation
			AND Environment = @environment
			AND Stale > SYSUTCDATETIME())
	BEGIN
		DELETE FROM dbo.tbl_environment_locks 
		WHERE LockId = @lockId AND Organisation = @organisation AND environment = @environment
		SET @instance = NEWID()
		INSERT INTO dbo.tbl_environment_locks VALUES (@lockId, @organisation, @environment, DATEADD(ms, @stale, SYSUTCDATETIME()), @instance)
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