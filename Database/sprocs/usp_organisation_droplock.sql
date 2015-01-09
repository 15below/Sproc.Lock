SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO


IF EXISTS (SELECT * FROM sysobjects WHERE type = 'P' AND name = 'usp_organisation_droplock')
	BEGIN
		PRINT 'Dropping Procedure usp_organisation_droplock'
		DROP  Procedure  usp_organisation_droplock
	END

GO

PRINT 'Creating Procedure usp_organisation_droplock'
GO

CREATE Procedure usp_organisation_droplock
	@lockId nvarchar(44),
	@organisation nvarchar(44),
	@instance uniqueidentifier
AS

/******************************************************************************
**		File: usp_organisation_droplock
**		Name: usp_organisation_droplock
**		Desc: Deletes organisation lock
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
DECLARE @appLockId nvarchar(255) = @lockId + @organisation
EXEC @RC = sp_getapplock @Resource=@appLockId, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=500
IF @RC >= 0 BEGIN
	IF exists(
		SELECT LockId 
		FROM dbo.tbl_organisation_locks 
		WHERE LockId = @lockId
		AND Organisation = @organisation
		AND InstanceId = @instance)
	BEGIN
		DELETE FROM dbo.tbl_organisation_locks WITH (ROWLOCK, READPAST)
		WHERE LockId = @lockId
		AND Organisation = @organisation			
	END
	BEGIN
		SET @RC = -1
	END
END
SELECT @RC
COMMIT
SET NOCOUNT OFF
GO