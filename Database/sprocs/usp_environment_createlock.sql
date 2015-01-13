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
	@lockId char(44),
	@organisation char(44),
	@environment char(44),
	@description nvarchar(4000),
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
SET @RC = 1
BEGIN TRAN
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED
DECLARE @appLockId nvarchar(255) = @lockId + @organisation + @environment
EXEC @RC = sp_getapplock @Resource=@appLockId, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=500
IF @RC >= 0 BEGIN
	IF
		exists(
			SELECT LockId 
			FROM dbo.tbl_environment_locks
			WHERE LockId = @lockId
			AND Organisation = @organisation
			AND Environment = @environment
			AND Stale > SYSUTCDATETIME())
	BEGIN
		SET @RC = -1
	END
	ELSE
	BEGIN
		DELETE FROM dbo.tbl_environment_locks 
		WHERE LockId = @lockId AND Organisation = @organisation AND environment = @environment
		SET @instance = NEWID()
		INSERT INTO dbo.tbl_environment_locks VALUES (@lockId, @organisation, @environment, @description, DATEADD(ms, @stale, SYSUTCDATETIME()), @instance)
	END
END
SELECT @RC
COMMIT
SET NOCOUNT OFF
GO
