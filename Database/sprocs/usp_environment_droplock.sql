SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO


IF EXISTS (SELECT * FROM sysobjects WHERE type = 'P' AND name = 'usp_environment_droplock')
	BEGIN
		PRINT 'Dropping Procedure usp_environment_droplock'
		DROP  Procedure  usp_environment_droplock
	END

GO

PRINT 'Creating Procedure usp_environment_droplock'
GO

CREATE Procedure usp_environment_droplock
	@lockId nvarchar(44),
	@organisation nvarchar(44),
	@environment nvarchar(44),
	@instance uniqueidentifier
AS

/******************************************************************************
**		File: usp_environment_droplock
**		Name: usp_environment_droplock
**		Desc: Deletes environment lock
**
**		Auth: Michael Newton
**		Date: 2014-12-15
*******************************************************************************
**		Change History
*******************************************************************************
**		Date:		Author:				Description:
**		--------	--------			-------------------------------------------
**		2014-12-15	Michael Newton		Created
*******************************************************************************/
SET NOCOUNT ON
DECLARE @RC int
BEGIN TRAN
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED
DECLARE @appLockId nvarchar(255) = @lockId + @organisation + @environment
EXEC @RC = sp_getapplock @Resource=@appLockId, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=500
IF @RC >= 0 BEGIN
	IF exists(
		SELECT LockId 
		FROM dbo.tbl_environment_locks 
		WHERE LockId = @lockId
		AND Organisation = @organisation
		AND Environment = @environment
		AND InstanceId = @instance)
	BEGIN
		DELETE FROM dbo.tbl_environment_locks WITH (ROWLOCK, READPAST)
		WHERE LockId = @lockId
		AND Organisation = @organisation
		AND environment = @environment			
	END
	BEGIN
		SET @RC = -1
	END
END
SELECT @RC
COMMIT
SET NOCOUNT OFF
GO