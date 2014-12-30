SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO


IF EXISTS (SELECT * FROM sysobjects WHERE type = 'P' AND name = 'usp_global_droplock')
	BEGIN
		PRINT 'Dropping Procedure usp_global_droplock'
		DROP  Procedure  usp_global_droplock
	END

GO

PRINT 'Creating Procedure usp_global_droplock'
GO

CREATE Procedure usp_global_droplock
	@lockId nvarchar(38),
	@instance uniqueidentifier
AS

/******************************************************************************
**		File: usp_global_droplock
**		Name: usp_global_droplock
**		Desc: Deletes global lock
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
EXEC @RC = sp_getapplock @Resource=@lockId, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=500
IF @RC >= 0 BEGIN
	IF exists(
		SELECT LockId 
		FROM dbo.tbl_global_locks 
		WHERE LockId = @lockId
		AND InstanceId = @instance) BEGIN
		DELETE FROM dbo.tbl_global_locks 
		WHERE
			LockId = @lockId
	END
	BEGIN
		SET @RC = -1
	END
END
SELECT @RC
COMMIT
SET NOCOUNT OFF
GO